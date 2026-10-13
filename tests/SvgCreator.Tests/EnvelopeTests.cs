using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SvgCreator.Fitting;
using SvgCreator.Svg;

namespace SvgCreator.Tests;

public partial class EnvelopeTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static readonly EnvelopeOptions Small = new() { Width = 220, Height = 110 };

    private static XElement Create(EnvelopeOptions options) => XDocument.Parse(new EnvelopeGenerator().Create(options)).Root!;

    private static double Number(XElement e, string attribute) => double.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"M([^Z]*)Z")]
    private static partial Regex Polygons();

    // Absolute M/L path data → its polygons.
    private static List<List<PointD>> ParsePolygons(string d) =>
        [.. Polygons().Matches(d).Select(m => m.Groups[1].Value.Split('L')
            .Select(p => p.Split(' '))
            .Select(xy => new PointD(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture)))
            .ToList())];

    [Fact]
    public void Golden_Flat() => SvgGoldenTests.AssertGolden("envelope-flat.svg", new EnvelopeGenerator().Create(Small));

    [Fact]
    public void Golden_Crumpled() =>
        SvgGoldenTests.AssertGolden("envelope-crumpled.svg", new EnvelopeGenerator().Create(Small with { Style = EnvelopeStyle.Crumpled, Seed = 3 }));

    [Fact]
    public void Create_CanvasIsTheEnvelopeSize()
    {
        var root = Create(Small);

        Assert.Equal((220.0, 110.0), (Number(root, "width"), Number(root, "height")));
        Assert.Equal("0 0 220 110", (string?)root.Attribute("viewBox"));
    }

    [Theory]
    [InlineData(EnvelopeStyle.Flat)]
    [InlineData(EnvelopeStyle.Crumpled)]
    public void Create_StrapsStayInTheirBorderOnThePaper(EnvelopeStyle style)
    {
        var root = Create(new EnvelopeOptions { Style = style });
        var paper = root.Descendants(Svg + "rect").First(r => (string?)r.Attribute("class") == "envelope-paper");
        var (x, y, w, h) = (Number(paper, "x"), Number(paper, "y"), Number(paper, "width"), Number(paper, "height"));
        var depth = 0.07 * 550;

        foreach (var className in new[] { "envelope-straps", "envelope-straps-alternate" })
        {
            var path = root.Descendants(Svg + "path").Single(p => (string?)p.Attribute("class") == className);
            var polygons = ParsePolygons((string)path.Attribute("d")!);
            Assert.NotEmpty(polygons);
            foreach (var p in polygons.SelectMany(polygon => polygon))
            {
                const double Eps = 1e-3;
                Assert.InRange(p.X, x - Eps, x + w + Eps);
                Assert.InRange(p.Y, y - Eps, y + h + Eps);
                var toEdge = Math.Min(Math.Min(p.X - x, x + w - p.X), Math.Min(p.Y - y, y + h - p.Y));
                Assert.True(toEdge <= depth + Eps, $"({p.X}, {p.Y}) is {toEdge} from the edge, deeper than the border");
            }
        }
    }

    [Fact]
    public void Create_StrapColoursAreTheOptions()
    {
        var root = Create(Small with { StrapColor = "green", AlternateStrapColor = "#ffcc00", PaperColor = "ivory" });

        Assert.Equal("green", (string?)root.Descendants(Svg + "path").Single(p => (string?)p.Attribute("class") == "envelope-straps").Attribute("fill"));
        Assert.Equal("#ffcc00", (string?)root.Descendants(Svg + "path").Single(p => (string?)p.Attribute("class") == "envelope-straps-alternate").Attribute("fill"));
        Assert.Equal("ivory", (string?)root.Descendants(Svg + "rect").First(r => (string?)r.Attribute("class") == "envelope-paper").Attribute("fill"));
    }

    [Fact]
    public void Straps_CapsRepeatsPerSideForElongatedEnvelopes()
    {
        // 1 × 1,000,000: the strap width follows the shorter side, so uncapped the long sides
        // would get millions of straps.
        var (straps, alternates) = EnvelopeWriter.Straps(0, 0, 1_000_000, 1, 1);

        Assert.True(straps.Count(c => c == 'M') + alternates.Count(c => c == 'M') <= 4 * 2 * (250 + 2));
    }

    [Fact]
    public void Straps_EachSideStartsTheSameWay()
    {
        // On a square, every side is the same length, so each side's straps are the top side's
        // turned by 90°: the same number of strap and alternate polygons per side.
        var (straps, alternates) = EnvelopeWriter.Straps(0, 0, 100, 100, 100);

        Assert.Equal(0, straps.Count(c => c == 'M') % 4);
        Assert.Equal(0, alternates.Count(c => c == 'M') % 4);
    }

    [Fact]
    public void ClipConvex_CutsASubjectToTheClipShape()
    {
        // A 4×4 square cut by the triangle (0,0) (4,0) (0,4), clockwise on screen.
        PointD[] square = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)];
        PointD[] triangle = [new(0, 0), new(4, 0), new(0, 4)];

        var clipped = EnvelopeWriter.ClipConvex(square, triangle);

        var area = 0.0;
        for (var i = 0; i < clipped.Count; i++)
        {
            var (a, b) = (clipped[i], clipped[(i + 1) % clipped.Count]);
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        Assert.Equal(8, Math.Abs(area) / 2, 9);
    }

    [Fact]
    public void Create_Flat_HasNoIdsSoItComposesFreely()
    {
        var root = Create(Small);

        Assert.DoesNotContain(root.DescendantsAndSelf(), e => e.Attribute("id") is not null);
        Assert.Empty(root.Descendants(Svg + "filter"));
    }

    [Fact]
    public void Create_Crumpled_IdsNameWhatTheirDefinitionsDependOn()
    {
        var ids = Create(Small with { Style = EnvelopeStyle.Crumpled, Seed = 3 })
            .Descendants().Select(e => (string?)e.Attribute("id")).OfType<string>().ToList();

        // Size and seed shape these, so they're in the id; the grain and crease gradient never change.
        Assert.Equal(
            ["svgcreator-envelope-220x110-3-edge", "svgcreator-envelope-grain", "svgcreator-envelope-crease", "svgcreator-envelope-220x110-paper"],
            ids);
    }

    [Fact]
    public void Create_Crumpled_SameSeedSameEnvelope_DifferentSeedDifferentCreases()
    {
        var options = new EnvelopeOptions { Style = EnvelopeStyle.Crumpled, Seed = 7 };
        var generator = new EnvelopeGenerator();

        Assert.Equal(generator.Create(options), generator.Create(options));
        Assert.NotEqual(generator.Create(options), generator.Create(options with { Seed = 8 }));
    }

    [Fact]
    public void Create_Crumpled_CreasesAreARemovableGroup()
    {
        var root = Create(Small with { Style = EnvelopeStyle.Crumpled });
        var creases = root.Descendants(Svg + "g").Single(g => (string?)g.Attribute("class") == "envelope-creases");

        Assert.Equal(12, creases.Elements(Svg + "g").Count());
        Assert.Equal("url(#svgcreator-envelope-220x110-paper)", (string?)creases.Attribute("clip-path"));
    }

    [Theory]
    [InlineData(0, 100, 1, "#fff")]
    [InlineData(100, -1, 1, "#fff")]
    [InlineData(100, 100, -1, "#fff")]
    [InlineData(100, 100, 1, "notacolour")]
    public void Validate_RejectsInvalidOptions(int width, int height, int seed, string paper) =>
        Assert.ThrowsAny<ArgumentException>(() => new EnvelopeGenerator().Create(new EnvelopeOptions { Width = width, Height = height, Seed = seed, PaperColor = paper }));

    [Fact]
    public void Validate_RejectsUnknownStyle() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnvelopeGenerator().Create(Small with { Style = (EnvelopeStyle)42 }));
}
