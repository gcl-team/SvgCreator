using SkiaSharp;
using SvgCreator.Imaging;
using System.Xml.Linq;

namespace SvgCreator.Tests;

public class ImageOrientationTests
{
    // A 60×30 JPEG whose top-left quadrant is red and the rest blue, with an EXIF orientation tag
    // (value at byte 31). Fixed bytes, so the test doesn't depend on an encoder writing EXIF.
    private const string QuadrantJpegBase64 =
        "/9j/4QAiRXhpZgAATU0AKgAAAAgAAQESAAMAAAABAAEAAAAAAAD/4AAQSkZJRgABAQAASABIAAD/4QBMRXhpZgAATU0AKgAA" +
        "AAgAAYdpAAQAAAABAAAAGgAAAAAAA6ABAAMAAAABAAEAAKACAAQAAAABAAAAPKADAAQAAAABAAAAHgAAAAD/7QA4UGhvdG9z" +
        "aG9wIDMuMAA4QklNBAQAAAAAAAA4QklNBCUAAAAAABDUHYzZjwCyBOmACZjs+EJ+/8AAEQgAHgA8AwEiAAIRAQMRAf/EAB8A" +
        "AAEFAQEBAQEBAAAAAAAAAAABAgMEBQYHCAkKC//EALUQAAIBAwMCBAMFBQQEAAABfQECAwAEEQUSITFBBhNRYQcicRQygZGh" +
        "CCNCscEVUtHwJDNicoIJChYXGBkaJSYnKCkqNDU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6g4SFhoeI" +
        "iYqSk5SVlpeYmZqio6Slpqeoqaqys7S1tre4ubrCw8TFxsfIycrS09TV1tfY2drh4uPk5ebn6Onq8fLz9PX29/j5+v/EAB8B" +
        "AAMBAQEBAQEBAQEAAAAAAAABAgMEBQYHCAkKC//EALURAAIBAgQEAwQHBQQEAAECdwABAgMRBAUhMQYSQVEHYXETIjKBCBRC" +
        "kaGxwQkjM1LwFWJy0QoWJDThJfEXGBkaJicoKSo1Njc4OTpDREVGR0hJSlNUVVZXWFlaY2RlZmdoaWpzdHV2d3h5eoKDhIWG" +
        "h4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uLj5OXm5+jp6vLz9PX29/j5+v/bAEMA" +
        "AQEBAQEBAgEBAgICAgICAwICAgIDBAMDAwMDBAUEBAQEBAQFBQUFBQUFBQYGBgYGBgcHBwcHCAgICAgICAgICP/bAEMBAQEB" +
        "AgICAwICAwgFBQUICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICP/dAAQABP/aAAwD" +
        "AQACEQMRAD8A/Leis3+2dH/5+7b/AL+r/jR/bOj/APP3bf8Af1f8a/jr/iF3E/8A0KK//gmp/wDIn/V3/wARV4X/AOhxh/8A" +
        "wdT/APkjSrm/F3/IvXH/AGz/APQ1rS/tnR/+fu2/7+r/AI1geKNT0240KeG3uIHc7MKkisTh1PABr9u+jP4ccRUPEjhSvXyu" +
        "tGEcdhG26U0klXptttxsklq29j8M+lB4lcOV/DTi2hQzWhKcsDi0kqtNtt4eokklK7beiS1bPIKKKK/6/wA/41wooooA/9D8" +
        "J6KKK/6qD+HwooooAKKKKACiiigD/9k=";

    private const int OrientationByte = 31;

    private static MemoryStream Jpeg(int orientation)
    {
        var bytes = Convert.FromBase64String(QuadrantJpegBase64);
        bytes[OrientationByte] = (byte)orientation;
        return new MemoryStream(bytes);
    }

