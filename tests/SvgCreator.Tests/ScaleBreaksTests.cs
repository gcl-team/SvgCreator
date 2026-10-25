using SvgCreator.Fitting;
using SvgCreator.Mapping;

namespace SvgCreator.Tests;

public class ScaleBreaksTests
{
    // A city's worth of stops around the origin, a few units apart.
    private static readonly PointD[] City = [new(0, 0), new(2, 1), new(-1, 2), new(1, -2), new(-2, -1)];

    // The trip's middle as ScaleBreaks finds it: the coordinate-wise median.
    private static PointD Middle(PointD[] trip) => new(Median(trip.Select(p => p.X)), Median(trip.Select(p => p.Y)));

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;
    }

    [Fact]
    public void Apply_LeavesATripWithNoFarStopToScale()
    {
        var (positions, groups) = ScaleBreaks.Apply(City);

        Assert.Equal(City, positions);
        Assert.All(groups, g => Assert.Equal(0, g));
    }

    [Fact]
    public void Apply_PullsAFarStopInKeepingItsDirection()
    {
        PointD airport = new(40, 10);
        PointD[] trip = [.. City, airport];
        var (positions, groups) = ScaleBreaks.Apply(trip);

        Assert.Equal(City, positions[..City.Length]);
        Assert.Equal(1, groups[^1]);

        var middle = Middle(trip);
        var (before, after) = (airport - middle, positions[^1] - middle);
        Assert.True(after.Length < before.Length / 2, $"pulled in to {positions[^1]}");
        Assert.Equal(before.Normalize().X, after.Normalize().X, 9);
        Assert.Equal(before.Normalize().Y, after.Normalize().Y, 9);
    }

    [Fact]
    public void Apply_LandsClearOfANearStopOnTheWay()
    {
        // A near stop in the airport's direction: the airport mustn't land right next to it.
        PointD onTheWay = new(4, 0);
        PointD[] trip = [.. City, onTheWay, new(40, 0)];
        var (positions, _) = ScaleBreaks.Apply(trip);

        var middle = Middle(trip);
        var reach = trip[..^1].Max(p => (p - middle).Length);
        Assert.True((positions[^1] - onTheWay).Length >= (reach * ScaleBreaks.ClearanceFactor) - 1e-9);
    }

    [Fact]
    public void Apply_MovesNearbyFarStopsTogether()
    {
        // An airport and its hotel, far out but close to each other, keep their arrangement.
        PointD airport = new(40, 0), hotel = new(41, 1);
        var (positions, groups) = ScaleBreaks.Apply([.. City, airport, hotel]);

        Assert.Equal(groups[^2], groups[^1]);
        Assert.NotEqual(0, groups[^1]);
        Assert.Equal(hotel - airport, positions[^1] - positions[^2]);
    }

    [Fact]
    public void Apply_FarStopsInDifferentDirectionsBreakSeparately()
    {
        var (_, groups) = ScaleBreaks.Apply([.. City, new(40, 0), new(-40, 0)]);

        Assert.NotEqual(groups[^2], groups[^1]);
        Assert.All(groups[^2..], g => Assert.NotEqual(0, g));
    }

    [Fact]
    public void Apply_IgnoresTinyTrips()
    {
        var (positions, groups) = ScaleBreaks.Apply([new(0, 0), new(100, 0)]);

        Assert.Equal([new PointD(0, 0), new PointD(100, 0)], positions);
        Assert.All(groups, g => Assert.Equal(0, g));
    }

    [Fact]
    public void DistanceKm_IsTheGreatCircleDistance()
    {
        // Pudong Airport to Longyang Road: about 25 km as the crow flies.
        Assert.Equal(25.3, ScaleBreaks.DistanceKm(31.1443, 121.8083, 31.2004, 121.5504), 0);

        // A degree of latitude is about 111 km anywhere.
        Assert.Equal(111.2, ScaleBreaks.DistanceKm(0, 0, 1, 0), 1);
    }

    [Theory]
    [InlineData(37.6, "≈ 38 km")]
    [InlineData(6.54, "≈ 6.5 km")]
    [InlineData(0.84, "≈ 800 m")]
    public void FormatDistance_RoundsToWhatAMapNeeds(double km, string expected) =>
        Assert.Equal(expected, ScaleBreaks.FormatDistance(km));
}
