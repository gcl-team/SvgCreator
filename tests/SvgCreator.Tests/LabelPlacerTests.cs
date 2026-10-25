using SvgCreator.Fitting;
using SvgCreator.Mapping;

namespace SvgCreator.Tests;

public class LabelPlacerTests
{
    private static readonly RectD Canvas = new(0, 0, 1000, 600);

    private static LabelRequest At(double x, double y) => new(new PointD(x, y), 10, 10, 120, 60, 20);

    private static LabelPlacement[] Place(LabelRequest[] requests, params (PointD, PointD)[] arrows) =>
        LabelPlacer.Place(requests, arrows, 4, 8, Canvas, offCanvasWeight: 50);

    [Fact]
    public void Place_WithNothingInTheWay_GoesBelow()
    {
        var placement = Assert.Single(Place([At(500, 300)]));

        Assert.Equal(LabelSide.Below, placement.Side);
        Assert.Equal(new RectD(440, 318, 120, 60), placement.Bounds);
    }

    [Fact]
    public void Place_AnArrowLeavingDownwards_PushesTheLabelAside()
    {
        var placement = Assert.Single(Place([At(500, 300)], (new PointD(500, 322), new PointD(500, 550))));

        Assert.NotEqual(LabelSide.Below, placement.Side);
        Assert.Equal(0, LabelPlacer.ClippedLength(new PointD(500, 322), new PointD(500, 550), placement.Bounds));
    }

    [Fact]
    public void Place_AtTheCanvasEdge_StaysOnIt()
    {
        var placements = Place([At(500, 590), At(990, 300)]);

        Assert.All(placements, p => Assert.True(Canvas.Contains(p.Bounds), $"{p.Side} {p.Bounds}"));
    }

    [Fact]
    public void Place_NeighboursDoNotOverlap()
    {
        // Two markers side by side: both labels below would overlap.
        var placements = Place([At(500, 300), At(560, 300)]);

        Assert.Equal(0, placements[0].Bounds.OverlapArea(placements[1].Bounds));
    }

    [Fact]
    public void Candidate_BesideTheMarker_CentresTheFirstLineOnIt()
    {
        var request = At(500, 300);

        Assert.Equal(new RectD(518, 290, 120, 60), LabelPlacer.Candidate(request, LabelSide.Right, 8));
        Assert.Equal(new RectD(362, 290, 120, 60), LabelPlacer.Candidate(request, LabelSide.Left, 8));
        Assert.Equal(new RectD(440, 222, 120, 60), LabelPlacer.Candidate(request, LabelSide.Above, 8));
    }

    [Theory]
    [InlineData(-10, 50, 110, 50, 100)]   // straight through
    [InlineData(50, 50, 60, 50, 10)]      // wholly inside
    [InlineData(-50, -50, 150, 150, 141.42135623730951)] // corner to corner
    [InlineData(-10, -10, -10, 200, 0)]   // beside it
    [InlineData(200, 0, 300, 100, 0)]     // well away
    public void ClippedLength_MeasuresTheSegmentInsideTheRectangle(double x0, double y0, double x1, double y1, double expected) =>
        Assert.Equal(expected, LabelPlacer.ClippedLength(new PointD(x0, y0), new PointD(x1, y1), new RectD(0, 0, 100, 100)), 9);
}