    // Where the red quadrant appears once the picture is upright, and whether the axes swap.
    // Orientation 6 was checked against Chrome, which rotates the embedded JPEG the same way.
    [Theory]
    [InlineData(1, "TopLeft", false)]     // upright
    [InlineData(2, "TopRight", false)]    // mirrored
    [InlineData(3, "BottomRight", false)] // 180°
    [InlineData(4, "BottomLeft", false)]  // flipped
    [InlineData(5, "TopLeft", true)]      // transposed
    [InlineData(6, "TopRight", true)]     // 90° clockwise
    [InlineData(7, "BottomRight", true)]  // transversed
    [InlineData(8, "BottomLeft", true)]   // 90° anticlockwise
    public void Load_TurnsThePictureUpright(int orientation, string redQuadrant, bool swapped)
    {
        using var jpeg = Jpeg(orientation);

        var image = ImageLoader.Load(jpeg);

        Assert.Equal(swapped ? (30, 60) : (60, 30), (image.Width, image.Height));
        foreach (var quadrant in new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
        {
            var x = quadrant.EndsWith("Left", StringComparison.Ordinal) ? image.Width / 4 : image.Width * 3 / 4;
            var y = quadrant.StartsWith("Top", StringComparison.Ordinal) ? image.Height / 4 : image.Height * 3 / 4;
            var i = ((y * image.Width) + x) * 3;
            var isRed = image.Pixels[i] > 150 && image.Pixels[i + 2] < 100;
            Assert.True(isRed == (quadrant == redQuadrant), $"{quadrant} should {(quadrant == redQuadrant ? "" : "not ")}be red");
        }
    }

    [Theory]
    [InlineData(1, 60, 30)]
    [InlineData(3, 60, 30)]
    [InlineData(6, 30, 60)]
    [InlineData(8, 30, 60)]
    public void PostageStamp_EmbedsTheOriginalBytesAtTheDisplayedSize(int orientation, int width, int height)
    {
        using var jpeg = Jpeg(orientation);
        XNamespace svg = "http://www.w3.org/2000/svg";

        var image = XDocument.Parse(new PostageStampGenerator().Create(jpeg, new PostageStampOptions { Country = "X" }))
            .Descendants(svg + "image").Single();

        // The browser applies the tag to the untouched bytes; the frame must match what it shows.
        Assert.StartsWith("data:image/jpeg;base64,", (string?)image.Attribute("href"), StringComparison.Ordinal);
        Assert.Equal(width.ToString(System.Globalization.CultureInfo.InvariantCulture), (string?)image.Attribute("width"));
        Assert.Equal(height.ToString(System.Globalization.CultureInfo.InvariantCulture), (string?)image.Attribute("height"));
    }

    [Fact]
    public void Load_UprightImage_AllocatesOnlyTheRgbBuffer()
    {
        // The upright path converts the decoded bitmap (native memory) straight to RGB. Turning
        // every picture through a packed RGBA copy first cost 4 more managed bytes per pixel.
        const int Side = 1000;
        byte[] png;
        using (var encoded = TestImages.PngWithBlackRect(Side, Side, new SKRect(10, 10, 20, 20)))
        {
            png = encoded.ToArray();
        }

        // SKCodec closes the stream it reads, so each load gets its own.
        ImageLoader.Load(new MemoryStream(png)); // warm up: JIT and Skia statics
        using var input = new MemoryStream(png);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var image = ImageLoader.Load(input);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(Side, image.Width);
        Assert.InRange(allocated, Side * Side * 3, Side * Side * 4); // the 3-byte RGB buffer, little else
    }

    [Fact]
    public void Orient_RightTop_TurnsStoredPixelsClockwise()
    {
        // 3×2 stored, one byte of R per pixel to follow them:  a b c / d e f
        byte[] rgba = [.. "abcdef".SelectMany(c => new[] { (byte)c, (byte)0, (byte)0, (byte)255 })];

        var (pixels, width, height) = ImageLoader.Orient(rgba, 3, 2, 12, 4, SKEncodedOrigin.RightTop);

        // Turned 90° clockwise: d a / e b / f c
        Assert.Equal((2, 3), (width, height));
        Assert.Equal("daebfc", new string([.. Enumerable.Range(0, 6).Select(i => (char)pixels[i * 4])]));
    }
}
