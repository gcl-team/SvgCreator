using System.Runtime.InteropServices;
using SkiaSharp;

namespace SvgCreator.Imaging;

/// <summary>
/// Decodes images with SkiaSharp and flattens them to opaque RGB over a white background. Every
/// path honours the EXIF orientation, the way browsers display the image: SkiaSharp reports and
/// decodes the stored pixel grid, which for orientations 5–8 is the displayed picture on its side.
/// </summary>
internal static class ImageLoader
{
    public static RgbImage Load(Stream stream)
    {
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException("Unsupported or corrupt image data.");

        var (rgba, width, height) = Decode(codec);
        return ToRgb(rgba, width, height, width * 4);
    }

    public static async Task<RgbImage> LoadAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Skia decodes synchronously; buffer the input asynchronously so I/O doesn't block.
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return Load(buffer);
    }

    /// <summary>
    /// Reads an image for embedding as is. Formats every browser shows (PNG, JPEG, WebP, GIF, BMP)
    /// keep their original bytes; anything else is re-encoded as PNG.
    /// </summary>
    public static EncodedImage LoadEncoded(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return ToEncoded(buffer.ToArray());
    }

    public static async Task<EncodedImage> LoadEncodedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return ToEncoded(buffer.ToArray());
    }

    private static EncodedImage ToEncoded(byte[] data)
    {
        using var codec = SKCodec.Create(new MemoryStream(data))
            ?? throw new InvalidDataException("Unsupported or corrupt image data.");

        var mediaType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Webp => "image/webp",
            SKEncodedImageFormat.Gif => "image/gif",
            SKEncodedImageFormat.Bmp => "image/bmp",
            _ => null,
        };
        if (mediaType is not null)
        {
            // The browser applies the orientation to the original bytes, so report the size it shows.
            var (w, h) = SwapsAxes(codec.EncodedOrigin)
                ? (codec.Info.Height, codec.Info.Width)
                : (codec.Info.Width, codec.Info.Height);
            return new EncodedImage(data, mediaType, w, h);
        }

        // Re-encoding drops the EXIF tag, so bake the orientation into the pixels instead.
        var (rgba, width, height) = Decode(codec);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(rgba, y * width * 4, bitmap.GetPixels() + (y * bitmap.RowBytes), width * 4);
        }

        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return new EncodedImage(png.ToArray(), "image/png", width, height);
    }

    // Decodes to tightly packed, unpremultiplied RGBA8888, turned upright.
    private static (byte[] Rgba, int Width, int Height) Decode(SKCodec codec)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidDataException($"Failed to decode image: {result}.");
        }

        return Orient(bitmap.GetPixelSpan(), info.Width, info.Height, bitmap.RowBytes, codec.EncodedOrigin);
    }

    private static bool SwapsAxes(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// Turns stored pixels upright for an EXIF orientation, returning tightly packed RGBA. Each
    /// origin names where the stored first row and column end up: RightTop (6) means the stored top
    /// row is shown as the right column, a 90° clockwise turn.
    /// </summary>
    internal static (byte[] Rgba, int Width, int Height) Orient(ReadOnlySpan<byte> rgba, int width, int height, int rowBytes, SKEncodedOrigin origin)
    {
        var (w, h) = SwapsAxes(origin) ? (height, width) : (width, height);
        var output = new byte[w * h * 4];
        for (var y = 0; y < height; y++)
        {
            var row = rgba.Slice(y * rowBytes, width * 4);
            for (var x = 0; x < width; x++)
            {
                var (ox, oy) = origin switch
                {
                    SKEncodedOrigin.TopRight => (width - 1 - x, y),                 // 2: mirrored
                    SKEncodedOrigin.BottomRight => (width - 1 - x, height - 1 - y), // 3: 180°
                    SKEncodedOrigin.BottomLeft => (x, height - 1 - y),              // 4: flipped
                    SKEncodedOrigin.LeftTop => (y, x),                              // 5: transposed
                    SKEncodedOrigin.RightTop => (height - 1 - y, x),                // 6: 90° clockwise
                    SKEncodedOrigin.RightBottom => (height - 1 - y, width - 1 - x), // 7: transversed
                    SKEncodedOrigin.LeftBottom => (y, width - 1 - x),               // 8: 90° anticlockwise
                    _ => (x, y),                                                    // 1: upright
                };
                row.Slice(x * 4, 4).CopyTo(output.AsSpan(((oy * w) + ox) * 4, 4));
            }
        }

        return (output, w, h);
    }

    /// <summary>Composites unpremultiplied RGBA8888 pixels over white, so transparent areas become background.</summary>
    internal static RgbImage ToRgb(ReadOnlySpan<byte> rgba, int width, int height, int rowBytes)
    {
        var pixels = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            var row = rgba.Slice(y * rowBytes, width * 4);
            var target = pixels.AsSpan(y * width * 3, width * 3);
            for (var x = 0; x < width; x++)
            {
                var alpha = row[(x * 4) + 3];
                for (var c = 0; c < 3; c++)
                {
                    target[(x * 3) + c] = (byte)(((row[(x * 4) + c] * alpha) + (255 * (255 - alpha))) / 255);
                }
            }
        }

        return new RgbImage(width, height, pixels);
    }
}
