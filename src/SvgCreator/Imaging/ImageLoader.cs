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
        using var codec = OpenCodec(stream);

        // Straight from the decoded bitmap to RGB; only a picture that needs turning pays for a
        // second buffer, and then at 3 bytes per pixel rather than RGBA's 4.
        using var bitmap = Decode(codec);
        var rgb = ToRgb(bitmap.GetPixelSpan(), bitmap.Width, bitmap.Height, bitmap.RowBytes);
        if (codec.EncodedOrigin is SKEncodedOrigin.TopLeft)
        {
            return rgb;
        }

        var (pixels, width, height) = Orient(rgb.Pixels, rgb.Width, rgb.Height, rgb.Width * 3, 3, codec.EncodedOrigin);
        return new RgbImage(width, height, pixels);
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
    /// Reads an image for embedding as is. Formats every current browser shows (PNG, JPEG, WebP,
    /// GIF, BMP, AVIF) keep their original bytes; anything else SkiaSharp decodes is re-encoded as
    /// PNG. AVIF is kept only where the SkiaSharp build can read it at all; the stock builds can't,
    /// and reject it as unsupported.
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
        using var codec = OpenCodec(new MemoryStream(data));

        var mediaType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Webp => "image/webp",
            SKEncodedImageFormat.Gif => "image/gif",
            SKEncodedImageFormat.Bmp => "image/bmp",
            SKEncodedImageFormat.Avif => "image/avif",
            _ => null, // such as HEIF, which only Safari shows
        };
        if (mediaType is not null)
        {
            // The browser applies the orientation to the original bytes, so report the size it shows.
            var (w, h) = DisplayedSize(codec);
            return new EncodedImage(data, mediaType, w, h);
        }

        // Re-encoding drops the EXIF tag, so bake the orientation into the pixels instead.
        using var decoded = Decode(codec);
        using var png = codec.EncodedOrigin is SKEncodedOrigin.TopLeft
            ? decoded.Encode(SKEncodedImageFormat.Png, 100)
            : Upright(decoded, codec.EncodedOrigin);
        var (width, height) = DisplayedSize(codec);
        return new EncodedImage(png.ToArray(), "image/png", width, height);
    }

    private static (int Width, int Height) DisplayedSize(SKCodec codec) =>
        SwapsAxes(codec.EncodedOrigin) ? (codec.Info.Height, codec.Info.Width) : (codec.Info.Width, codec.Info.Height);

    private static SKData Upright(SKBitmap decoded, SKEncodedOrigin origin)
    {
        var (rgba, width, height) = Orient(decoded.GetPixelSpan(), decoded.Width, decoded.Height, decoded.RowBytes, 4, origin);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(rgba, y * width * 4, bitmap.GetPixels() + (y * bitmap.RowBytes), width * 4);
        }

        return bitmap.Encode(SKEncodedImageFormat.Png, 100);
    }

    /// <summary>
    /// Opens a codec, or throws with a reason a person can act on. SkiaSharp reports the same
    /// <see cref="SKCodecResult.Unimplemented"/> for a format it has no decoder for and for random
    /// bytes, so formats it can't read are recognized here from their header instead.
    /// </summary>
    private static SKCodec OpenCodec(Stream stream)
    {
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            stream = buffer;
        }

        var start = stream.Position;
        var header = new byte[32];
        var length = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = start;

        return SKCodec.Create(stream, out var result)
            ?? throw new InvalidDataException(DescribeFailure(header.AsSpan(0, length), result));
    }

    internal static string DescribeFailure(ReadOnlySpan<byte> header, SKCodecResult result) => IsoMediaFormat(header) switch
    {
        { } format => $"{format} images aren't supported: SkiaSharp has no {format} decoder. Convert the image to PNG, JPEG or WebP.",
        null when result == SKCodecResult.IncompleteInput => "The image data is incomplete; the file may be truncated.",
        null => "Unsupported or corrupt image data.",
    };

    // AVIF and HEIF are ISO media files: an 'ftyp' box at the start lists brands (the major brand,
    // then compatible ones). AVIF files also list the generic HEIF brand 'mif1', so check AVIF first.
    private static string? IsoMediaFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length < 12 || !header.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return null;
        }

        var boxSize = Math.Min(header.Length, (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header));
        var brands = new List<string>();
        for (var i = 8; i + 4 <= boxSize; i += 4)
        {
            if (i != 12) // bytes 12–15 are the minor version, not a brand
            {
                brands.Add(System.Text.Encoding.ASCII.GetString(header.Slice(i, 4)));
            }
        }

        return brands.Any(b => b is "avif" or "avis") ? "AVIF"
            : brands.Any(b => b is "heic" or "heix" or "hevc" or "hevx" or "heim" or "heis" or "mif1" or "msf1") ? "HEIF"
            : null;
    }

    // Decodes the stored pixel grid as unpremultiplied RGBA8888.
    private static SKBitmap Decode(SKCodec codec)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new InvalidDataException($"Failed to decode image: {result}.");
        }

        return bitmap;
    }

    private static bool SwapsAxes(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// Turns stored pixels upright for an EXIF orientation, returning them tightly packed with
    /// <paramref name="bytesPerPixel"/> bytes each (4 for RGBA, 3 for RGB). Each
    /// origin names where the stored first row and column end up: RightTop (6) means the stored top
    /// row is shown as the right column, a 90° clockwise turn.
    /// </summary>
    internal static (byte[] Pixels, int Width, int Height) Orient(ReadOnlySpan<byte> pixels, int width, int height, int rowBytes, int bytesPerPixel, SKEncodedOrigin origin)
    {
        var (w, h) = SwapsAxes(origin) ? (height, width) : (width, height);
        var output = new byte[w * h * bytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * rowBytes, width * bytesPerPixel);
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
                row.Slice(x * bytesPerPixel, bytesPerPixel).CopyTo(output.AsSpan(((oy * w) + ox) * bytesPerPixel, bytesPerPixel));
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
