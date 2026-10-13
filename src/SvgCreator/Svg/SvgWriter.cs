using System.Text;
using System.Xml;
using SvgCreator.Fitting;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Svg;

/// <summary>
/// Writes contours as filled SVG paths: one <c>&lt;path&gt;</c> per ink component (outer border
/// plus its holes) under an even-odd fill so holes stay empty. Components recognized as ellipses use
/// native shapes: a solid ellipse becomes <c>&lt;circle&gt;</c>/<c>&lt;ellipse&gt;</c>, a band between
/// two concentric ellipses becomes one stroked <c>&lt;circle&gt;</c>/<c>&lt;ellipse&gt;</c>, and any
/// other elliptical contour is drawn with native arc (<c>a</c>) commands inside its path.
/// </summary>
internal static class SvgWriter
{
    // Ring widths may differ this much (px) between the two axes and still count as uniform.
    internal const double RingTolerance = 1.0;

    // Radii closer than this (px) are written as a circle.
    private const double CircleTolerance = 0.5;

    public static string Write(TracedDrawing drawing, string fillColor, SvgStyle? style = null) =>
        WriteDocument(drawing.Width, drawing.Height, fillColor, style ?? SvgStyle.Clean, (xml, filterId) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("fill", fillColor);
            xml.WriteAttributeString("fill-rule", "evenodd");
            xml.WriteAttributeString("stroke", "none");
            if (filterId is not null)
            {
                xml.WriteAttributeString("filter", $"url(#{filterId})");
            }

            foreach (var group in GroupByComponent(drawing.Contours))
            {
                if (TryWriteShape(xml, group, fillColor))
                {
                    continue;
                }

                xml.WriteStartElement("path", Namespace);
                xml.WriteAttributeString("d", PathData(group));
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        });

    /// <summary>
    /// Writes an SVG document of the given size: the style's filter definition, then the content,
    /// then the style's decorations. The content receives the id of the filter to apply to its ink.
    /// </summary>
    public static string WriteDocument(int width, int height, string inkColor, SvgStyle style, Action<XmlWriter, string?> writeContent)
    {
        var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };
        var output = new StringBuilder();
        using (var xml = XmlWriter.Create(output, settings))
        {
            // Decorations may run past the content (a postmark's waves do), so the canvas may widen.
            var canvasWidth = style.Decorations.Aggregate(width, (w, d) => Math.Max(w, d.RequiredWidth));
            var w = Integer(canvasWidth);
            var h = Integer(height);

            xml.WriteStartElement("svg", Namespace);
            xml.WriteAttributeString("width", w);
            xml.WriteAttributeString("height", h);
            xml.WriteAttributeString("viewBox", $"0 0 {w} {h}");

            style.Filter?.WriteDefinition(xml, canvasWidth, height);
            var filterId = style.Filter?.Id(canvasWidth, height);
            writeContent(xml, filterId);

            foreach (var decoration in style.Decorations)
            {
                decoration.Write(xml, inkColor, filterId);
            }

            xml.WriteEndElement();
        }

