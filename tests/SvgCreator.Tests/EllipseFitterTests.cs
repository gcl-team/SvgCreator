using SvgCreator.Fitting;
using SvgCreator.Imaging;
using SvgCreator.Tracing;

namespace SvgCreator.Tests;

public class EllipseFitterTests
{
    // The fitter's input: edge midpoints of the traced outline, as CurveBuilder produces them.
    private static IEnumerable<PointD[]> Outlines(BinaryImage mask)
    {
        foreach (var contour in ContourTracer.Trace(ComponentLabeler.Label(mask, minArea: 0)))
        {
            var dense = new List<PointI>();
            var v = contour.Points;
            for (var i = 0; i < v.Count; i++)
            {
                var (a, b) = (v[i], v[(i + 1) % v.Count]);
                int dx = Math.Sign(b.X - a.X), dy = Math.Sign(b.Y - a.Y);
                for (var p = a; p != b; p = new PointI(p.X + dx, p.Y + dy))
                {
                    dense.Add(p);
                }
            }

            yield return [.. dense.Select((p, i) => new PointD((p.X + dense[(i + 1) % dense.Count].X) / 2.0, (p.Y + dense[(i + 1) % dense.Count].Y) / 2.0))];
        }
    }

    private static bool Fit(BinaryImage mask, out Ellipse ellipse) =>
        EllipseFitter.TryFit(Assert.Single(Outlines(mask)), 1.0, out ellipse);

    [Fact]
    public void TryFit_ExactPointsOnRotatedEllipse_RecoversParameters()
    {
        var truth = new Ellipse(new PointD(50, 40), 30, 15, 30);
        var points = Enumerable.Range(0, 100).Select(i => truth.PointAt(i * 2 * Math.PI / 100)).ToArray();

        Assert.True(EllipseFitter.TryFit(points, 1.0, out var fitted));

        Assert.Equal(50, fitted.Center.X, 6);
        Assert.Equal(40, fitted.Center.Y, 6);
        Assert.Equal(30, fitted.RadiusX, 6);
        Assert.Equal(15, fitted.RadiusY, 6);
        Assert.Equal(30, fitted.AngleDegrees, 6);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(24)]
    [InlineData(60)]
    public void TryFit_PixelDisc_IsCircleWithMatchingRadius(double radius)
    {
        var size = (int)(radius * 2) + 10;

        Assert.True(Fit(TestImages.Disc(size, radius), out var e));

        Assert.True(e.IsCircle(0.5), $"rx={e.RadiusX}, ry={e.RadiusY}");
        Assert.InRange(e.RadiusX, radius - 0.5, radius + 0.5);
        Assert.InRange(e.Center.X, (size / 2.0) - 0.5, (size / 2.0) + 0.5);
    }

    [Fact]
    public void TryFit_PixelRotatedEllipse_RecoversAxesAndAngle()
    {
        Assert.True(Fit(TestImages.Ellipse(100, 40, 20, 25), out var e));

        Assert.InRange(e.RadiusX, 39.5, 40.5);
        Assert.InRange(e.RadiusY, 19.5, 20.5);
        Assert.InRange(e.AngleDegrees, 24, 26);
    }

    [Fact]
    public void TryFit_Square_IsRejected()
    {
        var mask = TestImages.Mask([.. Enumerable.Range(0, 24).Select(y => y is 0 or 23 ? new string('.', 24) : "." + new string('#', 22) + ".")]);

        Assert.False(Fit(mask, out _));
    }

    [Fact]
    public void TryFit_ThinStraightStroke_IsRejected()
    {
        // 2 px wide: its outline lies within a pixel of a very flat ellipse.
        var mask = TestImages.Mask(new string('.', 64), "." + new string('#', 62) + ".", "." + new string('#', 62) + ".", new string('.', 64));

        Assert.False(Fit(mask, out _));
    }

    [Fact]
    public void TryFit_ThinArc_IsRejected()
    {
        // Upper half of a 3 px ring: both sides hug one circle, but the area gives it away.
        var ring = TestImages.Ellipse(80, 30, 30, 0, 27, 27);
        var pixels = ring.Pixels.Select((p, i) => i / 80 < 40 ? p : (byte)0).ToArray();

        Assert.False(Fit(new BinaryImage(80, 80, pixels), out _));
    }
}
