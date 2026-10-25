using SvgCreator.Fitting;
using SvgCreator.Mapping;

namespace SvgCreator.Tests;

public class TravelMapGeometryTests
{
    private const double Tolerance = 1e-9;

    private static readonly RectD Area = new(100, 50, 800, 400);

    private static void AssertNear(PointD expected, PointD actual)
    {
        Assert.Equal(expected.X, actual.X, 1e-6);
        Assert.Equal(expected.Y, actual.Y, 1e-6);
    }

    [Fact]
    public void Project_EquatorAndPrimeMeridianIsTheWorldCentre()
    {
        var p = WebMercator.Project(0, 0);

        Assert.Equal(0.5, p.X, Tolerance);
        Assert.Equal(0.5, p.Y, Tolerance);
    }

    [Fact]
    public void Project_MaxLatitudeIsTheTopEdgeAndTheAntimeridianTheSides()
    {
        Assert.Equal(0, WebMercator.Project(WebMercator.MaxLatitude, -180).Y, 1e-7);
        Assert.Equal(1, WebMercator.Project(-WebMercator.MaxLatitude, 180).Y, 1e-7);
        Assert.Equal(0, WebMercator.Project(0, -180).X, Tolerance);
        Assert.Equal(1, WebMercator.Project(0, 180).X, Tolerance);
    }

    [Fact]
    public void Project_NorthIsUpAndEastIsRight()
    {
        var cityHall = WebMercator.Project(1.2931, 103.8520);
        var zoo = WebMercator.Project(1.4043, 103.7930);
        var changi = WebMercator.Project(1.3644, 103.9915);

        Assert.True(zoo.Y < cityHall.Y);
        Assert.True(zoo.X < cityHall.X && cityHall.X < changi.X);
    }

    [Fact]
    public void Project_IsConformal()
    {
        // Mercator keeps local shape: at latitude φ, one degree of latitude spans 1/cos φ times
        // what one degree of longitude does (for small steps).
        const double lat = 60;
        var origin = WebMercator.Project(lat, 10);
        var east = WebMercator.Project(lat, 10.001);
        var north = WebMercator.Project(lat + 0.001, 10);

        Assert.Equal(1 / Math.Cos(double.DegreesToRadians(lat)), (origin.Y - north.Y) / (east.X - origin.X), 1e-3);
    }

    [Fact]
    public void Fit_ScalesUniformlyByTheTighterSideAndCentres()
    {
        // A 1 × 1 box in an 800 × 400 area: the height holds it to 400 per unit, centred across.
        var square = MapFit.Fit([new PointD(0, 0), new PointD(1, 1)], Area);

        AssertNear(new PointD(300, 50), square[0]);
        AssertNear(new PointD(700, 450), square[1]);
    }

    [Fact]
    public void Fit_KeepsTheShapeOfTheGeography()
    {
        PointD[] projected = [new(0.2, 0.7), new(0.25, 0.6), new(0.4, 0.65)];
        var fitted = MapFit.Fit(projected, Area);

        var before = (projected[1] - projected[0]).Length / (projected[2] - projected[0]).Length;
        var after = (fitted[1] - fitted[0]).Length / (fitted[2] - fitted[0]).Length;
        Assert.Equal(before, after, Tolerance);
        Assert.All(fitted, p => Assert.InRange(p.X, Area.X - Tolerance, Area.Right + Tolerance));
        Assert.All(fitted, p => Assert.InRange(p.Y, Area.Y - Tolerance, Area.Bottom + Tolerance));
    }

    [Fact]
    public void Fit_OneStopOrStopsAtOnePlaceGoToTheCentre()
    {
        Assert.Equal(Area.Center, Assert.Single(MapFit.Fit([new PointD(0.3, 0.4)], Area)));
        Assert.All(MapFit.Fit([new PointD(0.3, 0.4), new PointD(0.3, 0.4)], Area), p => Assert.Equal(Area.Center, p));
        Assert.Empty(MapFit.Fit([], Area));
    }

    [Fact]
    public void Fit_StopsInALineSpanTheirAxis()
    {
        var horizontal = MapFit.Fit([new PointD(0.1, 0.5), new PointD(0.3, 0.5)], Area);
        var vertical = MapFit.Fit([new PointD(0.5, 0.1), new PointD(0.5, 0.3)], Area);

        AssertNear(new PointD(100, 250), horizontal[0]);
        AssertNear(new PointD(900, 250), horizontal[1]);
        AssertNear(new PointD(500, 50), vertical[0]);
        AssertNear(new PointD(500, 450), vertical[1]);
    }
}
