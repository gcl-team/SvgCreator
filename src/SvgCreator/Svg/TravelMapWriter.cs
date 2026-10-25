using System.Xml;
using SvgCreator.Fitting;
using SvgCreator.Mapping;
using static SvgCreator.Svg.SvgFormat;
using static SvgCreator.Svg.TextWrap;

namespace SvgCreator.Svg;

/// <summary>
/// Lays out a travel map: the stops are projected (<see cref="WebMercator"/>) and fitted to the
/// canvas (<see cref="MapFit"/>); each place visited gets one marker (<see cref="Places"/>), nudged
/// clear of its neighbours; arrows join the markers in travel order; and each marker's stops
/// are described in a text block beside it, or named there and described in an
/// <see cref="ItineraryPanel"/> beside the map. Every size is a fraction of a fixed unit
/// (<see cref="ReferenceUnit"/> times <see cref="TravelMapOptions.Scale"/>), not of the canvas:
/// a bigger canvas gives the stops more room rather than bigger text. Values are written with
/// <see cref="SvgFormat.Number"/>.
/// </summary>
internal static class TravelMapWriter
{
    /// <summary>
    /// The unit every size is a fraction of, at scale 1, in pixels. It is the default canvas's
    /// shorter side, so the default map's text and markers are sized for 1024 × 560.
    /// </summary>
    internal const double ReferenceUnit = 560;

    internal const double Margin = 0.05;            // canvas edge to anything drawn
    internal const double MarkerRadius = 0.026;
    internal const double WaypointRadius = 0.01;   // a waypoint's dot
    internal const double WaypointNameSize = 0.028;
    private const double WaypointGap = 0.05;       // the least space beside a waypoint: its legs have no head
    internal const double MarkerTextSize = 0.03;
    private const double ArrowWidth = 0.008;
    private const double ArrowGap = 0.012;         // between a marker's edge and an arrow's end
    private const double ArrowHeadLength = 0.04;
    private const double ArrowHeadHalfWidth = 0.022;
    private const double LegBow = 0.06;            // a curved leg's height at its middle, per repeat, as a fraction of its length
    private const double ArrowMarkerGap = 0.09;    // the least space between two markers' outlines, with arrows (see MapStyle)
    private const double MinLineLength = 0.02;     // the shortest headless line worth drawing

    // A broken leg (see ScaleBreaks): where along it the dots start and end, their spacing in
    // stroke widths, and the halo behind its distance label.
    private const double BreakStart = 0.3;
    private const double BreakEnd = 0.7;
    private const double BreakDotSpacing = 2.5;
    private const double DistanceHalo = 0.012;
    internal const double NameSize = 0.042;
    internal const double TimeSize = 0.03;
    internal const double DescriptionSize = 0.028;
    private const double WrapWidth = 0.45;         // a block's lines wrap at this width
    private const double LabelGap = 0.02;          // between a marker and the text under it
    internal const double VisitGap = 0.012;         // between the visits listed in one place's block

    // Baseline below the top of a line, and a line's height, in em.
    internal const double Ascent = 0.8;
    internal const double LineHeight = 1.2;

    // Fitting the map to its labels (see Layout): rounds of pulling the map in from edges that
    // labels overrun, the least the map may shrink to, and how heavily a label off the canvas
    // counts against a spot while the map can still make room for it (see LayoutAt).
    private const int FitRounds = 8;
    private const double MinMapFraction = 0.5;
    private static readonly double[] OffCanvasWeights = [1, 3];

    // Shrinking text (see Layout): rounds for text too big for the canvas; for text that collides,
    // the step and the smallest it may get, as a fraction of full size.
    private const int TextFitRounds = 3;
    private const double TextShrinkStep = 0.9;
    private const double MinTextScale = 0.6;

    // Rounds of nudging touching markers apart (see Markers).
    private const int NudgeRounds = 100;

    // Between the sequence numbers of stops sharing a marker.
    private const string GroupSeparator = " · ";

    /// <summary>A line of a block's text.</summary>
    /// <param name="ClassName">Its SVG class: what the line is (name, time or description).</param>
    /// <param name="Text">The text.</param>
    /// <param name="Size">Font size.</param>
    /// <param name="Bold">Whether it is bold.</param>
    /// <param name="SpaceBefore">Extra space above the line: set where a new visit starts in a block.</param>
    internal sealed record Line(string ClassName, string Text, double Size, bool Bold, double SpaceBefore = 0)
    {
        public double Height => SpaceBefore + (Size * LineHeight);
    }

    /// <summary>
    /// One place's text: its name, then each visit's time and description. A place visited more
    /// than once from the same marker gets one block, its name printed once.
    /// </summary>
    internal sealed record Block(IReadOnlyList<Line> Lines, double Width, double Height);

    /// <summary>A marker on the canvas: its stops, the label inside it, and its shape.</summary>
    /// <param name="Stops">Indexes into the stops sorted by sequence.</param>
    /// <param name="Center">Where the marker sits on the canvas.</param>
    /// <param name="Label">The text inside it: its visits' place numbers (empty for a waypoint).</param>
    /// <param name="HalfWidth">Half the marker's width: its radius for a circle, more for a pill holding several numbers.</param>
    /// <param name="Block">Its place's text.</param>
    /// <param name="Radius">Half its height: a numbered marker's radius, or a waypoint's small dot.</param>
    /// <param name="Waypoint">A waypoint's dot rather than a numbered place.</param>
    internal sealed record Marker(IReadOnlyList<int> Stops, PointD Center, string Label, double HalfWidth, Block Block, double Radius, bool Waypoint = false)
    {
        public RectD Bounds => new(Center.X - HalfWidth, Center.Y - Radius, 2 * HalfWidth, 2 * Radius);
    }

