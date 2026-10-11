using System.Xml.Linq;
using SkiaSharp;

namespace SvgCreator.Tests;

public class SvgConverterTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Fact]
    public void Convert_PngWithBlackSquare_ReturnsSvgWithOneSquarePath()
    {
        using var png = TestImages.PngWithBlackRect(20, 10, new SKRect(2, 3, 8, 7));

        var svg = new SvgConverter().Convert(png, new SvgConversionOptions { Mode = ExtractionMode.Threshold });

        var doc = XDocument.Parse(svg);
        Assert.Equal("0 0 20 10", (string?)doc.Root!.Attribute("viewBox"));
        var path = Assert.Single(doc.Descendants(Svg + "path"));
        Assert.Equal("M8 3l0 4l-6 0l0-4Z", (string?)path.Attribute("d"));
    }

    [Fact]
    public void Convert_LinesMode_OutlinesShapeWithoutFillingDarkBackground()
    {
        // A light disc on a dark background: Lines mode should outline the disc, not fill the background.
        using var bitmap = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.Clear(new SKColor(30, 50, 200));
            canvas.DrawCircle(32, 32, 20, paint);
        }

        using var png = new MemoryStream();
        bitmap.Encode(png, SKEncodedImageFormat.Png, 100);
        png.Position = 0;

        var svg = new SvgConverter().Convert(png);

        var path = Assert.Single(XDocument.Parse(svg).Descendants(Svg + "path"));
        var d = (string)path.Attribute("d")!;
        Assert.DoesNotContain("M0 0", d, StringComparison.Ordinal); // no full-canvas background shape
        Assert.Equal(2, d.Count(c => c == 'M')); // a ring: outer border + hole
    }

    [Fact]
    public async Task ConvertAsync_BlankImage_ReturnsSvgWithoutPaths()
    {
        using var png = TestImages.PngWithBlackRect(5, 5, SKRect.Empty);

        var svg = await new SvgConverter().ConvertAsync(png, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(XDocument.Parse(svg).Descendants(Svg + "path"));
    }

    [Fact]
    public void Convert_InvalidImageData_ThrowsInvalidDataException()
    {
        using var garbage = new MemoryStream([1, 2, 3, 4]);

        Assert.Throws<InvalidDataException>(() => new SvgConverter().Convert(garbage));
    }

    [Theory]
    [InlineData("#c0392b")]
    [InlineData("#C0392B80")]
    [InlineData("#f00")]
    [InlineData("navy")]
    [InlineData("rgb(192, 57, 43)")]
    [InlineData("hsl(6 63% 46% / .5)")]
    public void Convert_CustomInkColor_IsUsedAsFill(string color)
    {
        using var png = TestImages.PngWithBlackRect(20, 10, new SKRect(2, 3, 8, 7));

        var svg = new SvgConverter().Convert(png, new SvgConversionOptions { FillColor = color });

        var group = Assert.Single(XDocument.Parse(svg).Descendants(Svg + "g"));
        Assert.Equal(color, (string?)group.Attribute("fill"));
    }

    [Theory]
    [InlineData("#00g")]
    [InlineData("#12345")]
    [InlineData("red;stroke:blue")]
    [InlineData("url(#x)")]
    [InlineData("\"><script>")]
    public void Convert_InvalidInkColor_Throws(string color)
    {
        using var png = TestImages.PngWithBlackRect(5, 5, SKRect.Empty);

        var ex = Assert.Throws<ArgumentException>(
            () => new SvgConverter().Convert(png, new SvgConversionOptions { FillColor = color }));
        Assert.Equal("FillColor", ex.ParamName);
    }

    [Fact]
    public void Convert_InvalidThreshold_Throws()
    {
        using var png = TestImages.PngWithBlackRect(5, 5, SKRect.Empty);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SvgConverter().Convert(png, new SvgConversionOptions { Threshold = 300 }));
    }
}
