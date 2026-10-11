using SvgCreator.Tracing;

namespace SvgCreator.Tests;

public class ContourTracerTests
{
    private static IReadOnlyList<Contour> Trace(params string[] rows) =>
        ContourTracer.Trace(ComponentLabeler.Label(TestImages.Mask(rows), minArea: 0));

    [Fact]
    public void Trace_FilledSquare_ReturnsOneOuterContourAlongPixelEdges()
    {
        var contours = Trace(
            "....",
            ".##.",
            ".##.",
            "....");

        var contour = Assert.Single(contours);
        Assert.False(contour.IsHole);
        Assert.Equal(
            [new PointI(3, 1), new PointI(3, 3), new PointI(1, 3), new PointI(1, 1)],
            contour.Points);
    }

    [Fact]
    public void Trace_Ring_ReturnsOuterAndHoleForSameComponent()
    {
        var contours = Trace(
            "###",
            "#.#",
            "###");

        Assert.Equal(2, contours.Count);
        Assert.Single(contours, c => !c.IsHole);
        Assert.Single(contours, c => c.IsHole);
        Assert.All(contours, c => Assert.Equal(1, c.Label));
    }

    [Fact]
    public void Trace_OnePixelWideLine_KeepsItsArea()
    {
        var contours = Trace("#####");

        var contour = Assert.Single(contours);
        // Twice the area of a 5x1 rectangle.
        Assert.Equal(10, ContourTracer.SignedArea(contour.Points));
    }

    [Fact]
    public void Trace_DiagonalPixels_FormOneOuterContour()
    {
        var contours = Trace(
            "#.",
            ".#");

        var contour = Assert.Single(contours);
        Assert.False(contour.IsHole);
        Assert.Equal(4, ContourTracer.SignedArea(contour.Points));
    }
}
