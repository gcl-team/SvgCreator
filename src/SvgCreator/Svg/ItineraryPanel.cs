using System.Xml;
using SvgCreator.Fitting;
using SvgCreator.Mapping;
using static SvgCreator.Svg.SvgFormat;
using static SvgCreator.Svg.TextWrap;
using static SvgCreator.Svg.TravelMapWriter;

namespace SvgCreator.Svg;

/// <summary>
/// The itinerary beside a travel map whose text doesn't fit on it: every stop in travel order,
/// numbered like its marker, with its name, time and description. The map then names its markers
/// and nothing more. The panel takes the right of a landscape canvas or the bottom of a portrait
/// one, behind a thin divider. Entries flow down in columns, as many as give the largest text, and
/// all the panel's text shrinks evenly when even that doesn't fit. A waypoint is a step under the
/// place before it: indented, with a dot for a badge, set closer to its neighbours.
/// </summary>
internal static class ItineraryPanel
{
    private const double LandscapeShare = 0.4;    // of the width, for the panel on the right
    private const double PortraitShare = 0.45;    // of the height, for the panel at the bottom
    private const double BadgeGap = 0.015;        // between an entry's number and its text
    private const double EntryGap = 0.025;        // between entries
    private const double StepGap = 0.012;         // before a waypoint's step, under its place
    private const double ColumnGap = 0.04;
    private const int MaxColumns = 3;
    private const double BaseScale = 0.8;         // the panel's text at full size, against the map's: it's a reference list
    private const double MinScale = 0.3;          // the smallest the panel's text may shrink to
    private const double ScaleTolerance = 0.9;    // column counts this close to the largest text compete on wrapping
    private const double DividerOpacity = 0.35;
    private const double DividerWidth = 0.003;

    /// <summary>One stop's entry: its number badge (a plain dot for a waypoint) and its text.</summary>
    internal sealed record Entry(string? Number, PointD BadgeCenter, double BadgeRadius, Block Block, RectD Bounds);

    /// <summary>The panel's entries, the divider between it and the map, and the text scale used.</summary>
    internal sealed record PanelLayout(IReadOnlyList<Entry> Entries, PointD DividerFrom, PointD DividerTo, double Scale, double BadgeTextSize);

    /// <summary>The canvas split into the map's frame and the panel's.</summary>
    public static (RectD Map, RectD Panel) Split(int width, int height)
    {
        if (width >= height)
        {
            var panelWidth = width * LandscapeShare;
            return (new RectD(0, 0, width - panelWidth, height), new RectD(width - panelWidth, 0, panelWidth, height));
        }

        var panelHeight = height * PortraitShare;
        return (new RectD(0, 0, width, height - panelHeight), new RectD(0, height - panelHeight, width, panelHeight));
    }

    public static PanelLayout Layout(IReadOnlyList<TravelStop> ordered, RectD frame, double unit)
    {
        var margin = Margin * unit;
        var inner = frame.Inset(margin);
        var landscape = frame.X > 0;   // Split puts a landscape panel on the right, a portrait one at the bottom
        var (dividerFrom, dividerTo) = landscape
            ? (new PointD(frame.X, margin), new PointD(frame.X, frame.Bottom - margin))
            : (new PointD(margin, frame.Y), new PointD(frame.Right - margin, frame.Y));

        // For each column count, the largest text that fits, found by bisection: smaller text
        // always fits at least as well, as its lines wrap no later. Then among the counts whose
        // text is nearly the largest, the one that wraps the fewest lines: more, narrower columns
        // can fit larger text yet break every time in two. Fewer columns win a tie.
        var options = new List<(double Scale, int Columns, int Lines)>();
        for (var columns = 1; columns <= MaxColumns; columns++)
        {
            double scale;
            if (Flow(ordered, inner, unit, BaseScale, columns) is not null)
            {
                scale = BaseScale;
            }
            else
            {
                var (low, high) = (MinScale, BaseScale);
                for (var i = 0; i < 12; i++)
                {
                    var mid = (low + high) / 2;
                    (low, high) = Flow(ordered, inner, unit, mid, columns) is not null ? (mid, high) : (low, mid);
                }

                scale = low;
            }

            var lines = (Flow(ordered, inner, unit, scale, columns) ?? Flow(ordered, inner, unit, scale, columns, force: true)!).Sum(e => e.Block.Lines.Count);
            options.Add((scale, columns, lines));
        }

        var largest = options.Max(o => o.Scale);
        var (bestScale, bestColumns, _) = options
            .Where(o => o.Scale >= largest * ScaleTolerance)
            .OrderBy(o => o.Lines).ThenBy(o => o.Columns)
            .First();

        // Even at the smallest scale an itinerary this long can overrun; it is then laid out anyway.
        var entries = Flow(ordered, inner, unit, bestScale, bestColumns) ?? Flow(ordered, inner, unit, bestScale, bestColumns, force: true)!;
        return new PanelLayout(entries, dividerFrom, dividerTo, bestScale, MarkerTextSize * unit * bestScale);
    }

