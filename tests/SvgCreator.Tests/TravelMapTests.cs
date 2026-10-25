using System.Globalization;
using System.Xml.Linq;
using SvgCreator.Fitting;
using SvgCreator.Mapping;
using SvgCreator.Svg;

namespace SvgCreator.Tests;

public class TravelMapTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static readonly TravelStop CityHall = new(1, "City Hall", 1.2931, 103.8520) { Time = "Day 1 · 9 am", Description = ["Meet at Exit B"] };
    private static readonly TravelStop Zoo = new(2, "Singapore Zoo", 1.4043, 103.7930) { Time = "Day 1 · 11 am" };
    private static readonly TravelStop Changi = new(3, "Changi Airport", 1.3644, 103.9915) { Description = ["Jewel", "Flight home"] };
    private static readonly TravelStop Raffles = new(4, "Raffles Hotel", 1.2949, 103.8545);

    // A marker's text, for geometry tests that don't look at it.
    private static readonly TravelMapWriter.Block AnyText = TravelMapWriter.BlockFor([CityHall], 560);

    private static XElement Create(params TravelStop[] stops) =>
        XDocument.Parse(new TravelMapGenerator().Create(stops, new TravelMapOptions())).Root!;

    private static double Number(XElement e, string attribute) => double.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);

    private static IEnumerable<XElement> ByClass(XElement root, string className) =>
        root.Descendants().Where(e => (string?)e.Attribute("class") == className);

    [Fact]
    public void Create_DrawsAMarkerPerStopAndAnArrowPerLeg()
    {
        var root = Create(Changi, CityHall, Zoo);

        Assert.Equal(["1", "2", "3"], ByClass(root, "travel-marker").Select(m => m.Element(Svg + "text")!.Value).Order());
        Assert.Equal(2, ByClass(root, "travel-arrow").Count());
    }

    [Fact]
    public void Create_PrintsNameTimeAndDescriptionInThatOrder()
    {
        var root = Create(CityHall, Zoo);
        var label = ByClass(root, "travel-label").First();

        Assert.Equal(["travel-name", "travel-time", "travel-description"], label.Elements(Svg + "tspan").Select(t => (string)t.Attribute("class")!));
        Assert.Equal(["City Hall", "Day 1 · 9 am", "Meet at Exit B"], label.Elements(Svg + "tspan").Select(t => t.Value));
    }

    [Fact]
    public void Create_ClosePlacesKeepTheirOwnMarkers()
    {
        // City Hall and Raffles are 300 m apart: neighbours at this scale, but different places.
        var root = Create(CityHall, Raffles with { Sequence = 2 }, Zoo with { Sequence = 3 });

        var markers = ByClass(root, "travel-marker").ToList();
        Assert.Equal(["1", "2", "3"], markers.Select(m => m.Element(Svg + "text")!.Value));
        Assert.All(markers, m => Assert.NotNull(m.Element(Svg + "circle")));
        Assert.Equal(3, ByClass(root, "travel-label").Count());
    }

    [Fact]
    public void Create_NeighboursKeepRoomForAnArrowBetweenThem()
    {
        // 300 m apart: true to scale they'd touch, and the leg between them would vanish.
        var root = Create(CityHall, Raffles with { Sequence = 2 }, Zoo with { Sequence = 3 });
        var circles = ByClass(root, "travel-marker").Select(m => m.Element(Svg + "circle")!).ToList();
        var (a, b) = (circles[0], circles[1]);

        var gap = new PointD(Number(a, "cx") - Number(b, "cx"), Number(a, "cy") - Number(b, "cy")).Length - Number(a, "r") - Number(b, "r");
        Assert.True(gap >= (0.09 * 560) - 0.01, $"gap {gap}");
        Assert.Equal(2, ByClass(root, "travel-arrow").Count());
    }

    [Fact]
    public void Route_LinesHaveNoHeads()
    {
        var root = Create(new TravelMapOptions { Route = TravelMapRoute.Lines }, CityHall, Zoo, Changi);

        Assert.Equal(2, ByClass(root, "travel-arrow-shaft").Count());
        Assert.Empty(ByClass(root, "travel-arrow-head"));
    }

    [Fact]
    public void Route_NoneDrawsMarkersOnly()
    {
        var root = Create(new TravelMapOptions { Route = TravelMapRoute.None }, CityHall, Zoo, Changi);

        Assert.Empty(ByClass(root, "travel-arrow"));
        Assert.Equal(3, ByClass(root, "travel-marker").Count());
    }

    [Theory]
    [InlineData(TravelMapRoute.Arrows, 0.09)]
    [InlineData(TravelMapRoute.Lines, 0.05)]
    [InlineData(TravelMapRoute.None, 0.03)]
    public void Route_SetsHowCloseNeighboursMayBe(TravelMapRoute route, double gap)
    {
        // City Hall and Raffles, 300 m apart, are nudged apart to the style's gap: along one axis,
        // so on a slight diagonal a little more.
        // To scale: with the two so close, the zoo would otherwise count as far and be pulled in.
        var root = Create(new TravelMapOptions { Route = route, Layout = TravelMapLayout.Labels, Breaks = TravelMapBreaks.None }, CityHall, Raffles with { Sequence = 2 }, Zoo with { Sequence = 3 });
        var circles = ByClass(root, "travel-marker").Select(m => m.Element(Svg + "circle")!).ToList();
        var between = new PointD(Number(circles[0], "cx") - Number(circles[1], "cx"), Number(circles[0], "cy") - Number(circles[1], "cy")).Length
            - Number(circles[0], "r") - Number(circles[1], "r");

        Assert.InRange(between, (gap * 560) - 0.01, (gap * 560) + 2);
    }

    [Fact]
    public void Breaks_PullTheAirportInAndLabelTheRealDistance()
    {
        TravelStop[] trip = [new(0, "Pudong Airport", 31.1443, 121.8083) { Time = "Day 1 · 4 pm" }, .. Shanghai[1..]];
        var root = Create(new TravelMapOptions { Width = 1600, Height = 1000 }, trip);

        var broken = Assert.Single(ByClass(root, "travel-arrow"), a => ByClass(a, "travel-arrow-break").Any());
        Assert.NotNull(ByClass(broken, "travel-arrow-head").SingleOrDefault());
        Assert.Equal("≈ 25 km", Assert.Single(ByClass(root, "travel-distance")).Value);
    }

    [Fact]
    public void Breaks_NoneKeepsEverythingToScale()
    {
        TravelStop[] trip = [new(0, "Pudong Airport", 31.1443, 121.8083), .. Shanghai[1..]];
        var root = Create(new TravelMapOptions { Breaks = TravelMapBreaks.None }, trip);

        Assert.Empty(ByClass(root, "travel-arrow-break"));
        Assert.Empty(ByClass(root, "travel-distance"));
    }

    [Theory]
    [MemberData(nameof(Trips))]
    public void Breaks_LeaveTripsWithNoFarStopAlone(string trip)
    {
        Assert.Empty(ByClass(Create(RoomFor(trip), Trip(trip)), "travel-arrow-break"));
    }

    // The Shanghai day with its metro stations as waypoints: four places, seven stations. A
    // property, not a field: fields initialise in file order, and Shanghai is declared below.
    private static TravelStop[] ShanghaiByMetro =>
    [
        new(1, "Pudong Airport", 31.1443, 121.8083) { Time = "Day 1 · 4 pm", Description = ["Land", "Take the Maglev train"] },
        .. Shanghai[1..].Select(s => s.Name.Contains("Station", StringComparison.Ordinal) ? s with { Waypoint = true } : s),
    ];

    [Fact]
    public void Numbers_CountPlacesOnly()
    {
        Assert.Equal([1, null, null, null, 2, null, null, 3, null, null, 4], TravelMapWriter.Numbers(ShanghaiByMetro));
    }

    [Fact]
    public void Waypoints_AreUnnumberedDotsTheRouteRunsThrough()
    {
        var root = Create(new TravelMapOptions { Width = 1600, Height = 1000 }, ShanghaiByMetro);

        // Seven station stops, but Daduhe Road and Guilin Park are each passed twice: five dots.
        Assert.Equal(5, ByClass(root, "travel-waypoint").Count());
        Assert.Equal(["1", "2 · 4", "3"], ByClass(root, "travel-marker").Select(m => m.Element(Svg + "text")!.Value));

        // Ten legs, every stop to the next; arrowheads only on arrival at the four places.
        Assert.Equal(10, ByClass(root, "travel-arrow").Count());
        Assert.Equal(3, ByClass(root, "travel-arrow-head").Count());
    }

    [Fact]
    public void Waypoints_AreStepsInThePanelUnderThePlaceBefore()
    {
        var root = Create(new TravelMapOptions { Layout = TravelMapLayout.Panel }, ShanghaiByMetro);
        var panel = ByClass(root, "travel-panel").Single();
        var entries = panel.Elements(Svg + "g").ToList();

        Assert.Equal(
            ["1", "•", "•", "•", "2", "•", "•", "3", "•", "•", "4"],
            entries.Select(e => (string)e.Attribute("class")! == "travel-step" ? "•" : e.Element(Svg + "text")!.Value));

        // A step is indented past its place's name.
        static double X(XElement entry) => Number(entry.Descendants(Svg + "tspan").First(), "x");
        Assert.True(X(entries[1]) > X(entries[0]));
    }

    [Fact]
    public void Waypoints_AreInTheirOwnColourOnTheMapAndInThePanel()
    {
        var options = new TravelMapOptions { Layout = TravelMapLayout.Panel, WaypointColor = "orange" };
        var root = Create(options, ShanghaiByMetro);

        Assert.All(ByClass(root, "travel-waypoint"), dot => Assert.Equal("orange", (string?)dot.Attribute("fill")));
        Assert.All(ByClass(root, "travel-step"), step => Assert.Equal("orange", (string?)step.Attribute("fill")));
        var names = ByClass(root, "travel-labels").Single().Elements(Svg + "text").ToList();
        Assert.All(names.Where(n => ByClass(n, "travel-waypoint-name").Any()), n => Assert.Equal("orange", (string?)n.Attribute("fill")));
        Assert.All(names.Where(n => ByClass(n, "travel-name").Any()), n => Assert.Null(n.Attribute("fill")));

        // Places and the route keep the foreground colour.
        Assert.All(ByClass(root, "travel-marker"), m => Assert.Equal(options.ForegroundColor, (string?)m.Elements().First().Attribute("fill")));
    }

    [Fact]
    public void Validate_NeedsAPlaceNotOnlyWaypoints()
    {
        Assert.Throws<ArgumentException>(() => new TravelMapGenerator().Create([CityHall with { Waypoint = true }], new TravelMapOptions()));
    }

    [Fact]
    public void Wrap_BreaksBetweenAnEnglishAndAChineseNameFirst()
    {
        const string name = "Jinshajiang Road Station 金沙江路";
        var width = FontMetrics.Sans.Width("Jinshajiang Road Station 金", 10);

        Assert.Equal(["Jinshajiang Road Station", "金沙江路"], TravelMapWriter.Wrap(name, 10, width));
    }

    [Fact]
    public void Wrap_StartsAChineseNameTooLongForOneLineOnAFreshLine()
    {
        var lines = TravelMapWriter.Wrap("Hanting Hotel 汉庭酒店(上海金沙江路地铁站店)", 10, FontMetrics.Sans.Width("Hanting Hotel 汉庭", 10));

        Assert.Equal("Hanting Hotel", lines[0]);
        Assert.Equal("汉庭酒店(上海金沙江路地铁站店)", string.Concat(lines.Skip(1)));
    }

    [Fact]
    public void Create_ARevisitedPlaceSharesOneMarker()
    {
        var root = Create(CityHall, Zoo, CityHall with { Sequence = 3, Time = "Day 2" });

        var markers = ByClass(root, "travel-marker").ToList();
        Assert.Equal(2, markers.Count);
        Assert.Contains(markers, m => m.Element(Svg + "text")!.Value == "1 · 3" && m.Element(Svg + "rect") is not null);
    }

    [Fact]
    public void Places_AreStopsWithTheSameName()
    {
        TravelStop[] stops = [CityHall, Zoo, CityHall with { Sequence = 3, Name = " CITY HALL " }, Raffles with { Sequence = 4 }];
        var places = TravelMapWriter.Places(stops, [new(0, 0), new(100, 0), new(10, 20), new(5, 5)]);

        Assert.Equal([[0, 2], [1], [3]], places.Select(p => p.Members));
        Assert.Equal(new PointD(5, 10), places[0].Center);
    }

    // Each line's horizontal extent, from its x and its <text>'s text-anchor.
    private static IEnumerable<(XElement Line, double Left, double Right)> LineExtents(XElement root) =>
        root.Descendants(Svg + "tspan").Select(t =>
        {
            var width = FontMetrics.Sans.Width(t.Value, Number(t, "font-size"));
            var x = Number(t, "x");
            var left = (string?)t.Parent!.Attribute("text-anchor") switch
            {
                "start" => x,
                "end" => x - width,
                _ => x - (width / 2),
            };
            return (t, left, left + width);
        });

    [Theory]
    [MemberData(nameof(Trips))]
    public void Create_TextStaysOnTheCanvas(string trip)
    {
        var root = Create(Trip(trip));
        var options = new TravelMapOptions();

        foreach (var (line, left, right) in LineExtents(root))
        {
            Assert.InRange(left, 0, options.Width);
            Assert.InRange(right, 0, options.Width);
            Assert.InRange(Number(line, "y"), 0, options.Height);
        }
    }

    // Changi to Changi over four days: crossing arrows, two shared markers, labels near every edge.
    private static readonly TravelStop[] BusyTrip =
    [
        new(1, "Changi Airport", 1.3644, 103.9915) { Time = "Day 1 · 8 am", Description = ["Land, collect bags"] },
        new(2, "Marina Bay Sands", 1.2834, 103.8607) { Time = "Day 1 · 11 am", Description = ["Check in", "SkyPark at sunset"] },
        new(3, "Gardens by the Bay", 1.2816, 103.8636) { Time = "Day 1 · 7 pm", Description = ["Supertree light show"] },
        new(4, "Sentosa", 1.2494, 103.8303) { Time = "Day 2 · 10 am", Description = ["Beach day"] },
        new(5, "Singapore Zoo", 1.4043, 103.7930) { Time = "Day 3 · 9 am", Description = ["Breakfast with orangutans"] },
        new(6, "Jurong Bird Park", 1.3187, 103.7064) { Time = "Day 3 · 3 pm", Description = ["Now at Mandai"] },
        new(7, "Changi Airport", 1.3644, 103.9915) { Time = "Day 4 · 2 pm", Description = ["Flight home"] },
    ];

    public static TheoryData<string> Trips() => ["three stops", "busy trip"];

    private static TravelStop[] Trip(string name) => name == "busy trip" ? BusyTrip : [CityHall, Zoo, Changi, Raffles with { Sequence = 5 }];

    // Room enough for every label: the busy trip's neighbours (Marina Bay Sands and Gardens by the
    // Bay) need more than the default canvas, where Auto would move their text to the panel.
    private static TravelMapOptions RoomFor(string trip) => trip == "busy trip" ? new() { Width = 1600, Height = 1000 } : new();

    [Theory]
    [MemberData(nameof(Trips))]
    public void Create_LabelsKeepClearOfArrowsAndEachOther(string trip)
    {
        var root = Create(RoomFor(trip), Trip(trip));
        Assert.Empty(ByClass(root, "travel-panel"));
        var arrows = ByClass(root, "travel-arrow").SelectMany(ArrowSegments).ToList();
        var boxes = ByClass(root, "travel-label").Select(label =>
        {
            var lines = LineExtents(root).Where(e => e.Line.Parent == label).ToList();
            var sizes = lines.Select(l => Number(l.Line, "font-size")).ToList();
            var top = Number(lines[0].Line, "y") - (0.8 * sizes[0]);
            var bottom = Number(lines[^1].Line, "y") + (0.4 * sizes[^1]);
            return new RectD(lines.Min(l => l.Left), top, lines.Max(l => l.Right) - lines.Min(l => l.Left), bottom - top);
        }).ToList();

        Assert.All(boxes, box => Assert.All(arrows, a => Assert.Equal(0, LabelPlacer.ClippedLength(a.From, a.To, box))));
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                Assert.Equal(0, boxes[i].OverlapArea(boxes[j]));
            }
        }
    }

    [Fact]
    public void Create_ARevisitedPlacePrintsItsNameOnceWithEachVisit()
    {
        var root = Create(new TravelMapOptions { Layout = TravelMapLayout.Labels }, BusyTrip);
        var changi = ByClass(root, "travel-label").Single(l => l.Elements(Svg + "tspan").First().Value == "Changi Airport");

        Assert.Equal(
            ["Changi Airport", "Day 1 · 8 am", "Land, collect bags", "Day 4 · 2 pm", "Flight home"],
            changi.Elements(Svg + "tspan").Select(t => t.Value));
        Assert.Single(ByClass(root, "travel-name"), n => n.Value == "Changi Airport");
    }

    [Fact]
    public void BlockFor_PrintsTheNameOnceThenEachVisit()
    {
        var block = TravelMapWriter.BlockFor([CityHall, CityHall with { Sequence = 3, Name = " city hall ", Time = "Day 2", Description = [] }], 560);

        Assert.Equal(["City Hall", "Day 1 · 9 am", "Meet at Exit B", "Day 2"], block.Lines.Select(l => l.Text));
    }

    [Fact]
    public void BlockFor_NamesOnly()
    {
        var block = TravelMapWriter.BlockFor([CityHall, CityHall with { Sequence = 3 }], 560, namesOnly: true);

        Assert.Equal(["City Hall"], block.Lines.Select(l => l.Text));
    }

    [Fact]
    public void BlockFor_SetsEachLaterVisitApart()
    {
        var block = TravelMapWriter.BlockFor([CityHall, CityHall with { Sequence = 2, Time = "Day 2", Description = [] }], 560);

        Assert.Equal([0, 0, 0, 0.012 * 560], block.Lines.Select(l => l.SpaceBefore));
        Assert.Equal(block.Lines.Sum(l => (l.Size * 1.2) + l.SpaceBefore), block.Height, 9);
    }

    // ION Orchard, Ngee Ann City, Paragon, Takashimaya and 313@Somerset share one wide pill.
    private static readonly TravelStop[] Orchard =
    [
        new(1, "ION Orchard", 1.3040, 103.8318) { Time = "Day 1 · 10 am", Description = ["Shopping, level B4 food hall"] },
        new(2, "Ngee Ann City", 1.3025, 103.8345) { Time = "Day 1 · 12 pm", Description = ["Kinokuniya bookstore"] },
        new(3, "Paragon", 1.3036, 103.8358) { Time = "Day 1 · 2 pm", Description = ["Coffee break"] },
        new(4, "Takashimaya", 1.3029, 103.8350) { Time = "Day 1 · 3 pm", Description = ["Department store"] },
        new(5, "313@Somerset", 1.3010, 103.8385) { Time = "Day 1 · 5 pm", Description = ["Dinner nearby"] },
        Changi with { Sequence = 6 },
    ];

    [Fact]
    public void Golden_ThreeStops() =>
        SvgGoldenTests.AssertGolden("travel-map.svg", new TravelMapGenerator().Create([CityHall, Zoo, Changi], new TravelMapOptions()));

    [Fact]
    public void Create_ThereAndBackFormsALens()
    {
        // There, back, and there again: three legs between the same two places.
        var root = Create(CityHall, Zoo, CityHall with { Sequence = 3 }, Zoo with { Sequence = 4 });
        var arrows = ByClass(root, "travel-arrow").Select(ArrowPath).ToList();
        var centres = ByClass(root, "travel-marker").Select(m => m.Element(Svg + "rect")!)
            .Select(r => new PointD(Number(r, "x") + (Number(r, "width") / 2), Number(r, "y") + (Number(r, "height") / 2))).ToList();

        Assert.Equal(3, arrows.Count);
        Assert.All(arrows, a => Assert.NotNull(a.Control));

        // How far each curve bows from the line between the places, measured on one side of it.
        var along = (centres[1] - centres[0]).Normalize();
        var normal = new PointD(-along.Y, along.X);
        var bows = arrows.Select(a => (a.Control!.Value - centres[0]).Dot(normal)).ToList();
        Assert.True(bows[0] > 0 && bows[1] < 0, "going and coming back bow to opposite sides");
        Assert.True(bows[2] > bows[0], "the second trip out bows wider than the first");
    }

    [Fact]
    public void Create_ASingleLegIsStraight()
    {
        var root = Create(CityHall, Zoo, Changi);

        Assert.All(ByClass(root, "travel-arrow").Select(ArrowPath), a => Assert.Null(a.Control));
    }

    [Fact]
    public void Create_APillForManyVisitsStaysOnTheCanvas()
    {
        // Back to the hotel between every outing: one place with five visits.
        TravelStop[] outings = [Zoo, Changi, BusyTrip[1], BusyTrip[3]];
        TravelStop[] stops = [.. Enumerable.Range(1, 9).Select(i => (i % 2 == 1 ? CityHall : outings[(i / 2) - 1]) with { Sequence = i })];
        var root = Create(stops);
        var options = new TravelMapOptions();

        var pill = ByClass(root, "travel-marker").Select(m => m.Element(Svg + "rect")).Single(r => r is not null)!;
        Assert.Equal("1 · 3 · 5 · 7 · 9", pill.Parent!.Element(Svg + "text")!.Value);
        Assert.InRange(Number(pill, "x"), 0, options.Width);
        Assert.InRange(Number(pill, "x") + Number(pill, "width"), 0, options.Width);
    }

    [Fact]
    public void Create_NeighbouringPlacesAreNudgedApart()
    {
        // Five shops along Orchard Road, a few hundred metres apart.
        var root = Create(Orchard);

        Assert.Equal(Orchard.Length, ByClass(root, "travel-marker").Count());
    }

    [Fact]
    public void Create_TextTooWideForTheCanvasShrinksEvenly()
    {
        // A name that can't wrap, wider at full size than the whole canvas.
        var narrow = new TravelMapOptions { Width = 300, Height = 400, Layout = TravelMapLayout.Labels };
        var root = Create(narrow, CityHall with { Name = "Llanfairpwllgwyngyllgogerychwyrndrobwllllantysiliogogogoch" }, Zoo);

        foreach (var (line, left, right) in LineExtents(root))
        {
            Assert.InRange(left, 0, narrow.Width);
            Assert.InRange(right, 0, narrow.Width);
        }

        // Names, times and descriptions keep their proportions: all shrink by the same factor.
        var names = ByClass(root, "travel-name").Select(n => Number(n, "font-size")).Distinct().ToList();
        var times = ByClass(root, "travel-time").Select(n => Number(n, "font-size")).Distinct().ToList();
        Assert.True(Assert.Single(names) < 0.042 * 560);
        Assert.Equal(0.042 / 0.03, Assert.Single(names) / Assert.Single(times), 3);
    }

    [Fact]
    public void Description_LeftOutIsNone()
    {
        var stop = new TravelStop(1, "City Hall", 1.29, 103.85) { Description = null! };

        Assert.Empty(stop.Description);
    }

    // An arrow's shaft: its start, its curve's control point (none if straight), and where it ends.
    private static (PointD From, PointD? Control, PointD To) ArrowPath(XElement arrow)
    {
        var shaft = (string)ByClass(arrow, "travel-arrow-shaft").Single().Attribute("d")!;
        shaft = shaft.TrimStart('M');
        if (shaft.Contains('Q', StringComparison.Ordinal))
        {
            var parts = shaft.Split('Q');
            var rest = parts[1].Split(' ');
            return (ParsePoint(parts[0]), new PointD(Parse(rest[0]), Parse(rest[1])), new PointD(Parse(rest[2]), Parse(rest[3])));
        }

        var ends = shaft.Split('L');
        return (ParsePoint(ends[0]), null, ParsePoint(ends[1]));

        static double Parse(string v) => double.Parse(v, CultureInfo.InvariantCulture);
    }

    // The arrow as straight segments, its curve (if any) flattened, for clearance checks.
    private static IEnumerable<(PointD From, PointD To)> ArrowSegments(XElement arrow)
    {
        var (from, control, to) = ArrowPath(arrow);
        return new TravelMapWriter.Leg(from, to, control).Segments();
    }

    private static PointD ParsePoint(string xy)
    {
        var parts = xy.Split(' ');
        return new PointD(double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    // A day in Shanghai: long transit directions, ten stops within ~15 km and the airport 40 km out.
    private static readonly TravelStop[] Shanghai =
    [
        new(1, "Pudong Airport", 31.1443, 121.8083) { Time = "Day 1 · 4 pm", Description = ["Land", "Take the Maglev train"] },
        new(2, "Longyang Road Station 龙阳路", 31.2004, 121.5504) { Time = "Day 1 · 5 pm", Description = ["Take Line 2 towards Hongqiao Railway Station", "Drop off at Century Avenue Station"] },
        new(3, "Century Avenue Station 世纪大道", 31.2243, 121.5404) { Time = "Day 1 · 5.30 pm", Description = ["Take Line 4 towards Jinshajiang Road Station", "Drop off at Jinshajiang Road Station"] },
        new(4, "Jinshajiang Road Station 金沙江路", 31.2318, 121.4127) { Time = "Day 1 · 6 pm", Description = ["Exit at Exit 5", "Turn left to Hanting Hotel"] },
        new(5, "Hanting Hotel 汉庭酒店(上海金沙江路地铁站店)", 31.23, 121.41) { Time = "Day 1 · 6.10 pm", Description = ["Check in at the hotel"] },
        new(6, "Daduhe Road Station 大渡河路", 31.2409, 121.3926) { Time = "Day 1 · 6.30 pm", Description = ["Take Line 13 towards Jinyun Road Station", "Change to Line 15 towards Zizhu Hi-Tech Park Station", "Drop off at Guilin Park Station"] },
        new(7, "Guilin Park Station 桂林公园", 31.16, 121.4241) { Time = "Day 1 · 7 pm", Description = ["Exit at Exit 5", "Walk to The Ring Live 鑫耀·光环LIVE"] },
        new(8, "The Ring Live 鑫耀·光环LIVE", 31.17, 121.4241) { Time = "Day 1 · 7 pm - 9 pm", Description = ["Dinner", "明日方舟：终末地", "崩坏星穹铁道王下一桶塑像", "流萤萨姆雕像", "元界主题街区"] },
        new(9, "Guilin Park Station 桂林公园", 31.16, 121.4241) { Time = "Day 1 · 9 pm", Description = ["Enter at Exit 5", "Take Line 15 towards Gucun Park Station", "Change to Line 13 at Daduhe Road Station"] },
        new(10, "Daduhe Road Station 大渡河路", 31.2409, 121.3926) { Time = "Day 1 · 10 pm", Description = ["Take Line 13 towards Zhangjiang Road Station", "Drop off at Jinshajiang Road Station"] },
        new(11, "Hanting Hotel 汉庭酒店(上海金沙江路地铁站店)", 31.23, 121.41) { Time = "Day 1 · 10.30 pm", Description = ["Sleep"] },
    ];

    private static XElement Create(TravelMapOptions options, params TravelStop[] stops) =>
        XDocument.Parse(new TravelMapGenerator().Create(stops, options)).Root!;

    [Fact]
    public void Auto_KeepsLabelsWhenTheyFit()
    {
        var root = Create(CityHall, Zoo, Changi);

        Assert.Empty(ByClass(root, "travel-panel"));
        Assert.Contains(ByClass(root, "travel-time"), t => t.Value == "Day 1 · 9 am");
    }

    [Fact]
    public void Auto_MovesADenseItineraryToThePanel()
    {
        var root = Create(Shanghai);

        var panel = Assert.Single(ByClass(root, "travel-panel"));
        Assert.Equal(Shanghai.Length, ByClass(panel, "travel-entry").Count());

        // The map names its markers and nothing more; every time is in the panel.
        var mapLabels = ByClass(root, "travel-labels").Single();
        Assert.Empty(ByClass(mapLabels, "travel-time"));
        Assert.Empty(ByClass(mapLabels, "travel-description"));
        Assert.Equal(Shanghai.Select(s => s.Time), ByClass(panel, "travel-time").Select(t => t.Value));
    }

    [Fact]
    public void Panel_ListsEveryStopInTravelOrderWithItsNumber()
    {
        var root = Create(new TravelMapOptions { Layout = TravelMapLayout.Panel }, Changi, CityHall, Zoo, CityHall with { Sequence = 4 });
        var entries = ByClass(root, "travel-entry").ToList();

        Assert.Equal(["1", "2", "3", "4"], entries.Select(e => e.Element(Svg + "text")!.Value));
        Assert.Equal(["City Hall", "Singapore Zoo", "Changi Airport", "City Hall"],
            entries.Select(e => ByClass(e, "travel-name").First().Value));
    }

    [Theory]
    [InlineData(1024, 560)]   // landscape: the panel on the right
    [InlineData(600, 900)]    // portrait: the panel at the bottom
    public void Panel_StaysOnTheCanvasAndClearOfTheMap(int width, int height)
    {
        var options = new TravelMapOptions { Width = width, Height = height, Layout = TravelMapLayout.Panel };
        var root = Create(options, Shanghai);
        var (map, panel) = ItineraryPanel.Split(width, height);

        foreach (var (line, left, right) in LineExtents(root).Where(e => e.Line.Ancestors().Any(a => (string?)a.Attribute("class") == "travel-panel")))
        {
            var y = Number(line, "y");
            Assert.True(left >= panel.X - 0.01 && right <= panel.Right + 0.01, $"{line.Value}: {left}–{right} outside the panel");
            Assert.InRange(y, panel.Y, panel.Bottom);
        }

        foreach (var marker in ByClass(root, "travel-marker"))
        {
            var shape = marker.Elements().First();
            var x = shape.Name.LocalName == "circle" ? Number(shape, "cx") : Number(shape, "x");
            Assert.InRange(x, map.X, map.Right);
        }
    }

    [Fact]
    public void Panel_TextShrinksEvenlyToFit()
    {
        var small = Create(new TravelMapOptions { Width = 700, Height = 400, Layout = TravelMapLayout.Panel }, Shanghai);
        var panel = ByClass(small, "travel-panel").Single();
        var names = ByClass(panel, "travel-name").Select(n => Number(n, "font-size")).Distinct().ToList();
        var times = ByClass(panel, "travel-time").Select(n => Number(n, "font-size")).Distinct().ToList();

        Assert.True(Assert.Single(names) < 0.042 * 560);
        Assert.Equal(0.042 / 0.03, Assert.Single(names) / Assert.Single(times), 3);
    }

    [Fact]
    public void Markers_NeverOverlap()
    {
        foreach (var layout in new[] { TravelMapLayout.Labels, TravelMapLayout.Panel })
        {
            var root = Create(new TravelMapOptions { Layout = layout }, Shanghai);
            var boxes = ByClass(root, "travel-marker").Select(m => m.Elements().First()).Select(shape => shape.Name.LocalName == "circle"
                ? new RectD(Number(shape, "cx") - Number(shape, "r"), Number(shape, "cy") - Number(shape, "r"), 2 * Number(shape, "r"), 2 * Number(shape, "r"))
                : new RectD(Number(shape, "x"), Number(shape, "y"), Number(shape, "width"), Number(shape, "height"))).ToList();

            for (var i = 0; i < boxes.Count; i++)
            {
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    Assert.Equal(0, boxes[i].OverlapArea(boxes[j]), 6);
                }
            }
        }
    }

    [Fact]
    public void Scale_SizesTextAndMarkersButNotTheCanvas()
    {
        var normal = Create(new TravelMapOptions(), CityHall, Zoo, Changi);
        var large = Create(new TravelMapOptions { Width = 2048, Height = 1120 }, CityHall, Zoo, Changi);
        var scaled = Create(new TravelMapOptions { Width = 2048, Height = 1120, Scale = 2 }, CityHall, Zoo, Changi);

        static double Radius(XElement root) => Number(ByClass(root, "travel-marker").First().Element(Svg + "circle")!, "r");

        Assert.Equal(Radius(normal), Radius(large), 6);
        Assert.Equal(2 * Radius(normal), Radius(scaled), 6);
    }

    [Theory]
    [InlineData("Hanting Hotel 汉庭酒店(上海金沙江路地铁站店)", "Hanting|Hotel|汉|庭|酒|店|(上|海|金|沙|江|路|地|铁|站|店)")]
    [InlineData("The Ring Live 鑫耀·光环LIVE", "The|Ring|Live|鑫|耀·|光|环|LIVE")]
    [InlineData("明日方舟：终末地", "明|日|方|舟：|终|末|地")]
    [InlineData("Take Line 2 towards", "Take|Line|2|towards")]
    public void Tokens_BreakBetweenCjkCharactersAndKeepPunctuationAttached(string text, string expected) =>
        Assert.Equal(expected, string.Join('|', TravelMapWriter.Tokens(text).Select(t => t.Token)));

    [Fact]
    public void Wrap_BreaksCjkWithoutAddingSpaces()
    {
        var lines = TravelMapWriter.Wrap("汉庭酒店上海金沙江路地铁站店", 20, 20 * 6);

        Assert.True(lines.Count > 1);
        Assert.Equal("汉庭酒店上海金沙江路地铁站店", string.Concat(lines));
        Assert.DoesNotContain(lines, l => l.Contains(' ', StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsABadScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TravelMapGenerator().Create([CityHall], new TravelMapOptions { Scale = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TravelMapGenerator().Create([CityHall], new TravelMapOptions { Scale = double.NaN }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TravelMapGenerator().Create([CityHall], new TravelMapOptions { Route = (TravelMapRoute)7 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TravelMapGenerator().Create([CityHall], new TravelMapOptions { Breaks = (TravelMapBreaks)7 }));
    }

    [Fact]
    public void Create_OneStopIsAMarkerWithNoArrows()
    {
        var root = Create(CityHall);

        Assert.Single(ByClass(root, "travel-marker"));
        Assert.Empty(ByClass(root, "travel-arrow"));
    }

    [Fact]
    public void Arrow_EndsJustShortOfBothMarkers()
    {
        const double unit = 500;
        var radius = 0.026 * unit;
        var from = new TravelMapWriter.Marker([0], new PointD(0, 0), "1", radius, AnyText, radius);
        var to = new TravelMapWriter.Marker([1], new PointD(300, 400), "2", radius, AnyText, radius);

        var (start, end) = (TravelMapWriter.Arrow(from, to, unit)!.From, TravelMapWriter.Arrow(from, to, unit)!.To);

        var gap = 0.012 * unit;
        Assert.Equal(radius + gap, start.Length, 9);
        Assert.Equal(radius + gap, (new PointD(300, 400) - end).Length, 9);
    }

    [Fact]
    public void Arrow_CurvedEndsJustShortOfBothMarkersToo()
    {
        const double unit = 500;
        var radius = 0.026 * unit;
        var from = new TravelMapWriter.Marker([0], new PointD(0, 0), "1", radius, AnyText, radius);
        var to = new TravelMapWriter.Marker([1], new PointD(400, 0), "2", radius, AnyText, radius);

        var leg = TravelMapWriter.Arrow(from, to, unit, 0.12)!;

        var gap = 0.012 * unit;
        Assert.NotNull(leg.Control);
        Assert.Equal(radius + gap, leg.From.Length, 9);
        Assert.Equal(radius + gap, (new PointD(400, 0) - leg.To).Length, 9);
        Assert.True(leg.Control!.Value.Y > 0, "travelling east, it bows to the right: south, down the canvas");
    }

    [Fact]
    public void Arrow_IsLeftOutWhenTheMarkersAreTooClose()
    {
        var from = new TravelMapWriter.Marker([0], new PointD(0, 0), "1", 13, AnyText, 13);
        var to = new TravelMapWriter.Marker([1], new PointD(40, 0), "2", 13, AnyText, 13);

        Assert.Null(TravelMapWriter.Arrow(from, to, 500));
    }

    [Theory]
    [InlineData(1, 0, 30)]    // along the pill: half its width
    [InlineData(0, 1, 13)]    // straight up: its radius
    [InlineData(0.6, 0.8, 16.25)] // through the flat top: r / dy
    public void ToOutline_FollowsThePillsOutline(double dx, double dy, double expected)
    {
        // unit 500 gives radius 13; a pill 60 wide has straight sides 34 long.
        var pill = new TravelMapWriter.Marker([0, 1], default, "1 · 2", 30, AnyText, 13);

        Assert.Equal(expected, TravelMapWriter.ToOutline(pill, new PointD(dx, dy)), 9);
    }

    [Fact]
    public void ToOutline_PastTheFlatSidesMeetsTheEndCap()
    {
        var pill = new TravelMapWriter.Marker([0, 1], default, "1 · 2", 30, AnyText, 13);
        var d = new PointD(Math.Cos(0.3), Math.Sin(0.3));

        var t = TravelMapWriter.ToOutline(pill, d);

        // The point reached is a radius away from the end cap's centre (17, 0).
        Assert.Equal(13, ((d * t) - new PointD(17, 0)).Length, 9);
    }

    [Fact]
    public void Wrap_BreaksAtSpacesAndKeepsLongWordsWhole()
    {
        var width = FontMetrics.Sans.Width("Breakfast with", 10);

        Assert.Equal(["Breakfast with", "orangutans"], TravelMapWriter.Wrap("Breakfast with orangutans", 10, width));
        Assert.Equal(["Breakfast", "with", "orangutans"], TravelMapWriter.Wrap("Breakfast with orangutans", 10, width - 0.01));
        Assert.Equal(["Supercalifragilistic", "is"], TravelMapWriter.Wrap("Supercalifragilistic is", 10, 30));
        Assert.Equal(["One line"], TravelMapWriter.Wrap("  One   line ", 10, 1000));
    }

    [Fact]
    public void Validate_RejectsBadStops()
    {
        var generator = new TravelMapGenerator();
        var options = new TravelMapOptions();

        Assert.Throws<ArgumentException>(() => generator.Create([], options));
        Assert.Throws<ArgumentException>(() => generator.Create([CityHall, Zoo with { Sequence = 1 }], options));
        Assert.Throws<ArgumentException>(() => generator.Create([CityHall with { Name = " " }], options));
        Assert.Throws<ArgumentException>(() => generator.Create([CityHall with { Latitude = 89 }], options));
        Assert.Throws<ArgumentException>(() => generator.Create([CityHall with { Longitude = double.NaN }], options));
        Assert.Throws<ArgumentException>(() => generator.Create([CityHall], options with { BackgroundColor = "bluish" }));
        Assert.Throws<ArgumentException>(() => generator.Create([CityHall], options with { WaypointColor = "goldish" }));
    }
}
