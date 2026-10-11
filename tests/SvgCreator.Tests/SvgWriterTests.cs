using System.Xml.Linq;
using SvgCreator.Svg;
using SvgCreator.Fitting;

namespace SvgCreator.Tests;

public class SvgWriterTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Fact]
    public void Write_ProducesValidSvgWithEvenOddFillAndViewBox()
    {
        CurveContour[] contours =
        [
            Polygon(1, false, new(0, 0), new(4, 0), new(4, 4), new(0, 4)),
            Polygon(1, true, new(1, 1), new(1, 3), new(3, 3), new(3, 1)),
            Polygon(2, false, new(5, 5), new(6, 5), new(6, 6)),
        ];

        var doc = XDocument.Parse(SvgWriter.Write(10, 8, contours, "#000"));

        var root = doc.Root!;
        Assert.Equal(Svg + "svg", root.Name);
        Assert.Equal("0 0 10 8", (string?)root.Attribute("viewBox"));

        var group = Assert.Single(root.Elements(Svg + "g"));
        Assert.Equal("evenodd", (string?)group.Attribute("fill-rule"));
        Assert.Equal("none", (string?)group.Attribute("stroke"));

        var paths = group.Elements(Svg + "path").ToList();
        Assert.Equal(2, paths.Count); // one per component
        Assert.Equal("M0 0l4 0l0 4l-4 0ZM1 1l0 2l2 0l0-2Z", (string?)paths[0].Attribute("d"));
    }

    [Fact]
    public void Write_CurvedSegments_UseCompactRelativeCubicCommands()
    {
        CurveContour[] contours =
        [
            new(1, false,
            [
                new CubicBezier(new(2.04, 1), new(3.26, 5.68), new(11.99, 6), new(12, 1.5)),
                new CubicBezier(new(12, 1.5), new(12, -2), new(2.5, -2), new(2.04, 1)),
            ]),
        ];

        var d = (string?)XDocument.Parse(SvgWriter.Write(20, 10, contours, "#000")).Descendants(Svg + "path").Single().Attribute("d");

        // Relative to the previous rounded end point; tenths precision; minimal separators.
        Assert.Equal("M2 1c1.3 4.7 10 5 10 .5c0-3.5-9.5-3.5-10-.5Z", d);
    }

    [Fact]
    public void Write_SolidEllipseComponent_UsesNativeCircleOrEllipse()
    {
        CurveContour[] contours =
        [
            new(1, false, [], new Ellipse(new(20, 15), 10.04, 9.98, 0)),
            new(2, false, [], new Ellipse(new(50, 40), 20, 8, 30)),
        ];

        var g = XDocument.Parse(SvgWriter.Write(80, 60, contours, "#000")).Descendants(Svg + "g").Single();

        var circle = Assert.Single(g.Elements(Svg + "circle"));
        Assert.Equal(("20", "15", "10"), ((string?)circle.Attribute("cx"), (string?)circle.Attribute("cy"), (string?)circle.Attribute("r")));
        var ellipse = Assert.Single(g.Elements(Svg + "ellipse"));
        Assert.Equal("rotate(30 50 40)", (string?)ellipse.Attribute("transform"));
        Assert.Empty(g.Elements(Svg + "path"));
    }

    [Fact]
    public void Write_ConcentricRing_UsesOneStrokedCircle()
    {
        CurveContour[] contours =
        [
            new(1, false, [], new Ellipse(new(50, 50), 40, 40, 0)),
            new(1, true, [], new Ellipse(new(50.2, 49.9), 37, 37, 0)),
        ];

        var circle = XDocument.Parse(SvgWriter.Write(100, 100, contours, "navy")).Descendants(Svg + "circle").Single();

        Assert.Equal("38.5", (string?)circle.Attribute("r"));
        Assert.Equal("none", (string?)circle.Attribute("fill"));
        Assert.Equal("navy", (string?)circle.Attribute("stroke"));
        Assert.Equal("3", (string?)circle.Attribute("stroke-width"));
    }

    [Fact]
    public void Write_EllipseWithIrregularHole_UsesNativeArcsInsidePath()
    {
        CurveContour[] contours =
        [
            new(1, false, [], new Ellipse(new(50, 50), 40, 30, 0)),
            Polygon(1, true, new(45, 45), new(45, 55), new(55, 55)),
        ];

        var d = (string?)XDocument.Parse(SvgWriter.Write(100, 100, contours, "#000")).Descendants(Svg + "path").Single().Attribute("d");

        Assert.Equal("M90 50a40 30 0 0 1-80 0a40 30 0 0 1 80 0ZM45 45l0 10l10 0Z", d);
    }

    [Fact]
    public void Write_EscapesFillColor()
    {
        var svg = SvgWriter.Write(1, 1, [], "\"><script>");

        Assert.DoesNotContain("<script>", svg, StringComparison.Ordinal);
        XDocument.Parse(svg);
    }

    private static CurveContour Polygon(int label, bool isHole, params PointD[] points) =>
        new(label, isHole, [.. points.Select((p, i) => CubicBezier.Line(p, points[(i + 1) % points.Length]))]);
}
