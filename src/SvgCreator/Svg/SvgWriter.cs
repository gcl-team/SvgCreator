using System.Globalization;
using System.Text;
using System.Xml;
using SvgCreator.Fitting;

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
    private const string SvgNamespace = "http://www.w3.org/2000/svg";

    // Ring widths may differ this much (px) between the two axes and still count as uniform.
    private const double RingTolerance = 1.0;

    // Radii closer than this (px) are written as a circle.
    private const double CircleTolerance = 0.5;

    // Stamp filter, tuned for LineSigma = 1 and scaled with the line thickness (not the canvas size:
    // Lines-mode strokes are a few pixels wide at any resolution, so a canvas-scaled texture frays
    // them on large images). Frequencies divide by the scale, distances multiply by it.
    private const double EdgeNoiseFrequency = 0.8;  // fine noise that wobbles the ink edges
    private const double EdgeDisplacement = 2.5;    // how far (px) edges are pushed by that noise
    private const double WearNoiseFrequency = 0.06; // coarse blotches where ink didn't transfer
    private const double SpeckNoiseFrequency = 0.9; // small pinholes in the ink

    // Noise → alpha as alpha = Bias − Gain·R. Fractal noise centres on R ≈ 0.5, so ink stays fully
    // opaque below R = (Bias − 1)/Gain and vanishes above R = Bias/Gain: blotches where R > 0.63–0.70,
    // pinholes where R > 0.73–0.78. Tuned so the print looks worn but stays readable.
    private const double WearGain = 14;
    private const double WearBias = 9.8;
    private const double SpeckGain = 20;
    private const double SpeckBias = 15.6;

    public static string Write(
        int width, int height, IReadOnlyList<CurveContour> contours, string fillColor,
        OutputStyle style = OutputStyle.Clean, int stampSeed = 1, double stampScale = 1)
    {
        var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };
        var output = new StringBuilder();
        using (var xml = XmlWriter.Create(output, settings))
        {
            var w = width.ToString(CultureInfo.InvariantCulture);
            var h = height.ToString(CultureInfo.InvariantCulture);

            xml.WriteStartElement("svg", SvgNamespace);
            xml.WriteAttributeString("width", w);
            xml.WriteAttributeString("height", h);
            xml.WriteAttributeString("viewBox", $"0 0 {w} {h}");

            string? filterId = null;
            if (style == OutputStyle.Stamp)
            {
                filterId = $"svgcreator-stamp-{stampSeed.ToString(CultureInfo.InvariantCulture)}";
                WriteStampFilter(xml, filterId, width, height, stampSeed, stampScale);
            }

            xml.WriteStartElement("g", SvgNamespace);
            xml.WriteAttributeString("fill", fillColor);
            xml.WriteAttributeString("fill-rule", "evenodd");
            xml.WriteAttributeString("stroke", "none");
            if (filterId is not null)
            {
                xml.WriteAttributeString("filter", $"url(#{filterId})");
            }

            foreach (var group in GroupByComponent(contours))
            {
                if (TryWriteShape(xml, group, fillColor))
                {
                    continue;
                }

                xml.WriteStartElement("path", SvgNamespace);
                xml.WriteAttributeString("d", PathData(group));
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        return output.ToString();
    }

    /// <summary>
    /// A rubber-stamp impression made only of native SVG filter primitives, applied at render time:
    /// fractal noise displaces the ink edges (rough print), and two noise layers turned into alpha
    /// masks remove blotches and pinholes of ink (worn print). The id carries the seed because ids
    /// are global when several SVGs are inlined into one HTML page.
    /// </summary>
    private static void WriteStampFilter(XmlWriter xml, string id, int width, int height, int seed, double scale)
    {
        xml.WriteStartElement("defs", SvgNamespace);
        xml.WriteStartElement("filter", SvgNamespace);
        xml.WriteAttributeString("id", id);
        xml.WriteAttributeString("filterUnits", "userSpaceOnUse");
        xml.WriteAttributeString("x", "0");
        xml.WriteAttributeString("y", "0");
        xml.WriteAttributeString("width", width.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("height", height.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("color-interpolation-filters", "sRGB");

        // Rough edges.
        WriteTurbulence(xml, EdgeNoiseFrequency / scale, octaves: 2, seed, "edgeNoise");
        xml.WriteStartElement("feDisplacementMap", SvgNamespace);
        xml.WriteAttributeString("in", "SourceGraphic");
        xml.WriteAttributeString("in2", "edgeNoise");
        xml.WriteAttributeString("scale", Number(EdgeDisplacement * scale));
        xml.WriteAttributeString("xChannelSelector", "R");
        xml.WriteAttributeString("yChannelSelector", "G");
        xml.WriteAttributeString("result", "rough");
        xml.WriteEndElement();

        // Worn ink: coarse blotches and fine pinholes, each as an alpha mask.
        WriteTurbulence(xml, WearNoiseFrequency / scale, octaves: 3, seed + 1, "wearNoise");
        WriteNoiseToAlpha(xml, "wearNoise", WearGain, WearBias, "wear");
        WriteTurbulence(xml, SpeckNoiseFrequency / scale, octaves: 1, seed + 2, "speckNoise");
        WriteNoiseToAlpha(xml, "speckNoise", SpeckGain, SpeckBias, "speck");

        WriteComposite(xml, "wear", "speck", "mask");
        WriteComposite(xml, "rough", "mask", null);

        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void WriteTurbulence(XmlWriter xml, double frequency, int octaves, int seed, string result)
    {
        xml.WriteStartElement("feTurbulence", SvgNamespace);
        xml.WriteAttributeString("type", "fractalNoise");
        xml.WriteAttributeString("baseFrequency", Number(frequency));
        xml.WriteAttributeString("numOctaves", octaves.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("seed", seed.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("result", result);
        xml.WriteEndElement();
    }

    // alpha = bias − gain·R; colour channels are irrelevant for a mask.
    private static void WriteNoiseToAlpha(XmlWriter xml, string input, double gain, double bias, string result)
    {
        xml.WriteStartElement("feColorMatrix", SvgNamespace);
        xml.WriteAttributeString("in", input);
        xml.WriteAttributeString("type", "matrix");
        xml.WriteAttributeString("values", $"0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 {Number(-gain)} 0 0 0 {Number(bias)}");
        xml.WriteAttributeString("result", result);
        xml.WriteEndElement();
    }

    // Keeps `input` only where `mask` is opaque (alphas multiply).
    private static void WriteComposite(XmlWriter xml, string input, string mask, string? result)
    {
        xml.WriteStartElement("feComposite", SvgNamespace);
        xml.WriteAttributeString("in", input);
        xml.WriteAttributeString("in2", mask);
        xml.WriteAttributeString("operator", "in");
        if (result is not null)
        {
            xml.WriteAttributeString("result", result);
        }

        xml.WriteEndElement();
    }

    private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

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
        xml.WriteStartElement(circle ? "circle" : "ellipse", SvgNamespace);
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

    private static string Format(double value)
    {
        var sb = new StringBuilder();
        AppendNumber(sb, (long)Math.Round(value * 10, MidpointRounding.AwayFromZero));
        return sb.ToString();
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
        AppendNumber(sb, (long)Math.Round(e.RadiusX * 10, MidpointRounding.AwayFromZero));
        AppendNumber(sb, (long)Math.Round(e.RadiusY * 10, MidpointRounding.AwayFromZero));
        AppendNumber(sb, (long)Math.Round(e.AngleDegrees * 10, MidpointRounding.AwayFromZero));
        sb.Append(" 0 1"); // large-arc 0, sweep 1: each half goes the positive-angle way round
        AppendDelta(sb, end, from);
    }

    private static (long X, long Y) Tenths(PointD p) =>
        ((long)Math.Round(p.X * 10, MidpointRounding.AwayFromZero), (long)Math.Round(p.Y * 10, MidpointRounding.AwayFromZero));

    private static void AppendDelta(StringBuilder sb, (long X, long Y) point, (long X, long Y) origin)
    {
        AppendNumber(sb, point.X - origin.X);
        AppendNumber(sb, point.Y - origin.Y);
    }

    // Writes a value given in tenths in its shortest SVG form ("12", "1.5", ".5", "-.5"),
    // adding a space only where the previous number would otherwise run into this one.
    private static void AppendNumber(StringBuilder sb, long tenths)
    {
        var negative = tenths < 0;
        var magnitude = Math.Abs(tenths);
        long whole = magnitude / 10, fraction = magnitude % 10;

        if (negative)
        {
            sb.Append('-');
        }
        else if (sb.Length > 0 && char.IsAsciiDigit(sb[^1]))
        {
            sb.Append(' ');
        }

        if (whole != 0 || fraction == 0)
        {
            sb.Append(whole.ToString(CultureInfo.InvariantCulture));
        }

        if (fraction != 0)
        {
            sb.Append('.').Append((char)('0' + fraction));
        }
    }
}
