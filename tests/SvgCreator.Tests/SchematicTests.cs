using System.Xml.Linq;
using SvgCreator.Mapping;

namespace SvgCreator.Tests;

public class SchematicTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    // Four stations roughly west to east, as on Shanghai's Line 13, a hotel by the first and a
    // museum by the second.
    private static readonly TravelStop West = new(0, "West Station", 31.2318, 121.4127) { Waypoint = true };
    private static readonly TravelStop Middle = new(0, "Middle Station", 31.2354, 121.4600) { Waypoint = true };
    private static readonly TravelStop South = new(0, "South Station", 31.2010, 121.4870) { Waypoint = true };
    private static readonly TravelStop East = new(0, "East Station", 31.1985, 121.5885) { Waypoint = true };
    private static readonly TravelStop Hotel = new(0, "Hotel", 31.2300, 121.4100);
    private static readonly TravelStop Museum = new(0, "Museum", 31.2359, 121.4628);
    private static readonly TravelStop Park = new(0, "Park", 31.2384, 121.4596);

    // The stops in travel order, numbered, each riding the line given beside it to the next.
    private static List<TravelStop> Trip(params (TravelStop Stop, string? Line)[] legs) =>
        [.. legs.Select((l, i) => l.Stop with { Sequence = i + 1, Line = l.Line })];

    private static Schematic.Map Build(List<TravelStop> ordered)
    {
        var plan = global::SvgCreator.Svg.TravelMapWriter.MetroPlan.For(ordered, [.. ordered.Select(s => WebMercator.Project(s.Latitude, s.Longitude))]);
        return plan.Map;
    }

    private static int PlaceIndex(List<TravelStop> ordered, TravelStop stop)
    {
        var names = ordered.Select(s => s.Name).Distinct().ToList();
        return names.IndexOf(stop.Name);
    }

    [Fact]
    public void Build_DrawsARideHomeAlongStationsAlreadyOnItsLineOnce()
    {
        var trip = Trip((West, "13"), (Middle, "13"), (South, "13"), (East, "13"), (West, null));

        var map = Build(trip);

        // West–Middle, Middle–South and South–East; the ride home from East to West is drawn by them.
        Assert.Equal(3, map.Edges.Count(e => e.Line == "13"));
        Assert.DoesNotContain(map.Edges, e => (e.From, e.To) is (0, 3) or (3, 0));
    }

    [Fact]
    public void Build_SplitsARidePastAStationOnItsLineThere()
    {
        // Out to South, then from East back past South to West: South is on the way.
        var trip = Trip((West, "13"), (South, null), (East, "13"), (West, null));

        var map = Build(trip);

        var rides = map.Edges.Where(e => e.Line == "13").Select(e => (Math.Min(e.From, e.To), Math.Max(e.From, e.To))).Order().ToList();
        var (west, south, east) = (PlaceIndex(trip, West), PlaceIndex(trip, South), PlaceIndex(trip, East));
        Assert.Equal([(Math.Min(west, south), Math.Max(west, south)), (Math.Min(south, east), Math.Max(south, east))], rides);
    }

    [Fact]
    public void Build_RunsEverySegmentOfATreeAtAMultipleOf45Degrees()
    {
        var trip = Trip((Hotel, null), (West, "13"), (Middle, null), (Museum, null), (Park, null), (Middle, "13"), (South, "13"), (East, null));

        var map = Build(trip);

        Assert.All(map.Edges, e =>
        {
            var d = map.Positions[e.To] - map.Positions[e.From];
            var angle = double.RadiansToDegrees(Math.Atan2(d.Y, d.X));
            Assert.Equal(0, Math.IEEERemainder(angle, 45), 6);
        });
    }

    [Fact]
    public void Build_GivesEveryRideTheSameLength()
    {
        // West to Middle is 4.5 km, Middle to East 13 km: on a metro map they're the same.
        var trip = Trip((West, "13"), (Middle, "13"), (East, null));

        var map = Build(trip);

        var lengths = map.Edges.Select(e => (map.Positions[e.To] - map.Positions[e.From]).Length).ToList();
        Assert.Equal(lengths[0], lengths[1], 6);
    }

    [Fact]
    public void Build_MarksStationsWhereLinesMeetAsInterchanges()
    {
        var trip = Trip((West, "13"), (Middle, "15"), (South, null));

        var map = Build(trip);

        Assert.Equal([false, true, false], map.Interchanges);
    }

    [Fact]
    public void Build_HangsPlacesWalkedToFromAStationOnAStraightBranch()
    {
        var trip = Trip((West, "13"), (Middle, null), (Museum, null), (Park, null), (Middle, "13"), (West, null));

        var map = Build(trip);

        var (middle, museum, park) = (PlaceIndex(trip, Middle), PlaceIndex(trip, Museum), PlaceIndex(trip, Park));
        var first = map.Positions[museum] - map.Positions[middle];
        var second = map.Positions[park] - map.Positions[museum];
        Assert.Equal(first.X, second.X, 6);
        Assert.Equal(first.Y, second.Y, 6);

        // The branch is the walk out, Middle → Museum → Park; the walk back is left out.
        Assert.Equal(2, map.Edges.Count(e => e.Line is null));
    }

    [Fact]
    public void Create_MetroDrawsRidesWithBadgesAndWalksDotted()
    {
        var trip = Trip((Hotel, null), (West, "13"), (Middle, null), (Museum, null));

        var root = XDocument.Parse(new TravelMapGenerator().Create(trip, new TravelMapOptions { Layout = TravelMapLayout.Metro })).Root!;
        IEnumerable<XElement> ByClass(string name) => root.Descendants().Where(e => (string?)e.Attribute("class") == name);

        Assert.Single(ByClass("travel-ride"));
        Assert.Equal(2, ByClass("travel-walk").Count());
        Assert.All(ByClass("travel-walk"), w => Assert.NotNull(w.Attribute("stroke-dasharray")));
        Assert.Equal(["Line 13"], ByClass("travel-line-badge").Select(b => b.Element(Svg + "text")!.Value));
        Assert.Empty(ByClass("travel-arrow"));
    }

    [Fact]
    public void Create_MetroDrawsInterchangesAsRings()
    {
        var trip = Trip((West, "13"), (Middle, "15"), (South, null), (Museum, null));

        var root = XDocument.Parse(new TravelMapGenerator().Create(trip, new TravelMapOptions { Layout = TravelMapLayout.Metro })).Root!;

        var ring = Assert.Single(root.Descendants(Svg + "circle"), c => (string?)c.Attribute("class") == "travel-interchange");
        Assert.NotNull(ring.Attribute("stroke"));
    }

    [Fact]
    public void Create_MetroWithNoLinesStillDrawsEveryLeg()
    {
        var trip = Trip((Hotel, null), (Museum, null), (Park, null));

        var root = XDocument.Parse(new TravelMapGenerator().Create(trip, new TravelMapOptions { Layout = TravelMapLayout.Metro })).Root!;

        Assert.Equal(2, root.Descendants().Count(e => (string?)e.Attribute("class") == "travel-walk"));
    }

    [Fact]
    public void Create_MetroPanelPutsTheItineraryInThePanelEvenWhenItWouldFit()
    {
        var trip = Trip((Hotel, null), (West, "13"), (Middle, null), (Museum, null));
        string Draw(TravelMapLayout layout) => new TravelMapGenerator().Create(trip, new TravelMapOptions { Width = 1600, Height = 1000, Layout = layout });
        bool HasPanel(string svg) => XDocument.Parse(svg).Root!.Descendants().Any(e => (string?)e.Attribute("class") == "travel-panel");

        Assert.False(HasPanel(Draw(TravelMapLayout.Metro)));
        var root = XDocument.Parse(Draw(TravelMapLayout.MetroPanel)).Root!;
        Assert.True(HasPanel(root.ToString()));
        Assert.Contains(root.Descendants(), e => (string?)e.Attribute("class") == "travel-ride");
    }

    [Theory]
    [InlineData("13", "Line 13")]
    [InlineData("Maglev", "Maglev")]
    [InlineData("NS", "NS")]
    [InlineData("Line 13", "Line 13")]
    public void BadgeText_PrefixesOnlyBareNumbers(string line, string expected) =>
        Assert.Equal(expected, global::SvgCreator.Svg.TravelMapWriter.BadgeText(line));
}
