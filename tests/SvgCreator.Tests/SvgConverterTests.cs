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

    private static MemoryStream LightDiscOnDarkBackground()
    {
        using var bitmap = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.Clear(new SKColor(30, 50, 200));
            canvas.DrawCircle(32, 32, 20, paint);
        }

        var png = new MemoryStream();
        bitmap.Encode(png, SKEncodedImageFormat.Png, 100);
        png.Position = 0;
        return png;
    }

    [Fact]
    public void Convert_LinesMode_OutlinesCircleAsNativeStrokedCircle()
    {
        // Lines mode outlines the disc with a thin ring instead of filling the dark background,
        // and the ring is recognized as one stroked <circle>.
        using var png = LightDiscOnDarkBackground();

        var g = XDocument.Parse(new SvgConverter().Convert(png)).Descendants(Svg + "g").Single();

        Assert.Empty(g.Elements(Svg + "path"));
        var circle = Assert.Single(g.Elements(Svg + "circle"));
        Assert.Equal("none", (string?)circle.Attribute("fill"));
        // The line sits on the darker side, i.e. just outside the radius-20 disc.
        Assert.InRange(double.Parse((string)circle.Attribute("r")!, System.Globalization.CultureInfo.InvariantCulture), 20, 25);
        Assert.InRange(double.Parse((string)circle.Attribute("cx")!, System.Globalization.CultureInfo.InvariantCulture), 31, 33);
    }

    [Fact]
    public void Convert_EllipseDetectionOff_KeepsCurvesInPath()
    {
        using var png = LightDiscOnDarkBackground();

        var svg = new SvgConverter().Convert(png, new SvgConversionOptions { DetectEllipses = false });

        var d = (string)Assert.Single(XDocument.Parse(svg).Descendants(Svg + "path")).Attribute("d")!;
        Assert.DoesNotContain("M0 0", d, StringComparison.Ordinal); // no full-canvas background shape
        Assert.Equal(2, d.Count(c => c == 'M')); // a ring: outer border + hole
        Assert.Empty(XDocument.Parse(svg).Descendants(Svg + "circle"));
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
    [InlineData("RebeccaPurple")]
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
    [InlineData("notacolour")]
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
    public void Convert_StampStyle_FiltersInkWhileDefaultHasNoFilter()
    {
        using var png1 = TestImages.PngWithBlackRect(20, 10, new SKRect(2, 3, 8, 7));
        using var png2 = TestImages.PngWithBlackRect(20, 10, new SKRect(2, 3, 8, 7));

        var clean = XDocument.Parse(new SvgConverter().Convert(png1));
        var stamp = XDocument.Parse(new SvgConverter().Convert(png2, new SvgConversionOptions { Style = OutputStyle.Stamp }));

        Assert.Empty(clean.Descendants(Svg + "filter"));
        Assert.Equal("url(#svgcreator-stamp-1)", (string?)stamp.Descendants(Svg + "g").Single().Attribute("filter"));
    }

    [Fact]
    public void Convert_PostmarkStyle_AddsCancellationAndWidensCanvas()
    {
        using var png = TestImages.PngWithBlackRect(40, 20, new SKRect(5, 5, 15, 15));

        var doc = XDocument.Parse(new SvgConverter().Convert(png, new SvgConversionOptions { Style = OutputStyle.Postmark }));

        Assert.Single(doc.Descendants(Svg + "g"), g => (string?)g.Attribute("class") == "postmark-cancel");
        Assert.True(int.Parse((string)doc.Root!.Attribute("width")!, System.Globalization.CultureInfo.InvariantCulture) > 40);
    }

    [Fact]
    public void Convert_InvalidStyleOrSeed_Throws()
    {
        using var png = TestImages.PngWithBlackRect(5, 5, SKRect.Empty);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SvgConverter().Convert(png, new SvgConversionOptions { Style = (OutputStyle)42 }));
        png.Position = 0;
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SvgConverter().Convert(png, new SvgConversionOptions { StampSeed = -1 }));
    }

    [Fact]
    public void Convert_InvalidThreshold_Throws()
    {
        using var png = TestImages.PngWithBlackRect(5, 5, SKRect.Empty);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SvgConverter().Convert(png, new SvgConversionOptions { Threshold = 300 }));
    }
}
