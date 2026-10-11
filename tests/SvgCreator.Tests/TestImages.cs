using SkiaSharp;
using SvgCreator.Imaging;

namespace SvgCreator.Tests;

/// <summary>Builds small synthetic images for tests; no binary fixtures needed.</summary>
internal static partial class TestImages
{
    /// <summary>Builds a mask from ASCII art: <c>#</c> = ink, anything else = background.</summary>
    public static BinaryImage Mask(params string[] rows)
    {
        var width = rows[0].Length;
        var pixels = new byte[width * rows.Length];
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = rows[y][x] == '#' ? (byte)1 : (byte)0;
            }
        }

        return new BinaryImage(width, rows.Length, pixels);
    }

    /// <summary>Encodes a white PNG with a black rectangle drawn on it.</summary>
    public static MemoryStream PngWithBlackRect(int width, int height, SKRect rect)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(rect, paint);
        }

        var stream = new MemoryStream();
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        stream.Position = 0;
        return stream;
    }
}