        return output.ToString();
    }

    // A component that is exactly one solid ellipse, or one elliptical band, becomes a native shape.
    private static bool TryWriteShape(XmlWriter xml, List<CurveContour> group, string fillColor)
    {
        if (group is [{ IsHole: false, Ellipse: { } solid }])
        {
            WriteEllipse(xml, solid);
            xml.WriteEndElement();
            return true;
        }

        if (group.Count == 2
            && group.Find(c => !c.IsHole)?.Ellipse is { } outer
            && group.Find(c => c.IsHole)?.Ellipse is { } inner
            && Ellipse.TryMakeRing(outer, inner, RingTolerance, out var middle, out var width))
        {
            WriteEllipse(xml, middle);
            xml.WriteAttributeString("fill", "none");
            xml.WriteAttributeString("stroke", fillColor);
            xml.WriteAttributeString("stroke-width", Format(width));
            xml.WriteEndElement();
            return true;
        }

        return false;
    }

    // Leaves the element open so callers can add attributes.
    private static void WriteEllipse(XmlWriter xml, Ellipse e)
    {
        var circle = e.IsCircle(CircleTolerance);
        xml.WriteStartElement(circle ? "circle" : "ellipse", Namespace);
        xml.WriteAttributeString("cx", Format(e.Center.X));
        xml.WriteAttributeString("cy", Format(e.Center.Y));
        if (circle)
        {
            xml.WriteAttributeString("r", Format((e.RadiusX + e.RadiusY) / 2));
            return;
        }

        xml.WriteAttributeString("rx", Format(e.RadiusX));
        xml.WriteAttributeString("ry", Format(e.RadiusY));
        if (Math.Round(e.AngleDegrees, 1) is not (0 or 180))
        {
            xml.WriteAttributeString("transform", $"rotate({Format(e.AngleDegrees)} {Format(e.Center.X)} {Format(e.Center.Y)})");
        }
    }

    private static List<List<CurveContour>> GroupByComponent(IReadOnlyList<CurveContour> contours)
    {
        var groups = new List<List<CurveContour>>();
        var index = new Dictionary<int, int>();
        foreach (var contour in contours)
        {
            if (!index.TryGetValue(contour.Label, out var g))
            {
                g = groups.Count;
                index[contour.Label] = g;
                groups.Add([]);
            }

            groups[g].Add(contour);
        }

        return groups;
    }

    // Control points this close to the chord are written as a straight line.
    private const double LineTolerance = 0.1;

    /// <summary>
    /// Builds compact path data: absolute <c>M</c> per subpath, then relative <c>l</c>/<c>c</c> commands
    /// with 0.1 px precision. Coordinates are rounded to tenths first and deltas taken between rounded
    /// points, so rounding never accumulates along a path.
    /// </summary>
    internal static string PathData(IEnumerable<CurveContour> contours)
    {
        var sb = new StringBuilder();
        foreach (var contour in contours)
        {
            if (contour.Ellipse is { } ellipse)
            {
                AppendEllipse(sb, ellipse);
                continue;
            }

            var segments = contour.Segments;
            if (segments.Count == 0)
            {
                continue;
            }

            var current = Tenths(segments[0].P0);
            sb.Append('M');
            AppendNumber(sb, current.X);
            AppendNumber(sb, current.Y);

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var end = Tenths(segment.P3);
                if (segment.IsNearlyLine(LineTolerance))
                {
                    // 'Z' already draws a closing straight line.
                    if (i < segments.Count - 1)
                    {
                        sb.Append('l');
                        AppendDelta(sb, end, current);
                    }
                }
                else
                {
                    sb.Append('c');
                    AppendDelta(sb, Tenths(segment.P1), current);
                    AppendDelta(sb, Tenths(segment.P2), current);
                    AppendDelta(sb, end, current);
                }

                current = end;
            }

            sb.Append('Z');
        }

        return sb.ToString();
    }

    // Two native half-ellipse arcs between opposite ends of the X axis.
    private static void AppendEllipse(StringBuilder sb, Ellipse e)
    {
        var start = Tenths(e.PointAt(0));
        var opposite = Tenths(e.PointAt(Math.PI));
        sb.Append('M');
        AppendNumber(sb, start.X);
        AppendNumber(sb, start.Y);
        AppendArc(sb, e, opposite, start);
        AppendArc(sb, e, start, opposite);
        sb.Append('Z');
    }

    private static void AppendArc(StringBuilder sb, Ellipse e, (long X, long Y) end, (long X, long Y) from)
    {
        sb.Append('a');
        AppendNumber(sb, ToTenths(e.RadiusX));
        AppendNumber(sb, ToTenths(e.RadiusY));
        AppendNumber(sb, ToTenths(e.AngleDegrees));
        sb.Append(" 0 1"); // large-arc 0, sweep 1: each half goes the positive-angle way round
        AppendDelta(sb, end, from);
    }
}
