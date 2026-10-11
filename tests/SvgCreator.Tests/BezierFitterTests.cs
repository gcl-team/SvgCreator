using SvgCreator.Fitting;

namespace SvgCreator.Tests;

public class BezierFitterTests
{
    [Fact]
    public void Fit_PointsSampledFromOneCubic_RecoverSingleSegment()
    {
        var source = new CubicBezier(new(0, 0), new(10, 30), new(40, 30), new(50, 0));
        var points = Enumerable.Range(0, 60).Select(i => source.Evaluate(i / 59.0)).ToArray();
        var output = new List<CubicBezier>();

        BezierFitter.Fit(points, (source.P1 - source.P0).Normalize(), (source.P2 - source.P3).Normalize(), 1.0, output);

        var fitted = Assert.Single(output);
        Assert.Equal(source.P0, fitted.P0);
        Assert.Equal(source.P3, fitted.P3);
    }

    [Fact]
    public void Fit_SharpZigZag_SplitsAndStaysWithinTolerance()
    {
        PointD[] points = [new(0, 0), new(5, 10), new(10, 0), new(15, 10), new(20, 0)];
        var output = new List<CubicBezier>();

        BezierFitter.Fit(points, BezierFitter.StartTangent(points), BezierFitter.EndTangent(points), 0.25, output);

        Assert.True(output.Count > 1);
        Assert.Equal(points[0], output[0].P0);
        Assert.Equal(points[^1], output[^1].P3);
        for (var i = 1; i < output.Count; i++)
        {
            Assert.Equal(output[i - 1].P3, output[i].P0);
        }
    }
}
