using SvgCreator.Imaging;

namespace SvgCreator.Tests;

public class LineExtractorTests
{
    private const int Size = 40;

    private static GrayImage Image(Func<int, int, byte> luminance)
    {
        var pixels = new byte[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                pixels[(y * Size) + x] = luminance(x, y);
            }
        }

        return new GrayImage(Size, Size, pixels);
    }

    private static BinaryImage Extract(GrayImage image) =>
        LineExtractor.Extract(image, SvgConversionOptions.Default.LineSigma, SvgConversionOptions.Default.LineContrast);

    [Fact]
    public void Extract_FlatDarkBackground_ProducesNoInk()
    {
        // The blue background of a typical avatar has luminance ~60: not a line.
        var mask = Extract(Image((_, _) => 60));

        Assert.DoesNotContain(mask.Pixels, p => p != 0);
    }

    [Fact]
    public void Extract_FaintColouredLineOnLightArea_IsInk()
    {
        // Dusty-rose hair line (luma ~131) on light pink hair (~232): too light for a global threshold.
        var mask = Extract(Image((x, _) => x is 20 or 21 ? (byte)131 : (byte)232));

        for (var y = 0; y < Size; y++)
        {
            Assert.True(mask.IsInk(20, y) && mask.IsInk(21, y), $"Line missing at row {y}.");
            Assert.False(mask.IsInk(10, y) || mask.IsInk(30, y), $"Background marked as ink at row {y}.");
        }
    }

    [Fact]
    public void Extract_SoftShadingGradient_ProducesNoInk()
    {
        var mask = Extract(Image((x, _) => (byte)(100 + (x * 3))));

        Assert.DoesNotContain(mask.Pixels, p => p != 0);
    }

    [Fact]
    public void Extract_BoundaryBetweenRegions_GivesThinLineOnDarkSideOnly()
    {
        var mask = Extract(Image((x, _) => x < 20 ? (byte)60 : (byte)230));

        Assert.True(mask.IsInk(19, 20));
        Assert.False(mask.IsInk(5, 20)); // deep inside the dark region
        Assert.False(mask.IsInk(25, 20)); // bright side
    }

    [Fact]
    public void Extract_ThickDarkLine_IsFilledNotHollow()
    {
        var mask = Extract(Image((x, _) => x is >= 17 and < 23 ? (byte)30 : (byte)240));

        for (var x = 17; x < 23; x++)
        {
            Assert.True(mask.IsInk(x, 20), $"Column {x} of the thick line is not ink.");
        }
    }

    [Fact]
    public void Extract_LineFadingAlongItsLength_StaysConnectedWithHysteresis()
    {
        // A 2 px vertical line that fades towards the bottom without disappearing, like a hair strand
        // thinning to its tip: 40 levels darker than the background at the top, 20 at the bottom.
        var image = Image((x, y) => x is 20 or 21 ? (byte)(190 + (y / 2)) : (byte)230);

        var single = LineExtractor.Extract(image, 1.0, contrast: 12);
        var hysteresis = LineExtractor.Extract(image, 1.0, contrast: 12, weakContrast: 6);

        int Rows(BinaryImage mask) => Enumerable.Range(0, Size).Count(y => mask.IsInk(20, y) || mask.IsInk(21, y));
        Assert.True(Rows(single) < Size, "Test line should fade below the strong threshold.");
        Assert.Equal(Size, Rows(hysteresis));
    }

    [Fact]
    public void Extract_IsolatedFaintMark_IsIgnoredByHysteresis()
    {
        // Faint everywhere (weak but never strong): nothing to grow from.
        var image = Image((x, _) => x is 20 or 21 ? (byte)215 : (byte)230);

        var mask = LineExtractor.Extract(image, 1.0, contrast: 12, weakContrast: 6);

        Assert.DoesNotContain(mask.Pixels, p => p != 0);
    }

    [Fact]
    public void Extract_StrongLine_IsNotThickenedByHysteresis()
    {
        var image = Image((x, _) => x is 20 or 21 ? (byte)131 : (byte)232);

        var single = LineExtractor.Extract(image, 1.0, contrast: 12);
        var hysteresis = LineExtractor.Extract(image, 1.0, contrast: 12, weakContrast: 6);

        Assert.Equal(single.Pixels, hysteresis.Pixels);
    }

    private static RgbImage Rgb(Func<int, int, (byte R, byte G, byte B)> color)
    {
        var pixels = new byte[Size * Size * 3];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b) = color(x, y);
                var i = ((y * Size) + x) * 3;
                (pixels[i], pixels[i + 1], pixels[i + 2]) = (r, g, b);
            }
        }

        return new RgbImage(Size, Size, pixels);
    }

    private static BinaryImage ExtractColor(RgbImage image, double colorContrast) =>
        LineExtractor.Extract(image.ToGray(), image.ToLabChroma(), 1.0, 12, 12, colorContrast);

    [Fact]
    public void Extract_EquallyBrightHueBoundary_IsFoundOnlyWithColorContrast()
    {
        // Pink (240,170,190) and grey (193,193,193) have the same luma (193): no luminance edge at all.
        var image = Rgb((x, _) => x < 20 ? ((byte)240, (byte)170, (byte)190) : ((byte)193, (byte)193, (byte)193));

        // The two colours are ΔE ≈ 28.5 apart.
        var luminanceOnly = ExtractColor(image, colorContrast: 0);
        var withColor = ExtractColor(image, colorContrast: 20);
        var tooStrict = ExtractColor(image, colorContrast: 35);

        Assert.DoesNotContain(luminanceOnly.Pixels, p => p != 0);
        Assert.DoesNotContain(tooStrict.Pixels, p => p != 0);
        for (var y = 0; y < Size; y++)
        {
            // A thin line just inside the more saturated (pink) side; the grey side stays clean.
            Assert.True(withColor.IsInk(18, y), $"Boundary missing at row {y}.");
            var inked = Enumerable.Range(0, Size).Where(x => withColor.IsInk(x, y)).ToList();
            Assert.All(inked, x => Assert.InRange(x, 16, 19));
        }
    }

    [Fact]
    public void Extract_ColorBoundaryAlreadyOutlined_IsNotDoubled()
    {
        // Pink hair | dark outline | blue background: the outline already marks the boundary.
        var image = Rgb((x, _) => x switch
        {
            < 18 => ((byte)240, (byte)170, (byte)190),
            < 20 => ((byte)120, (byte)60, (byte)80),
            _ => ((byte)30, (byte)50, (byte)200),
        });

        var luminanceOnly = ExtractColor(image, colorContrast: 0);
        var withColor = ExtractColor(image, colorContrast: 10);

        Assert.Contains(luminanceOnly.Pixels, p => p != 0);
        Assert.Equal(luminanceOnly.Pixels, withColor.Pixels);
    }
}
