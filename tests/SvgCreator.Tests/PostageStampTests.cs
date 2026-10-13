using System.Globalization;
using System.Xml.Linq;
using SvgCreator.Svg;

namespace SvgCreator.Tests;

public class PostageStampTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    // A fixed 60×40 solid PNG. Built once rather than encoded at test time, so its bytes (and the
    // golden files that embed them) don't depend on the encoder version.
    private const string PictureBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAADwAAAAoCAIAAAAt2Q6oAAAAO0lEQVR42u3OQQkAAAgEsItjHGMbyxQ+hMECLNXzTqSlpaWlpaWlpaWlpaWlpaWlpaWlpaWlpaWlpS8t1h+4pujAzIsAAAAASUVORK5CYII=";

    private static readonly PostageStampOptions Singapore = new() { Country = "SINGAPORE", Fee = "$1.20", Caption = "Lion City", InkColor = "#7a1f1f" };

    private static string Create(PostageStampOptions options)
    {
        using var png = new MemoryStream(System.Convert.FromBase64String(PictureBase64));
        return new PostageStampGenerator().Create(png, options);
    }

    private static double Number(XElement e, string attribute) => double.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);

    private static XElement Text(XElement root, string className) =>
        root.Descendants(Svg + "text").Single(t => (string?)t.Attribute("class") == className);

    [Fact]
    public void Golden() => SvgGoldenTests.AssertGolden("postage-stamp.svg", Create(Singapore));

    [Fact]
    public void Create_EmbedsTheOriginalImageUnchanged()
    {
        var image = XDocument.Parse(Create(Singapore)).Descendants(Svg + "image").Single();

        Assert.Equal("data:image/png;base64," + PictureBase64, (string?)image.Attribute("href"));
        Assert.Equal((60.0, 40.0), (Number(image, "width"), Number(image, "height")));
    }

    // A 60×30 AVIF (encoded once with macOS sips).
    private const string AvifBase64 =
        "AAAAIGZ0eXBhdmlmAAAAAE1pUHJhdmlmbWlhZm1pZjEAAAEhbWV0YQAAAAAAAAAhaGRscgAAAAAAAAAAcGljdAAAAAAAAAAA" +
        "AAAAAAAAAAAkZGluZgAAABxkcmVmAAAAAAAAAAEAAAAMdXJsIAAAAAEAAAAOcGl0bQAAAAAAAQAAACNpaW5mAAAAAAABAAAA" +
        "FWluZmUCAAAAAAEAAGF2MDEAAAAAgWlwcnAAAABgaXBjbwAAABNjb2xybmNseAACAAIABoAAAAAMY2xsaQDLAEAAAAAUaXNw" +
        "ZQAAAAAAAAA8AAAAHgAAAAlpcm90AAAAABBwaXhpAAAAAAMICAgAAAAMYXYxQ4EADAAAAAAZaXBtYQAAAAAAAAABAAEGgQID" +
        "BYaEAAAAHmlsb2MAAAAARAAAAQABAAAAAQAAAVEAAABGAAAAAW1kYXQAAAAAAAAAVhIACg0AAAACp30//gQICBoQMjMUAGSA" +
        "AgggiOn4goLIXPbMzz/Uny8pWtfx8xtSoyZe6nPv+CoJujewxvlcIkpCCR02jsA=";

    [Fact]
    public void Create_Avif_IsKeptAsAvifOrRejected_NeverReencoded()
    {
        // Browsers show AVIF, so its bytes must be kept, never swapped for a larger PNG. The stock
        // SkiaSharp builds can't decode AVIF at all, so there it is rejected as unsupported.
        using var avif = new MemoryStream(System.Convert.FromBase64String(AvifBase64));

        string svg;
        try
        {
            svg = new PostageStampGenerator().Create(avif, Singapore);
        }
        catch (InvalidDataException ex)
        {
            Assert.StartsWith("AVIF images aren't supported", ex.Message, StringComparison.Ordinal);
            return;
        }

        var href = (string)XDocument.Parse(svg).Descendants(Svg + "image").Single().Attribute("href")!;
        Assert.Equal("data:image/avif;base64," + AvifBase64, href);
    }

    [Fact]
    public async Task CreateAsync_MatchesCreate()
    {
        using var png = new MemoryStream(System.Convert.FromBase64String(PictureBase64));

        Assert.Equal(Create(Singapore), await new PostageStampGenerator().CreateAsync(png, Singapore, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Create_PlacesTextAtItsCorners()
    {
        var root = XDocument.Parse(Create(Singapore with { CountryCorner = StampCorner.BottomRight, CaptionCorner = StampCorner.TopLeft })).Root!;
        var image = root.Descendants(Svg + "image").Single();
        var (top, bottom) = (Number(image, "y"), Number(image, "y") + Number(image, "height"));
        var (left, right) = (Number(image, "x"), Number(image, "x") + Number(image, "width"));

        var country = Text(root, "stamp-country");
        Assert.True(Number(country, "y") > bottom);
        Assert.Equal(right, Number(country, "x"));
        Assert.Equal("end", (string?)country.Attribute("text-anchor"));

        var caption = Text(root, "stamp-caption");
        Assert.True(Number(caption, "y") < top);
        Assert.Equal(left, Number(caption, "x"));
        Assert.Null(caption.Attribute("text-anchor"));

        Assert.True(Number(Text(root, "stamp-fee"), "y") < top); // default TopRight
    }

    [Fact]
    public void Create_WithoutFeeOrCaption_LeavesNoRowBelowThePicture()
    {
        var root = XDocument.Parse(Create(new PostageStampOptions { Country = "JAPAN" })).Root!;
        var image = root.Descendants(Svg + "image").Single();

        Assert.Single(root.Descendants(Svg + "text"));
        var paperBottom = Number(root, "height");
        var pictureBottom = Number(image, "y") + Number(image, "height");
        Assert.True(paperBottom - pictureBottom < 0.12 * 40); // just the margin
    }

    [Fact]
    public void Create_LongText_ShrinksTheRowToFitThePicture()
    {
        var root = XDocument.Parse(Create(Singapore with { Country = "UNITED KINGDOM OF GREAT BRITAIN" })).Root!;
        var normal = XDocument.Parse(Create(Singapore)).Root!;

        Assert.True(Number(Text(root, "stamp-country"), "font-size") < Number(Text(normal, "stamp-country"), "font-size"));
        Assert.Equal(Number(Text(root, "stamp-country"), "font-size") / Number(Text(normal, "stamp-country"), "font-size"),
            Number(Text(root, "stamp-fee"), "font-size") / Number(Text(normal, "stamp-fee"), "font-size"), 1); // shrunk together
    }

    [Fact]
    public void FitRow_ShrinksOnlyTheTextSoTheRowWithItsFixedGapFits()
    {
        // The 60×40 picture with a long country and the fee: about 61 px if the gap were scaled too.
        const double Unit = 40, Available = 60;
        List<PostageStampWriter.Item> row =
        [
            new("stamp-country", "UNITED KINGDOM OF GREAT BRITAIN", StampCorner.TopLeft, 0.12 * Unit, Bold: true, Italic: false),
            new("stamp-fee", "$1.20", StampCorner.TopRight, 0.11 * Unit, Bold: true, Italic: false),
        ];
        Assert.True(PostageStampWriter.RowWidth(row, Unit) > Available);

        var fitted = PostageStampWriter.FitRow(row, Available, Unit);

        Assert.Equal(Available, PostageStampWriter.RowWidth(fitted, Unit), 9);
        Assert.Equal(row[0].Size / row[1].Size, fitted[0].Size / fitted[1].Size, 9); // shrunk together
    }

    private static PostageStampWriter.Item Country(string text, double size = 10) =>
        new("stamp-country", text, StampCorner.TopLeft, size, Bold: true, Italic: false);

    [Fact]
    public void RowWidth_CountsFullWidthGlyphsAsAFullEm()
    {
        // Five CJK glyphs at size 10 are about 50 px wide. At 0.74 em (37 px) a 40 px picture
        // would have been judged wide enough, and the text would overrun it.
        List<PostageStampWriter.Item> row = [Country("日本郵便局")];

        Assert.Equal(50, PostageStampWriter.RowWidth(row, 40), 9);
        Assert.Equal(40, PostageStampWriter.RowWidth(PostageStampWriter.FitRow(row, 40, 40), 40), 9);
    }

    [Theory]
    [InlineData("ÖSTERREICH", "OSTERREICH")] // accented capitals: as wide as their base letters
    [InlineData("españa", "espana")]         // accented lowercase
    [InlineData("Ce\u0301te", "Cete")]      // a combining accent adds no width
    public void RowWidth_MeasuresAccentedLatinLikeAscii(string accented, string plain) =>
        Assert.Equal(PostageStampWriter.RowWidth([Country(plain)], 40), PostageStampWriter.RowWidth([Country(accented)], 40), 9);

    [Theory]
    [InlineData("ΕΛΛΑΣ")]  // Greek capitals: unmeasured script
    [InlineData("россия")] // Cyrillic lowercase: char.IsLower once counted these at 0.54 em
    [InlineData("🇸🇬")]     // a flag: two runes (four UTF-16 units), each a full em
    public void RowWidth_AssumesAFullEmForUnmeasuredScripts(string text) =>
        Assert.Equal(text.EnumerateRunes().Count() * 10.0, PostageStampWriter.RowWidth([Country(text)], 40), 9);

    [Fact]
    public void FitRow_LeavesARowThatFitsUnchanged()
    {
        List<PostageStampWriter.Item> row = [new("stamp-country", "JAPAN", StampCorner.TopLeft, 4.8, Bold: true, Italic: false)];

        Assert.Same(row, PostageStampWriter.FitRow(row, 60, 40));
    }

    [Theory]
    [InlineData(12, 8)]   // paper edge 0.024 px and frame 0.048 px: both rounded to 0 at tenths
    [InlineData(200, 16)] // just under the 16.7 px where the paper edge reached 0.05
    public void Create_SmallPicture_KeepsProportionalStrokeWidths(int width, int height)
    {
        using var png = TestImages.PngWithBlackRect(width, height, new SkiaSharp.SKRect(1, 1, 3, 3));
        var root = XDocument.Parse(new PostageStampGenerator().Create(png, Singapore)).Root!;
        double unit = Math.Min(width, height);

        var paper = root.Descendants(Svg + "path").Single(p => (string?)p.Attribute("class") == "stamp-paper");
        var frame = root.Descendants(Svg + "rect").Single();
        Assert.Equal(0.003 * unit, Number(paper, "stroke-width"), 4);
        Assert.Equal(0.006 * unit, Number(frame, "stroke-width"), 4);
    }

    [Theory]
    [InlineData(12, 8)]
    [InlineData(16, 12)]
    [InlineData(60, 40)]
    public void Create_FrameStrokeHugsThePictureEdges(int width, int height)
    {
        using var png = TestImages.PngWithBlackRect(width, height, new SkiaSharp.SKRect(1, 1, 3, 3));
        var root = XDocument.Parse(new PostageStampGenerator().Create(png, Singapore)).Root!;
        var image = root.Descendants(Svg + "image").Single();
        var frame = root.Descendants(Svg + "rect").Single();
        var half = Number(frame, "stroke-width") / 2;

        // The stroke's inner edge sits on the picture's edge on every side.
        Assert.Equal(Number(image, "x"), Number(frame, "x") + half, 3);
        Assert.Equal(Number(image, "y"), Number(frame, "y") + half, 3);
        Assert.Equal(Number(image, "x") + Number(image, "width"), Number(frame, "x") + Number(frame, "width") - half, 3);
        Assert.Equal(Number(image, "y") + Number(image, "height"), Number(frame, "y") + Number(frame, "height") - half, 3);
    }

    [Fact]
    public void Create_FittedRowStillFitsAfterFontSizesAreWritten()
    {
        // 16×12 picture: the long country row is shrunk to exactly fill 16 px. At tenths each font
        // size moves by up to 0.05: here the row came out 15.2 px, and sizes that round up overflow.
        using var png = TestImages.PngWithBlackRect(16, 12, new SkiaSharp.SKRect(1, 1, 3, 3));
        var root = XDocument.Parse(new PostageStampGenerator().Create(png, Singapore with { Country = "UNITED KINGDOM OF GREAT BRITAIN" })).Root!;

        List<PostageStampWriter.Item> written =
        [
            new("stamp-country", "UNITED KINGDOM OF GREAT BRITAIN", StampCorner.TopLeft, Number(Text(root, "stamp-country"), "font-size"), Bold: true, Italic: false),
            new("stamp-fee", "$1.20", StampCorner.TopRight, Number(Text(root, "stamp-fee"), "font-size"), Bold: true, Italic: false),
        ];
        Assert.InRange(PostageStampWriter.RowWidth(written, 12), 15.99, 16.001);
    }

    [Fact]
    public void Create_EscapesText()
    {
        var svg = Create(new PostageStampOptions { Country = "<script>", Caption = "\"&'" });

        Assert.DoesNotContain("<script>", svg, StringComparison.Ordinal);
        Assert.Equal("<script>", Text(XDocument.Parse(svg).Root!, "stamp-country").Value);
    }

    [Fact]
    public void Create_IsAlwaysCleanArtworkForLaterComposition()
    {
        var root = XDocument.Parse(Create(Singapore)).Root!;

        Assert.Empty(root.Descendants(Svg + "filter"));
        Assert.DoesNotContain(root.Descendants(), e => e.Attribute("filter") is not null);
        Assert.DoesNotContain(root.Descendants(Svg + "g"), g => (string?)g.Attribute("class") == "postmark-cancel");
    }

    [Fact]
    public void Create_UsesInkColorForTextAndFrame()
    {
        var root = XDocument.Parse(Create(Singapore with { InkColor = "navy" })).Root!;
        var print = root.Descendants(Svg + "g").Single(g => (string?)g.Attribute("class") == "stamp-print");

        Assert.Equal("navy", (string?)print.Attribute("fill"));
        Assert.Equal("navy", (string?)print.Element(Svg + "rect")!.Attribute("stroke"));
    }

    [Theory]
    [InlineData(12, 8)]   // inset 0.024 px: rounded to 0 at tenths, clipping half the edge stroke
    [InlineData(16, 12)]
    [InlineData(60, 40)]
    public void Create_PaperEdgeStrokeStaysInsideTheCanvas(int width, int height)
    {
        using var png = TestImages.PngWithBlackRect(width, height, new SkiaSharp.SKRect(1, 1, 3, 3));
        var root = XDocument.Parse(new PostageStampGenerator().Create(png, Singapore)).Root!;
        var paper = root.Descendants(Svg + "path").Single(p => (string?)p.Attribute("class") == "stamp-paper");
        var half = Number(paper, "stroke-width") / 2;

        // Absolute commands: M/L take x y; A takes rx ry rotation large-arc sweep x y. The bites
        // curve inward, so the end points bound the outline.
        var tokens = System.Text.RegularExpressions.Regex.Matches((string)paper.Attribute("d")!, @"[MLAZ]|-?[\d.]+")
            .Select(m => m.Value).ToList();
        var points = new List<(double X, double Y)>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var skip = tokens[i] switch { "M" or "L" => 0, "A" => 5, _ => -1 };
            if (skip < 0)
            {
                continue;
            }

            i += skip;
            points.Add((double.Parse(tokens[i + 1], CultureInfo.InvariantCulture), double.Parse(tokens[i + 2], CultureInfo.InvariantCulture)));
            i += 2;
        }

        Assert.True(points.Min(p => p.X) >= half && points.Min(p => p.Y) >= half, "left/top edge stroke is clipped");
        Assert.True(points.Max(p => p.X) + half <= Number(root, "width"), "right edge stroke is clipped");
        Assert.True(points.Max(p => p.Y) + half <= Number(root, "height"), "bottom edge stroke is clipped");
    }

    [Theory]
    [InlineData(1_000_000)] // a 1 × 1,000,000 px picture: 20 million holes uncapped
    [InlineData(1e12)]      // length / pitch beyond int.MaxValue must not overflow the count
    public void PerforatedEdge_CapsHolesPerSideForElongatedPictures(double length)
    {
        var d = PostageStampWriter.PerforatedEdge(0, length, 1, 0.05);

        Assert.Equal(2 * (200 + 20), d.Count(c => c == 'A')); // 200 along each long side, 1 / 0.05 down
        Assert.True(d.Length < 100_000);
    }

    [Fact]
    public void PerforatedEdge_HasEvenlySpacedHolesOnEverySide()
    {
        // 100×60 with pitch 10: 10 holes across and 6 down, on each of the two sides.
        var d = PostageStampWriter.PerforatedEdge(0, 100, 60, 10);

        Assert.StartsWith("M0 0L2 0A3 3 0 0 0 8 0", d, StringComparison.Ordinal); // first hole centred at 5, radius 3: 2 to 8
        Assert.Equal(2 * (10 + 6), d.Count(c => c == 'A'));
        Assert.EndsWith("Z", d, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StampCorner.TopLeft, null)]   // fee and country share a corner
    [InlineData(StampCorner.TopRight, "")]    // empty caption
    public void Validate_RejectsCollidingOrEmptyItems(StampCorner feeCorner, string? caption)
    {
        var layout = new PostageStampOptions { Country = "SINGAPORE", Fee = "$1", FeeCorner = feeCorner, Caption = caption };

        Assert.ThrowsAny<ArgumentException>(() => Create(layout));
    }

    [Fact]
    public void Validate_AllowsSharedCornerForAnAbsentItem()
    {
        // The fee isn't shown, so its corner doesn't matter.
        var layout = new PostageStampOptions { Country = "SINGAPORE", FeeCorner = StampCorner.TopLeft };

        XDocument.Parse(Create(layout));
    }

    [Theory]
    [InlineData("not a colour")]
    [InlineData("notacolour")] // looks like a CSS name, but isn't one: a renderer would drop it silently
    public void Validate_RejectsInvalidInkColor(string color) =>
        Assert.Throws<ArgumentException>(() => Create(Singapore with { InkColor = color }));

    [Fact]
    public void Validate_RejectsBlankCountry() =>
        Assert.ThrowsAny<ArgumentException>(() => Create(new PostageStampOptions { Country = " " }));
}