    public static string Write(IReadOnlyList<TravelStop> stops, TravelMapOptions options)
    {
        var (width, height) = (options.Width, options.Height);
        var unit = ReferenceUnit * options.Scale;
        var ordered = stops.OrderBy(s => s.Sequence).ToList();
        var foreground = options.ForegroundColor;

        // Auto keeps the text beside the markers when it fits there without colliding, and moves
        // it to the itinerary panel when it doesn't. Names leaves it out.
        var whole = new RectD(0, 0, width, height);
        MapLayout? layout = null;
        ItineraryPanel.PanelLayout? panel = null;
        if (options.Layout == TravelMapLayout.Names)
        {
            layout = Layout(ordered, whole, unit, new MapStyle(NamesOnly: true, Route: options.Route, Breaks: options.Breaks == TravelMapBreaks.Auto));
        }
        else if (options.Layout != TravelMapLayout.Panel)
        {
            layout = Layout(ordered, whole, unit, new MapStyle(Route: options.Route, Breaks: options.Breaks == TravelMapBreaks.Auto));
        }

        if (options.Layout == TravelMapLayout.Panel || (options.Layout == TravelMapLayout.Auto && Collides(layout!, unit)))
        {
            var (mapFrame, panelFrame) = ItineraryPanel.Split(width, height);
            layout = Layout(ordered, mapFrame, unit, new MapStyle(NamesOnly: true, Route: options.Route, Breaks: options.Breaks == TravelMapBreaks.Auto));
            panel = ItineraryPanel.Layout(ordered, panelFrame, unit);
        }

        return SvgWriter.WriteDocument(width, height, foreground, SvgStyle.Clean, (xml, _) =>
        {
            xml.WriteStartElement("rect", Namespace);
            xml.WriteAttributeString("class", "travel-background");
            xml.WriteAttributeString("width", Integer(width));
            xml.WriteAttributeString("height", Integer(height));
            xml.WriteAttributeString("fill", options.BackgroundColor);
            xml.WriteEndElement();

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "travel-arrows");
            xml.WriteAttributeString("fill", foreground);
            xml.WriteAttributeString("stroke", foreground);
            xml.WriteAttributeString("stroke-width", Number(ArrowWidth * unit));
            xml.WriteAttributeString("stroke-linecap", "round");
            xml.WriteAttributeString("stroke-linejoin", "round");
            foreach (var arrow in layout!.Arrows)
            {
                WriteArrow(xml, arrow, unit, withHead: arrow.Head);
            }

            xml.WriteEndElement();

            // Each break's real distance, on the middle of its dots, with a halo of the background
            // so the dots don't run through the text.
            if (layout.Arrows.Any(a => a.Distance is not null))
            {
                xml.WriteStartElement("g", Namespace);
                xml.WriteAttributeString("class", "travel-distances");
                xml.WriteAttributeString("fill", foreground);
                xml.WriteAttributeString("stroke", options.BackgroundColor);
                xml.WriteAttributeString("stroke-width", Number(DistanceHalo * unit));
                xml.WriteAttributeString("stroke-linejoin", "round");
                xml.WriteAttributeString("paint-order", "stroke");
                xml.WriteAttributeString("font-family", FontMetrics.Sans.Family);
                xml.WriteAttributeString("font-size", Number(TimeSize * unit));
                xml.WriteAttributeString("text-anchor", "middle");
                foreach (var arrow in layout.Arrows.Where(a => a.Distance is not null))
                {
                    var bounds = DistanceBounds(arrow, unit);
                    xml.WriteStartElement("text", Namespace);
                    xml.WriteAttributeString("class", "travel-distance");
                    xml.WriteAttributeString("x", Number(bounds.Center.X));
                    xml.WriteAttributeString("y", Number(bounds.Y + (TimeSize * unit * Ascent)));
                    xml.WriteString(arrow.Distance);
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
            }

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "travel-markers");
            xml.WriteAttributeString("font-family", FontMetrics.Sans.Family);
            xml.WriteAttributeString("font-weight", "bold");
            foreach (var marker in layout.Markers)
            {
                WriteMarker(xml, marker, unit, marker.Waypoint ? options.WaypointColor : foreground, options.BackgroundColor);
            }

            xml.WriteEndElement();

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "travel-labels");
            xml.WriteAttributeString("fill", foreground);
            xml.WriteAttributeString("font-family", FontMetrics.Sans.Family);
            foreach (var label in layout.Labels)
            {
                WriteBlock(xml, label.Block, label.Bounds, label.Anchor, label.Waypoint ? options.WaypointColor : null);
            }

            xml.WriteEndElement();

            if (panel is not null)
            {
                ItineraryPanel.Write(xml, panel, unit, foreground, options.BackgroundColor, options.WaypointColor);
            }
        });
    }

    internal enum TextAnchor
    {
        Start,
        Middle,
        End,
    }

    /// <summary>One block of text on the canvas.</summary>
    /// <param name="Block">The text.</param>
    /// <param name="Bounds">Where the block sits.</param>
    /// <param name="Anchor">How its lines align within it.</param>
    /// <param name="Side">Which side of its marker it went to.</param>
    /// <param name="Waypoint">A waypoint's name, set in the waypoint colour.</param>
    internal sealed record Label(Block Block, RectD Bounds, TextAnchor Anchor, LabelSide Side, bool Waypoint = false);

    /// <summary>
    /// An arrow from one marker to the next: straight, or a quadratic curve through
    /// <paramref name="Control"/>.
    /// </summary>
    /// <param name="From">Where it starts, just short of the first marker.</param>
    /// <param name="To">Its tip, just short of the second.</param>
    /// <param name="Control">The curve's control point; null for a straight arrow.</param>
    /// <param name="Distance">For a leg to a far stop pulled in (see <see cref="ScaleBreaks"/>): its real length, labelled on its dotted middle.</param>
    /// <param name="Head">Whether it ends in an arrowhead: on arrival at a place, with arrows drawn.</param>
    internal sealed record Leg(PointD From, PointD To, PointD? Control = null, string? Distance = null, bool Head = true)
    {
        private const int CurveSegments = 12;

        /// <summary>The point at <paramref name="t"/> (0 at its start, 1 at its tip).</summary>
        public PointD At(double t) => Control is { } c
            ? (From * ((1 - t) * (1 - t))) + (c * (2 * t * (1 - t))) + (To * (t * t))
            : From + ((To - From) * t);

        /// <summary>
        /// The part from <paramref name="t0"/> to <paramref name="t1"/>, as SVG path data. A part of
        /// a quadratic is a quadratic, whose control point is the blossom (1−t0)(1−t1)·P0 +
        /// (t0(1−t1) + t1(1−t0))·C + t0·t1·P2.
        /// </summary>
        public string PathData(double t0, double t1)
        {
            var (start, end) = (At(t0), At(t1));
            if (Control is not { } c)
            {
                return $"M{Point(start)}L{Point(end)}";
            }

            var control = (From * ((1 - t0) * (1 - t1))) + (c * ((t0 * (1 - t1)) + (t1 * (1 - t0)))) + (To * (t0 * t1));
            return $"M{Point(start)}Q{Point(control)} {Point(end)}";
        }

        /// <summary>The direction it arrives in at its tip.</summary>
        public PointD EndDirection => (To - (Control ?? From)).Normalize();

        /// <summary>Its path as straight segments (a curve flattened), for measuring what it crosses.</summary>
        public IEnumerable<(PointD From, PointD To)> Segments()
        {
            if (Control is not { } c)
            {
                yield return (From, To);
                yield break;
            }

            var previous = From;
            for (var i = 1; i <= CurveSegments; i++)
            {
                var t = (double)i / CurveSegments;
                var point = (From * ((1 - t) * (1 - t))) + (c * (2 * t * (1 - t))) + (To * (t * t));
                yield return (previous, point);
                previous = point;
            }
        }
    }

    /// <summary>How the map is drawn: what its labels say, and how the route between places is shown.</summary>
    /// <param name="NamesOnly">Label markers with their places' names only (the panel has the rest).</param>
    /// <param name="Route">Arrows, lines without heads, or no route at all.</param>
    /// <param name="Breaks">Pull far stops in, breaking the legs to them (see <see cref="ScaleBreaks"/>).</param>
    internal sealed record MapStyle(bool NamesOnly = false, TravelMapRoute Route = TravelMapRoute.Arrows, bool Breaks = true)
    {
        /// <summary>
        /// The least space between markers' outlines, as a fraction of the unit: room for a visible
        /// arrow between neighbours, less for a line with no head, and just clear for no route.
        /// </summary>
        public double MarkerGap => Route switch
        {
            TravelMapRoute.Arrows => ArrowMarkerGap,
            TravelMapRoute.Lines => 0.05,
            _ => 0.03,
        };

    }

    internal sealed record MapLayout(
        IReadOnlyList<Marker> Markers,
        IReadOnlyList<Leg> Arrows,
        IReadOnlyList<Label> Labels);

    /// <summary>
    /// Fits the map so its labels and markers stay on the canvas. The stops first span the whole
    /// canvas (less the margin and a marker's radius); wherever a label or a pill still runs off an
    /// edge, the map pulls in from that edge by the overrun and is laid out again. The map never
    /// shrinks below <see cref="MinMapFraction"/> of the canvas; past that, labels that still
    /// overrun are moved on. Text too big for the canvas at any map size shrinks first.
    /// </summary>
    /// <param name="ordered">The stops, sorted by sequence.</param>
    /// <param name="frame">The part of the canvas the map fills: all of it, or what the itinerary panel leaves.</param>
    /// <param name="unit">The size unit (<see cref="ReferenceUnit"/> times the scale), which every size is a fraction of.</param>
    /// <param name="style">What the labels say and how the route is drawn.</param>
    internal static MapLayout Layout(IReadOnlyList<TravelStop> ordered, RectD frame, double unit, MapStyle style)
    {
        // Text that can't fit at full size anywhere (a block wider or taller than the canvas) shrinks, all of it evenly, so the label reads as one: wrapping scales with
        // the text, so lines break in the same places and every size scales exactly. Shrinking can
        // change which stops share a marker, and so the rows, hence a few rounds.
        var canvas = frame.Inset(Margin * unit);
        var text = unit;
        var layout = LayoutAt(ordered, frame, unit, text, style);
        for (var round = 0; round < TextFitRounds; round++)
        {
            var overflow = layout.Markers.Max(m => Math.Max(m.Block.Width / canvas.Width, m.Block.Height / canvas.Height));
            if (overflow <= 1)
            {
                break;
            }

            text /= overflow;
            layout = LayoutAt(ordered, frame, unit, text, style);
        }

        // Text that fits the canvas can still be too much for it: a dense itinerary leaves labels
        // nowhere to go but over each other or the arrows. Then all the text shrinks a step at a
        // time until nothing collides, but no smaller than MinTextScale of full size: past that it
        // stops being readable, and the canvas needs to be bigger instead. The layout with the
        // fewest collisions wins; on a tie, the larger text.
        var (best, collisions) = (layout, Collisions(layout, unit));
        while (Collides(collisions, unit) && text * TextShrinkStep >= unit * MinTextScale)
        {
            text *= TextShrinkStep;
            var smaller = LayoutAt(ordered, frame, unit, text, style);
            var c = Collisions(smaller, unit);
            if (c < collisions)
            {
                (best, collisions) = (smaller, c);
            }
        }

        return best;
    }

    /// <summary>
    /// How badly a layout's text collides: the area each block overlaps other blocks and markers,
    /// plus the length of arrow (as wide as its stroke) running through it. Zero when every label
    /// is clear.
    /// </summary>
    internal static bool Collides(MapLayout layout, double unit) => Collides(Collisions(layout, unit), unit);

    // Label bounds are estimated wide (see FontMetrics), so a sliver of overlap is usually none
    // at all on screen. Less than a square of the label gap doesn't count.
    private static bool Collides(double collisions, double unit) => collisions > Math.Pow(LabelGap * unit, 2);

    internal static double Collisions(MapLayout layout, double unit)
    {
        var stroke = ArrowWidth * unit;
        var total = 0.0;
        for (var i = 0; i < layout.Labels.Count; i++)
        {
            var bounds = layout.Labels[i].Bounds;
            for (var j = i + 1; j < layout.Labels.Count; j++)
            {
                total += bounds.OverlapArea(layout.Labels[j].Bounds);
            }

            total += layout.Markers.Sum(m => bounds.OverlapArea(m.Bounds));
            var padded = bounds.Inflate(stroke / 2);
            total += layout.Arrows.SelectMany(a => a.Segments()).Sum(a => LabelPlacer.ClippedLength(a.From, a.To, padded) * stroke);
            total += layout.Arrows.Where(a => a.Distance is not null).Sum(a => bounds.OverlapArea(DistanceBounds(a, unit)));
        }

        return total;
    }


    /// <summary>
    /// Lays the map out with text sized by <paramref name="text"/> (the unit, at full size), once
    /// for each weight of going off the canvas while the map can still shrink, and keeps the
    /// layout with the fewest collisions, then the larger map. No one weight suits every trip. A
    /// light one sends labels off the canvas rather than over arrows, which shrinking fixes. But a
    /// label that runs far off the edge pulls the map in by all of its overrun, and on a busy trip
    /// the next label then runs off the other side, until the map is half its size. A heavier
    /// weight keeps the map large and accepts an arrow crossing a label.
    /// </summary>
    private static MapLayout LayoutAt(IReadOnlyList<TravelStop> ordered, RectD frame, double unit, double text, MapStyle style) =>
        OffCanvasWeights
            .Select(weight => LayoutWith(ordered, frame, unit, text, style, weight))
            .OrderBy(fit => Collisions(fit.Layout, unit))
            .ThenByDescending(fit => fit.MapArea)
            .First().Layout;

    private static (MapLayout Layout, double MapArea) LayoutWith(
        IReadOnlyList<TravelStop> ordered, RectD frame, double unit, double text, MapStyle style, double offCanvasWeight)
    {
        var margin = Margin * unit;
        var canvas = frame.Inset(margin);
        var full = canvas.Inset(MarkerRadius * unit);
        var raw = ordered.Select(s => WebMercator.Project(s.Latitude, s.Longitude)).ToList();
        var (projected, groups) = style.Breaks ? ScaleBreaks.Apply(raw) : ([.. raw], new int[raw.Count]);

        var area = full;
        for (var round = 0; round < FitRounds; round++)
        {
            var (markers, arrows) = Markers(ordered, projected, groups, area, unit, text, style);
            var placements = PlaceLabels(markers, arrows, canvas, unit, text, offCanvasWeight);
            // Labels, and markers too: a pill for several stops is wider than the circle the area
            // was inset for.
            var (left, top, right, bottom) = (0.0, 0.0, 0.0, 0.0);
            foreach (var bounds in placements.Select(p => p.Bounds).Concat(markers.Select(m => m.Bounds)))
            {
                left = Math.Max(left, canvas.X - bounds.X);
                top = Math.Max(top, canvas.Y - bounds.Y);
                right = Math.Max(right, bounds.Right - canvas.Right);
                bottom = Math.Max(bottom, bounds.Bottom - canvas.Bottom);
            }

            if (Math.Max(Math.Max(left, top), Math.Max(right, bottom)) < 0.5)
            {
                return (Labelled(markers, arrows, placements), area.Width * area.Height);
            }

            var next = new RectD(area.X + left, area.Y + top, area.Width - left - right, area.Height - top - bottom);
            if (next.Width < full.Width * MinMapFraction || next.Height < full.Height * MinMapFraction)
            {
                break;
            }

            area = next;
        }

        // The map can shrink no further for its labels, but markers must stay on the canvas: a
        // wide pill pulls the map in past the floor if it has to. Labels that would still overrun
        // are moved onto the canvas, each spot judged where its label would end up.
        var (lastMarkers, lastArrows) = Markers(ordered, projected, groups, area, unit, text, style);
        for (var round = 0; round < FitRounds; round++)
        {
            var left = lastMarkers.Max(m => canvas.X - m.Bounds.X);
            var right = lastMarkers.Max(m => m.Bounds.Right - canvas.Right);
            if (Math.Max(left, right) < 0.5)
            {
                break;
            }

            area = new RectD(area.X + Math.Max(0, left), area.Y, Math.Max(0, area.Width - Math.Max(0, left) - Math.Max(0, right)), area.Height);
            (lastMarkers, lastArrows) = Markers(ordered, projected, groups, area, unit, text, style);
        }

        var last = PlaceLabels(lastMarkers, lastArrows, canvas, unit, text, offCanvasWeight, keepOnCanvas: true);
        return (Labelled(lastMarkers, lastArrows, last), area.Width * area.Height);
    }

    /// <summary>
    /// Each stop's number on the map and in the panel: places are numbered 1, 2, 3… in travel
    /// order, skipping waypoints, which have none.
    /// </summary>
    internal static int?[] Numbers(IReadOnlyList<TravelStop> ordered)
    {
        var numbers = new int?[ordered.Count];
        var next = 1;
        for (var i = 0; i < ordered.Count; i++)
        {
            numbers[i] = ordered[i].Waypoint ? null : next++;
        }

        return numbers;
    }

    /// <summary>A place on the map: the stops that visit it, and where its marker goes.</summary>
    /// <param name="Members">Indexes of its stops, ascending (stops are indexed in travel order).</param>
    /// <param name="Center">The mean of its stops' canvas positions.</param>
    internal sealed record Place(IReadOnlyList<int> Members, PointD Center);

    /// <summary>
    /// The trip's places: stops are the same place when their names match, ignoring case and
    /// surrounding spaces, and both are waypoints or neither is; in order of each place's first
    /// visit. Places are never merged by distance: a marker holding two different places can't
    /// say which number is which.
    /// </summary>
    internal static List<Place> Places(IReadOnlyList<TravelStop> ordered, IReadOnlyList<PointD> positions) =>
        [.. Enumerable.Range(0, ordered.Count)
            .GroupBy(i => (ordered[i].Waypoint ? "waypoint:" : "place:") + ordered[i].Name.Trim().ToUpperInvariant())
            .Select(g => new Place([.. g], g.Aggregate(default(PointD), (sum, i) => sum + positions[i]) * (1.0 / g.Count())))];

    private static (List<Marker> Markers, List<Leg> Arrows) Markers(
        IReadOnlyList<TravelStop> ordered, IReadOnlyList<PointD> projected, int[] groups, RectD area, double unit, double text, MapStyle style)
    {
        var positions = MapFit.Fit(projected, area);
        var numbers = Numbers(ordered);
        var markers = Places(ordered, positions).Select(p => MarkerFor(p, ordered, numbers, unit, text, style.NamesOnly)).ToList();

        // Places close together on the map would draw crowded markers. Markers closer than the
        // style's MarkerGap, outline to outline, are nudged apart, each moving half the shortfall along
        // the axis it is smaller on (for pills, nearly always up and down, so they stack). The gap
        // leaves room for a visible leg between neighbours. A nudge moves a marker only by the
        // shortfall, so distant places stay true to scale; merging close markers would snowball
        // instead, each merge widening the pill and moving it until it catches the next.
        // A waypoint's legs have no arrowhead to make room for, so pairs with one need less.
        double Clearance(Marker a, Marker b) => (a.Waypoint || b.Waypoint ? Math.Min(style.MarkerGap, WaypointGap) : style.MarkerGap) * unit;
        for (var round = 0; round < NudgeRounds; round++)
        {
            var moved = false;
            for (var a = 0; a < markers.Count; a++)
            {
                for (var b = a + 1; b < markers.Count; b++)
                {
                    var clearance = Clearance(markers[a], markers[b]);
                    var (ra, rb) = (markers[a].Bounds.Inflate(clearance / 2), markers[b].Bounds.Inflate(clearance / 2));
                    var overlapX = Math.Min(ra.Right, rb.Right) - Math.Max(ra.X, rb.X);
                    var overlapY = Math.Min(ra.Bottom, rb.Bottom) - Math.Max(ra.Y, rb.Y);
                    if (overlapX <= 0 || overlapY <= 0)
                    {
                        continue;
                    }

                    // The lower-numbered marker goes left or up when the centres are level.
                    var delta = markers[b].Center - markers[a].Center;
                    var push = overlapX < overlapY
                        ? new PointD((delta.X < 0 ? -1 : 1) * overlapX / 2, 0)
                        : new PointD(0, (delta.Y < 0 ? -1 : 1) * overlapY / 2);
                    markers[a] = markers[a] with { Center = markers[a].Center - push };
                    markers[b] = markers[b] with { Center = markers[b].Center + push };
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }

        markers.Sort((x, y) => x.Stops[0].CompareTo(y.Stops[0]));
        var markerOf = new int[ordered.Count];
        for (var m = 0; m < markers.Count; m++)
        {
            foreach (var stop in markers[m].Stops)
            {
                markerOf[stop] = m;
            }
        }

        // Consecutive visits to the same place need no arrow; with no route, nothing does.
        var legs = new List<(int From, int To, int Stop)>();
        for (var i = 0; style.Route != TravelMapRoute.None && i + 1 < ordered.Count; i++)
        {
            if (markerOf[i] != markerOf[i + 1])
            {
                legs.Add((markerOf[i], markerOf[i + 1], i));
            }
        }

        // A single leg between two places is straight. Several (there and back, or the same trip
        // twice) curve, each bowing to its own right: going and coming back bow to opposite sides
        // and form a lens, and each repeat in the same direction bows wider than the one before.
        var arrows = new List<Leg>();
        var shared = legs.GroupBy(l => (Math.Min(l.From, l.To), Math.Max(l.From, l.To))).ToDictionary(g => g.Key, g => g.Count());
        var repeats = new Dictionary<(int, int), int>();
        foreach (var (from, to, stop) in legs)
        {
            var repeat = repeats[(from, to)] = repeats.GetValueOrDefault((from, to)) + 1;
            var bow = shared[(Math.Min(from, to), Math.Max(from, to))] > 1 ? LegBow * repeat : 0;

            // The route runs through waypoints like a metro line: only arriving at a place gets a head.
            var head = style.Route == TravelMapRoute.Arrows && !markers[to].Waypoint;
            if (Arrow(markers[from], markers[to], unit, bow, head ? ArrowHeadLength : MinLineLength) is { } leg)
            {
                // A leg between a far group and the rest isn't to scale: it gets a break, labelled
                // with its real length.
                var (a, b) = (ordered[stop], ordered[stop + 1]);
                var arrow = leg with { Head = head };
                arrows.Add(groups[stop] == groups[stop + 1] ? arrow : arrow with
                {
                    Distance = ScaleBreaks.FormatDistance(ScaleBreaks.DistanceKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude)),
                });
            }
        }

        return (markers, arrows);
    }

    private static LabelPlacement[] PlaceLabels(
        IReadOnlyList<Marker> markers, IReadOnlyList<Leg> arrows,
        RectD canvas, double unit, double text, double offCanvasWeight, bool keepOnCanvas = false)
    {
        var requests = markers.Select(m => new LabelRequest(
            m.Center, m.HalfWidth, m.Radius,
            m.Block.Width, m.Block.Height, m.Block.Lines[0].Height)).ToList();
        return LabelPlacer.Place(
            requests, [.. arrows.SelectMany(a => a.Segments())], ArrowWidth * unit, LabelGap * unit, canvas, offCanvasWeight, keepOnCanvas,
            [.. arrows.Where(a => a.Distance is not null).Select(a => DistanceBounds(a, unit))]);
    }

    // Each marker's block in its placed bounds, its text aligned toward the marker: right-aligned
    // left of it, left-aligned right of it, centred above or below.
    private static MapLayout Labelled(List<Marker> markers, List<Leg> arrows, LabelPlacement[] placements)
    {
        var labels = new List<Label>();
        for (var m = 0; m < markers.Count; m++)
        {
            var (side, bounds) = placements[m];
            var anchor = side switch
            {
                LabelSide.Left or LabelSide.BelowLeft or LabelSide.AboveLeft => TextAnchor.End,
                LabelSide.Right or LabelSide.BelowRight or LabelSide.AboveRight => TextAnchor.Start,
                _ => TextAnchor.Middle,
            };
            labels.Add(new Label(markers[m].Block, bounds, anchor, side, markers[m].Waypoint));
        }

        return new MapLayout(markers, arrows, labels);
    }

    /// <summary>
    /// A place's block: its name (as first given), then each visit's time and description in
    /// travel order, a little space between one visit and the next; or just the name. A waypoint's
    /// name is set smaller, like a station on a metro map, and its details smaller still.
    /// </summary>
    internal static Block BlockFor(IReadOnlyList<TravelStop> visits, double unit, bool namesOnly = false)
    {
        var wrap = WrapWidth * unit;
        var lines = new List<Line>();
        var waypoint = visits[0].Waypoint;
        var (nameClass, nameSize, timeSize) = waypoint
            ? ("travel-waypoint-name", WaypointNameSize * unit, DescriptionSize * unit)
            : ("travel-name", NameSize * unit, TimeSize * unit);
        lines.AddRange(Wrap(visits[0].Name, nameSize, wrap).Select(t => new Line(nameClass, t, nameSize, Bold: true)));
        for (var v = 0; v < (namesOnly ? 0 : visits.Count); v++)
        {
            var visit = visits[v];
            var start = lines.Count;
            if (!string.IsNullOrWhiteSpace(visit.Time))
            {
                lines.AddRange(Wrap(visit.Time, timeSize, wrap).Select(t => new Line("travel-time", t, timeSize, Bold: false)));
            }

            foreach (var description in visit.Description)
            {
                lines.AddRange(Wrap(description, DescriptionSize * unit, wrap).Select(t => new Line("travel-description", t, DescriptionSize * unit, Bold: false)));
            }

            // The first visit follows the name directly; each later one is set apart from the last.
            if (v > 0 && lines.Count > start)
            {
                lines[start] = lines[start] with { SpaceBefore = VisitGap * unit };
            }
        }

        return Measure(lines);
    }

    internal static Block Measure(IReadOnlyList<Line> lines) =>
        new(lines, lines.Max(l => FontMetrics.Sans.Width(l.Text, l.Size)), lines.Sum(l => l.Height));

    private static Marker MarkerFor(Place group, IReadOnlyList<TravelStop> ordered, int?[] numbers, double unit, double text, bool namesOnly)
    {
        var visits = group.Members.Select(i => ordered[i]).ToList();
        if (visits[0].Waypoint)
        {
            var dot = WaypointRadius * unit;
            return new Marker(group.Members, group.Center, "", dot, BlockFor(visits, text, namesOnly), dot, Waypoint: true);
        }

        var radius = MarkerRadius * unit;
        var label = string.Join(GroupSeparator, group.Members.Select(i => numbers[i]));

        // A circle while the number fits in it; wider labels stretch it into a pill, keeping the
        // circle's padding at each end.
        var textWidth = FontMetrics.Sans.Width(label, MarkerTextSize * unit);
        var halfWidth = Math.Max(radius, (textWidth / 2) + (radius * 0.55));
        return new Marker(group.Members, group.Center, label, halfWidth, BlockFor(visits, text, namesOnly), radius);
    }

    /// <summary>
    /// The arrow from one marker to the next, both ends pulled back to just short of the markers'
    /// outlines; <see langword="null"/> when the markers are too close for a visible arrow. A
    /// nonzero <paramref name="bow"/> curves it to its right: the curve's height at the middle, as
    /// a fraction of the distance between the centres. A curve leaves and arrives at an angle, so
    /// its ends are pulled back along those directions.
    /// </summary>
    internal static Leg? Arrow(Marker from, Marker to, double unit, double bow = 0, double minLength = ArrowHeadLength)
    {
        var delta = to.Center - from.Center;
        var length = delta.Length;
        var direction = delta.Normalize();
        var gap = ArrowGap * unit;

        // The quadratic's control point sits twice the bow's height out, so the curve's middle
        // reaches the bow. Right of the direction of travel, with y down, is (−dy, dx).
        PointD? control = bow == 0 ? null : from.Center + (delta * 0.5) + (new PointD(-direction.Y, direction.X) * (2 * bow * length));
        var leave = ((control ?? to.Center) - from.Center).Normalize();
        var arrive = (to.Center - (control ?? from.Center)).Normalize();
        // A waypoint's dot is drawn over the route, so legs run right to its centre and join up
        // through it, like a metro line through a station.
        var start = from.Waypoint ? from.Center : from.Center + (leave * (ToOutline(from, leave) + gap));
        var end = to.Waypoint ? to.Center : to.Center - (arrive * (ToOutline(to, -arrive) + gap));
        if ((end - start).Length < minLength * unit || (end - start).Dot(direction) <= 0)
        {
            return null;
        }

        return new Leg(start, end, control);
    }

    /// <summary>
    /// Distance from a marker's centre to its outline along unit vector <paramref name="d"/>. The
    /// outline is a capsule: a segment of half-length <c>a</c> along x swept by the radius (a = 0
    /// is the circle). Within the straight sides the ray meets them at r / |dy|; past them it meets
    /// the end cap, solving |t·d − (a, 0)| = r.
    /// </summary>
    internal static double ToOutline(Marker marker, PointD d)
    {
        var r = marker.Radius;
        var a = marker.HalfWidth - r;
        var (dx, dy) = (Math.Abs(d.X), Math.Abs(d.Y));
        if (dy > 0 && r / dy * dx <= a)
        {
            return r / dy;
        }

        return (a * dx) + Math.Sqrt(Math.Max(0, (r * r) - (a * a * dy * dy)));
    }

    private static void WriteArrow(XmlWriter xml, Leg arrow, double unit, bool withHead)
    {
        var direction = arrow.EndDirection;
        var normal = new PointD(-direction.Y, direction.X);
        var headBase = arrow.To - (direction * (ArrowHeadLength * unit));
        var wing = normal * (ArrowHeadHalfWidth * unit);

        // The shaft stops inside the head so its round cap can't poke out of the tip. A curved
        // shaft keeps its control point: over the head's short length the curve barely bends.
        // Shaft and head are separate paths: the head is filled, and a curved shaft must not be,
        // or the area between the curve and its chord fills in.
        var shaft = arrow with { To = withHead ? headBase : arrow.To };
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "travel-arrow");

        // A broken leg is solid at both ends, so it still leaves and arrives in the right
        // directions, and dotted in between: round caps on zero-length dashes draw the dots.
        xml.WriteStartElement("path", Namespace);
        xml.WriteAttributeString("class", "travel-arrow-shaft");
        xml.WriteAttributeString("d", arrow.Distance is null ? shaft.PathData(0, 1) : shaft.PathData(0, BreakStart) + shaft.PathData(BreakEnd, 1));
        xml.WriteAttributeString("fill", "none");
        xml.WriteEndElement();

        if (arrow.Distance is not null)
        {
            xml.WriteStartElement("path", Namespace);
            xml.WriteAttributeString("class", "travel-arrow-break");
            xml.WriteAttributeString("d", shaft.PathData(BreakStart, BreakEnd));
            xml.WriteAttributeString("fill", "none");
            xml.WriteAttributeString("stroke-dasharray", $"0 {Number(BreakDotSpacing * ArrowWidth * unit)}");
            xml.WriteEndElement();
        }

        if (withHead)
        {
            xml.WriteStartElement("path", Namespace);
            xml.WriteAttributeString("class", "travel-arrow-head");
            xml.WriteAttributeString("d", $"M{Point(arrow.To)}L{Point(headBase + wing)}L{Point(headBase - wing)}Z");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static string Point(PointD p) => $"{Number(p.X)} {Number(p.Y)}";

    // Where a broken leg's distance label sits: centred on the middle of its dots, its halo
    // hiding the dots behind it. When the dotted stretch is too short to show dots on both sides
    // of the label (a short leg on a small map), the label moves beside the line instead, on its
    // upper side, clear of the stroke.
    private static RectD DistanceBounds(Leg arrow, double unit)
    {
        var size = TimeSize * unit;
        var (width, height) = (FontMetrics.Sans.Width(arrow.Distance!, size), size * LineHeight);
        var middle = arrow.At((BreakStart + BreakEnd) / 2);
        var dotted = (arrow.At(BreakEnd) - arrow.At(BreakStart)).Length;
        if (dotted < width + (4 * BreakDotSpacing * ArrowWidth * unit))
        {
            var along = (arrow.At(BreakEnd) - arrow.At(BreakStart)).Normalize(new PointD(1, 0));
            var normal = new PointD(-along.Y, along.X);
            normal = normal.Y > 0 ? -normal : normal;

            // Far enough out that the box, whichever way the line runs, clears the stroke.
            var reach = (Math.Abs(normal.X) * width / 2) + (Math.Abs(normal.Y) * height / 2) + (ArrowWidth * unit);
            middle += normal * reach;
        }

        return new RectD(middle.X - (width / 2), middle.Y - (height / 2), width, height);
    }

    private static void WriteMarker(XmlWriter xml, Marker marker, double unit, string fill, string textColor)
    {
        if (marker.Waypoint)
        {
            xml.WriteStartElement("circle", Namespace);
            xml.WriteAttributeString("class", "travel-waypoint");
            xml.WriteAttributeString("cx", Number(marker.Center.X));
            xml.WriteAttributeString("cy", Number(marker.Center.Y));
            xml.WriteAttributeString("r", Number(marker.Radius));
            xml.WriteAttributeString("fill", fill);
            xml.WriteEndElement();
            return;
        }

        var r = marker.Radius;
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "travel-marker");

        if (marker.HalfWidth <= r)
        {
            xml.WriteStartElement("circle", Namespace);
            xml.WriteAttributeString("cx", Number(marker.Center.X));
            xml.WriteAttributeString("cy", Number(marker.Center.Y));
            xml.WriteAttributeString("r", Number(r));
        }
        else
        {
            xml.WriteStartElement("rect", Namespace);
            xml.WriteAttributeString("x", Number(marker.Center.X - marker.HalfWidth));
            xml.WriteAttributeString("y", Number(marker.Center.Y - r));
            xml.WriteAttributeString("width", Number(2 * marker.HalfWidth));
            xml.WriteAttributeString("height", Number(2 * r));
            xml.WriteAttributeString("rx", Number(r));
        }

        xml.WriteAttributeString("fill", fill);
        xml.WriteEndElement();

        // Centred on the marker: 0.35 em below the centre puts the digits' middle on it.
        var size = MarkerTextSize * unit;
        xml.WriteStartElement("text", Namespace);
        xml.WriteAttributeString("x", Number(marker.Center.X));
        xml.WriteAttributeString("y", Number(marker.Center.Y + (0.35 * size)));
        xml.WriteAttributeString("font-size", Number(size));
        xml.WriteAttributeString("fill", textColor);
        xml.WriteAttributeString("text-anchor", "middle");
        xml.WriteString(marker.Label);
        xml.WriteEndElement();

        xml.WriteEndElement();
    }

    // One <text> per block, a <tspan> per line; in its own colour if given, else the group's.
    internal static void WriteBlock(XmlWriter xml, Block block, RectD bounds, TextAnchor anchor, string? fill = null)
    {
        var x = Number(anchor switch
        {
            TextAnchor.Start => bounds.X,
            TextAnchor.End => bounds.Right,
            _ => bounds.X + (bounds.Width / 2),
        });
        var top = bounds.Y;
        xml.WriteStartElement("text", Namespace);
        xml.WriteAttributeString("class", "travel-label");
        if (fill is not null)
        {
            xml.WriteAttributeString("fill", fill);
        }

        xml.WriteAttributeString("text-anchor", anchor switch
        {
            TextAnchor.Start => "start",
            TextAnchor.End => "end",
            _ => "middle",
        });
        foreach (var line in block.Lines)
        {
            top += line.SpaceBefore;
            xml.WriteStartElement("tspan", Namespace);
            xml.WriteAttributeString("class", line.ClassName);
            xml.WriteAttributeString("x", x);
            xml.WriteAttributeString("y", Number(top + (line.Size * Ascent)));
            xml.WriteAttributeString("font-size", Number(line.Size));
            if (line.Bold)
            {
                xml.WriteAttributeString("font-weight", "bold");
            }

            xml.WriteString(line.Text);
            xml.WriteEndElement();
            top += line.Size * LineHeight;
        }

        xml.WriteEndElement();
    }
}
