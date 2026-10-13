using SkiaSharp;

namespace SvgCreator.Imaging;

/// <summary>Decodes images with SkiaSharp and flattens them to opaque RGB over a white background.</summary>
internal static class ImageLoader
{
    public static RgbImage Load(Stream stream)
    {
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException("Unsupported or corrupt image data.");

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidDataException($"Failed to decode image: {result}.");
        }

        return ToRgb(bitmap.GetPixelSpan(), info.Width, info.Height, bitmap.RowBytes);
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
        var (width, height) = (codec.Info.Width, codec.Info.Height);

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
            return new EncodedImage(data, mediaType, width, height);
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidDataException($"Failed to decode image: {result}.");
        }

        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return new EncodedImage(png.ToArray(), "image/png", width, height);
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