    // Lays the entries out down `columns` columns of `inner` at text scale `scale`; null if they
    // don't fit (unless forced, when the last column runs on past the bottom).
    private static List<Entry>? Flow(IReadOnlyList<TravelStop> ordered, RectD inner, double unit, double scale, int columns, bool force = false)
    {
        var text = unit * scale;
        var columnGap = ColumnGap * unit;
        var columnWidth = (inner.Width - (columnGap * (columns - 1))) / columns;
        var radius = MarkerRadius * text;
        var indent = (2 * radius) + (BadgeGap * text);
        var textWidth = columnWidth - indent;
        if (textWidth <= 0)
        {
            return null;
        }

        var numbers = TravelMapWriter.Numbers(ordered);
        var dot = WaypointRadius * text;
        var stepIndent = indent + (2 * dot) + (BadgeGap * text);
        var entries = new List<Entry>();
        var (column, top) = (0, inner.Y);
        for (var i = 0; i < ordered.Count; i++)
        {
            var stop = ordered[i];
            var entryIndent = stop.Waypoint ? stepIndent : indent;
            if (columnWidth - entryIndent <= 0)
            {
                return null;
            }

            var block = EntryBlock(stop, text, columnWidth - entryIndent);
            var height = stop.Waypoint ? block.Height : Math.Max(block.Height, 2 * radius);
            // On to the next column when this one is full; with no column left (or an entry too
            // tall for any column) it doesn't fit.
            if (top + height > inner.Bottom)
            {
                if (top > inner.Y && column + 1 < columns)
                {
                    (column, top) = (column + 1, inner.Y);
                }

                if (top + height > inner.Bottom && !force)
                {
                    return null;
                }
            }

            // A place's badge sits left of its name; a waypoint's dot, under that name's first letter.
            var x = inner.X + (column * (columnWidth + columnGap));
            var middle = top + (block.Lines[0].Height / 2);
            entries.Add(stop.Waypoint
                ? new Entry(null, new PointD(x + indent + dot, middle), dot, block, new RectD(x + stepIndent, top, block.Width, block.Height))
                : new Entry(Integer(numbers[i]!.Value), new PointD(x + radius, middle), radius, block, new RectD(x + indent, top, block.Width, block.Height)));
            var next = i + 1 < ordered.Count && ordered[i + 1].Waypoint;
            top += height + ((next ? StepGap : EntryGap) * text);
        }

        return entries;
    }

    private static Block EntryBlock(TravelStop stop, double text, double wrap)
    {
        var lines = new List<Line>();
        var (nameClass, nameSize, timeSize) = stop.Waypoint
            ? ("travel-waypoint-name", WaypointNameSize * text, DescriptionSize * text)
            : ("travel-name", NameSize * text, TimeSize * text);
        lines.AddRange(Wrap(stop.Name, nameSize, wrap).Select(t => new Line(nameClass, t, nameSize, Bold: true)));
        if (!string.IsNullOrWhiteSpace(stop.Time))
        {
            lines.AddRange(Wrap(stop.Time, timeSize, wrap).Select(t => new Line("travel-time", t, timeSize, Bold: false)));
        }

        foreach (var description in stop.Description)
        {
            lines.AddRange(Wrap(description, DescriptionSize * text, wrap).Select(t => new Line("travel-description", t, DescriptionSize * text, Bold: false)));
        }

        return Measure(lines);
    }

    public static void Write(XmlWriter xml, PanelLayout panel, double unit, string foreground, string background, string waypointColor)
    {
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "travel-panel");
        xml.WriteAttributeString("fill", foreground);
        xml.WriteAttributeString("font-family", FontMetrics.Sans.Family);

        xml.WriteStartElement("line", Namespace);
        xml.WriteAttributeString("class", "travel-panel-divider");
        xml.WriteAttributeString("x1", Number(panel.DividerFrom.X));
        xml.WriteAttributeString("y1", Number(panel.DividerFrom.Y));
        xml.WriteAttributeString("x2", Number(panel.DividerTo.X));
        xml.WriteAttributeString("y2", Number(panel.DividerTo.Y));
        xml.WriteAttributeString("stroke", foreground);
        xml.WriteAttributeString("stroke-opacity", Number(DividerOpacity));
        xml.WriteAttributeString("stroke-width", Number(DividerWidth * unit));
        xml.WriteEndElement();

        foreach (var entry in panel.Entries)
        {
            // A waypoint's step, dot and text, is in the waypoint colour, as on the map.
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", entry.Number is null ? "travel-step" : "travel-entry");
            if (entry.Number is null)
            {
                xml.WriteAttributeString("fill", waypointColor);
            }

            xml.WriteStartElement("circle", Namespace);
            xml.WriteAttributeString("cx", Number(entry.BadgeCenter.X));
            xml.WriteAttributeString("cy", Number(entry.BadgeCenter.Y));
            xml.WriteAttributeString("r", Number(entry.BadgeRadius));
            xml.WriteEndElement();

            if (entry.Number is null)
            {
                WriteBlock(xml, entry.Block, entry.Bounds, TextAnchor.Start);
                xml.WriteEndElement();
                continue;
            }

            // Like the markers: 0.35 em below the centre puts the digits' middle on it.
            xml.WriteStartElement("text", Namespace);
            xml.WriteAttributeString("x", Number(entry.BadgeCenter.X));
            xml.WriteAttributeString("y", Number(entry.BadgeCenter.Y + (0.35 * panel.BadgeTextSize)));
            xml.WriteAttributeString("font-size", Number(panel.BadgeTextSize));
            xml.WriteAttributeString("font-weight", "bold");
            xml.WriteAttributeString("fill", background);
            xml.WriteAttributeString("text-anchor", "middle");
            xml.WriteString(entry.Number);
            xml.WriteEndElement();

            WriteBlock(xml, entry.Block, entry.Bounds, TextAnchor.Start);
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }
}
