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

    private static readonly PostageStampLayout Singapore = new() { Country = "SINGAPORE", Fee = "$1.20", Caption = "Lion City" };

    private static string Convert(PostageStampLayout layout, OutputStyle style = OutputStyle.Clean)
    {
        using var png = new MemoryStream(System.Convert.FromBase64String(PictureBase64));
        return new SvgConverter().Convert(png, new SvgConversionOptions { Layout = layout, Style = style, FillColor = "#7a1f1f", StampSeed = 3 });
    }

    private static double Number(XElement e, string attribute) => double.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);

    private static XElement Text(XElement root, string className) =>
        root.Descendants(Svg + "text").Single(t => (string?)t.Attribute("class") == className);

    [Fact]
    public void Golden_Clean() => SvgGoldenTests.AssertGolden("postage-stamp.svg", Convert(Singapore));

    [Fact]
    public void Golden_Postmark() => SvgGoldenTests.AssertGolden("postage-stamp-postmark.svg", Convert(Singapore, OutputStyle.Postmark));

    [Fact]
    public void Convert_EmbedsTheOriginalImageUnchanged()
    {
        var image = XDocument.Parse(Convert(Singapore)).Descendants(Svg + "image").Single();

        Assert.Equal("data:image/png;base64," + PictureBase64, (string?)image.Attribute("href"));
        Assert.Equal((60.0, 40.0), (Number(image, "width"), Number(image, "height")));
    }

    [Fact]
    public async Task ConvertAsync_MatchesConvert()
    {
        using var png = new MemoryStream(System.Convert.FromBase64String(PictureBase64));
        var options = new SvgConversionOptions { Layout = Singapore, FillColor = "#7a1f1f", StampSeed = 3 };

        Assert.Equal(Convert(Singapore), await new SvgConverter().ConvertAsync(png, options, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Convert_PlacesTextAtItsCorners()
    {
        var root = XDocument.Parse(Convert(Singapore with { CountryCorner = StampCorner.BottomRight, CaptionCorner = StampCorner.TopLeft })).Root!;
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
    public void Convert_WithoutFeeOrCaption_LeavesNoRowBelowThePicture()
    {
        var root = XDocument.Parse(Convert(new PostageStampLayout { Country = "JAPAN" })).Root!;
        var image = root.Descendants(Svg + "image").Single();

        Assert.Single(root.Descendants(Svg + "text"));
        var paperBottom = Number(root, "height");
        var pictureBottom = Number(image, "y") + Number(image, "height");
        Assert.True(paperBottom - pictureBottom < 0.12 * 40); // just the margin
    }

    [Fact]
    public void Convert_LongText_ShrinksTheRowToFitThePicture()
    {
        var root = XDocument.Parse(Convert(Singapore with { Country = "UNITED KINGDOM OF GREAT BRITAIN" })).Root!;
        var normal = XDocument.Parse(Convert(Singapore)).Root!;

        Assert.True(Number(Text(root, "stamp-country"), "font-size") < Number(Text(normal, "stamp-country"), "font-size"));
        Assert.Equal(Number(Text(root, "stamp-country"), "font-size") / Number(Text(normal, "stamp-country"), "font-size"),
            Number(Text(root, "stamp-fee"), "font-size") / Number(Text(normal, "stamp-fee"), "font-size"), 1); // shrunk together
    }

    [Fact]
    public void Convert_EscapesText()
    {
        var svg = Convert(new PostageStampLayout { Country = "<script>", Caption = "\"&'" });

        Assert.DoesNotContain("<script>", svg, StringComparison.Ordinal);
        Assert.Equal("<script>", Text(XDocument.Parse(svg).Root!, "stamp-country").Value);
    }

    [Fact]
    public void Convert_Postmark_CancelsTheStampWithoutDrawingAFrame()
    {
        var root = XDocument.Parse(Convert(Singapore, OutputStyle.Postmark)).Root!;
        var clean = XDocument.Parse(Convert(Singapore)).Root!;

        var cancel = root.Elements(Svg + "g").Single(g => (string?)g.Attribute("class") == "postmark-cancel");
        Assert.Empty(cancel.Elements(Svg + "rect"));                     // the perforated paper is the frame
        Assert.True(Number(root, "width") > Number(clean, "width"));     // the waves run past the right edge
        Assert.Equal(Number(clean, "height"), Number(root, "height"));
        Assert.Equal("url(#svgcreator-stamp-3)", (string?)root.Descendants(Svg + "g").First(g => (string?)g.Attribute("class") == "stamp-print").Attribute("filter"));
        Assert.Null(root.Descendants(Svg + "image").Single().Attribute("filter")); // the picture is printed as is
    }

    [Fact]
    public void PerforatedEdge_HasEvenlySpacedHolesOnEverySide()
    {
        // 100×60 with pitch 10: 10 holes across and 6 down, on each of the two sides.
        var d = PostageStampWriter.PerforatedEdge(0, 100, 60, 10);

        Assert.StartsWith("M0 0l2 0a3 3 0 0 0 6 0", d, StringComparison.Ordinal); // first hole centred at 5, radius 3: 2 to 8
        Assert.Equal(2 * (10 + 6), d.Count(c => c == 'a'));
        Assert.EndsWith("Z", d, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StampCorner.TopLeft, null)]   // fee and country share a corner
    [InlineData(StampCorner.TopRight, "")]    // empty caption
    public void Validate_RejectsCollidingOrEmptyItems(StampCorner feeCorner, string? caption)
    {
        var layout = new PostageStampLayout { Country = "SINGAPORE", Fee = "$1", FeeCorner = feeCorner, Caption = caption };

        Assert.ThrowsAny<ArgumentException>(() => Convert(layout));
    }

    [Fact]
    public void Validate_AllowsSharedCornerForAnAbsentItem()
    {
        // The fee isn't shown, so its corner doesn't matter.
        var layout = new PostageStampLayout { Country = "SINGAPORE", FeeCorner = StampCorner.TopLeft };

        XDocument.Parse(Convert(layout));
    }

    [Fact]
    public void Validate_RejectsBlankCountry() =>
        Assert.ThrowsAny<ArgumentException>(() => Convert(new PostageStampLayout { Country = " " }));
}
