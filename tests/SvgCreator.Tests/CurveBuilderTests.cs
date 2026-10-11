using SvgCreator.Fitting;
using SvgCreator.Imaging;
using SvgCreator.Tracing;

namespace SvgCreator.Tests;

public class CurveBuilderTests
{
    private static CurveContour Build(BinaryImage mask, double tolerance = 1.0, double cornerAngle = 135) =>
        Assert.Single(CurveBuilder.Build(Trace(mask), tolerance, cornerAngle));

    private static IReadOnlyList<Contour> Trace(BinaryImage mask) =>
        ContourTracer.Trace(ComponentLabeler.Label(mask, minArea: 0));

    // Largest distance from a pixel-outline vertex to the fitted curve.
    private static double MaxDeviation(Contour pixels, CurveContour curve)
    {
        var samples = Sample(curve).ToList();
        return pixels.Points.Max(p => samples.Min(s => (s - new PointD(p.X, p.Y)).Length));
    }

    private static IEnumerable<PointD> Sample(CurveContour contour) =>
        contour.Segments.SelectMany(s => Enumerable.Range(0, 21).Select(i => s.Evaluate(i / 20.0)));

    [Fact]
    public void Build_Square_KeepsFourExactCornersAndStraightSides()
    {
        var mask = TestImages.Mask(
            "............",
            ".##########.",
            ".##########.",
            ".##########.",
            ".##########.",
            ".##########.",
            ".##########.",
            ".##########.",
            ".##########.",
            "............");

        var curve = Build(mask);

        Assert.Equal(4, curve.Segments.Count);
        Assert.All(curve.Segments, s => Assert.True(s.IsNearlyLine(0.1)));
        Assert.Equal(
            new HashSet<PointD> { new(1, 1), new(11, 1), new(11, 9), new(1, 9) },
            curve.Segments.Select(s => s.P0).ToHashSet());
    }

    [Fact]
    public void Build_Disc_FitsFewSmoothCurvesCloseToTheCircle()
    {
        const int size = 64;
        const double radius = 24;

        var curve = Build(TestImages.Disc(size, radius));

        Assert.InRange(curve.Segments.Count, 2, 12);
        Assert.All(curve.Segments, s => Assert.False(s.IsNearlyLine(0.1)));
        var center = new PointD(size / 2.0, size / 2.0);
        Assert.All(Sample(curve), p => Assert.InRange((p - center).Length, radius - 1.5, radius + 1.5));

        // Smooth joins: consecutive segments share a tangent direction.
        for (var i = 0; i < curve.Segments.Count; i++)
        {
            var a = curve.Segments[i];
            var b = curve.Segments[(i + 1) % curve.Segments.Count];
            var cos = (a.P3 - a.P2).Normalize().Dot((b.P1 - b.P0).Normalize());
            Assert.True(cos > 0.99, $"Join {i} is not smooth (cos={cos}).");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Build_StaircaseDiagonal_CollapsesToFewSegmentsThatFollowThePixels(int run)
    {
        var mask = TestImages.Staircase(height: 20, run);
        var pixels = Assert.Single(Trace(mask));

        var curve = Build(mask);

        // ~40 jagged pixel corners become a triangle-like handful of segments...
        Assert.True(pixels.Points.Count >= 40);
        Assert.InRange(curve.Segments.Count, 3, 5);
        // ...that still hug the pixel outline (staircase corners sit ~0.7 px off the ideal line).
        Assert.InRange(MaxDeviation(pixels, curve), 0, 1.5);
    }

    [Fact]
    public void Build_SinglePixel_KeepsExactPixelSquare()
    {
        var curve = Build(TestImages.Mask("#"));

        Assert.Equal(
            [new PointD(1, 0), new PointD(1, 1), new PointD(0, 1), new PointD(0, 0)],
            curve.Segments.Select(s => s.P0));
    }
}
