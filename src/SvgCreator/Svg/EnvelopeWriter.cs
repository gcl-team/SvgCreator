using System.Text;
using System.Xml;
using SvgCreator.Fitting;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Svg;

/// <summary>
/// Draws an airmail envelope: paper with a border of slanted straps alternating between two
/// colours with paper-coloured gaps, and for <see cref="EnvelopeStyle.Crumpled"/> a group of
/// seeded creases over it. Like the postage stamp, every value is a fraction of the envelope's
/// shorter side and is written with <see cref="SvgFormat.Number"/>.
/// </summary>
internal static class EnvelopeWriter
{
    // Sizes are fractions of the envelope's shorter side, so it looks the same at any size.
    private const double StrapDepth = 0.07;        // how far the border reaches in from the edge
    private const double StrapWidth = 0.07;        // each strap and each gap, along the edge
    private const double PaperStrokeWidth = 0.003;

    // A strap repeat is strap, gap, alternate strap, gap. Like the stamp's perforations, a very
    // elongated envelope would otherwise get an unbounded number along its long side.
    private const int MaxRepeatsPerSide = 250;

    // Crumpling: straight creases through random points at random angles. Each shades one side of
    // the fold, fading out over its reach, and lights the other side with a thin highlight, so
    // overlapping creases form the facets of crumpled paper.
    private const int CreaseCount = 12;
    private const double CreaseReach = 0.13;
    private const double CreaseShade = 0.12;       // shadow opacity at the fold
    private const double HighlightOffset = 0.002;
    private const double HighlightWidth = 0.003;
    private const double HighlightOpacity = 0.55;

    // Slightly irregular edges: low-frequency noise displaces the whole envelope a little. The
    // paper is inset by half the displacement so its edges stay on the canvas.
    private const double EdgeWaves = 2.2;           // noise cycles across the shorter side
    private const double EdgeDisplacement = 0.013;
    private const double GrainFrequency = 0.9;      // per pixel: grain is a property of the paper, not the size
    private const double GrainOpacity = 0.06;

    private const string PaperEdgeColor = "#bbb";

    public static string Write(EnvelopeOptions options)
    {
        var (width, height) = (options.Width, options.Height);
        double unit = Math.Min(width, height);
        var crumpled = options.Style == EnvelopeStyle.Crumpled;

        // The paper is inset by its edge stroke (and, crumpled, by how far its edges can move), so
        // nothing is clipped by the canvas.
        var stroke = PaperStrokeWidth * unit;
        var displacement = crumpled ? EdgeDisplacement * unit : 0;
        var inset = stroke + (displacement / 2);
        var paper = (X: inset, Y: inset, Width: width - (2 * inset), Height: height - (2 * inset));

        var ids = new Ids(width, height, options.Seed);

        return SvgWriter.WriteDocument(width, height, options.PaperColor, SvgStyle.Clean, (xml, _) =>
        {
            if (crumpled)
            {
                WriteCrumpleDefinitions(xml, ids, width, height, unit, options.Seed, paper);
            }

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "envelope");
            if (crumpled)
            {
                xml.WriteAttributeString("filter", $"url(#{ids.Edge})");
            }

            xml.WriteStartElement("rect", Namespace);
            xml.WriteAttributeString("class", "envelope-paper");
            xml.WriteAttributeString("x", Number(paper.X));
            xml.WriteAttributeString("y", Number(paper.Y));
            xml.WriteAttributeString("width", Number(paper.Width));
            xml.WriteAttributeString("height", Number(paper.Height));
            xml.WriteAttributeString("fill", options.PaperColor);
            xml.WriteAttributeString("stroke", PaperEdgeColor);
            xml.WriteAttributeString("stroke-width", Number(stroke));
            xml.WriteEndElement();

            var (straps, alternates) = Straps(paper.X, paper.Y, paper.Width, paper.Height, unit);
            WritePath(xml, "envelope-straps", options.StrapColor, straps);
            WritePath(xml, "envelope-straps-alternate", options.AlternateStrapColor, alternates);

            if (crumpled)
            {
                WriteCreases(xml, ids, unit, options.Seed, paper);
            }

            xml.WriteEndElement();
        });
    }

    // Ids are global once several SVGs share a page or a canvas. Each id names everything its
    // definition depends on, so two envelopes only share an id when the definitions are identical.
    private sealed record Ids(int Width, int Height, int Seed)
    {
        public string Paper => $"svgcreator-envelope-{Integer(Width)}x{Integer(Height)}-paper";

        public string Edge => $"svgcreator-envelope-{Integer(Width)}x{Integer(Height)}-{Integer(Seed)}-edge";

        public static string Grain => "svgcreator-envelope-grain";

        public static string Crease => "svgcreator-envelope-crease";
    }

    private static void WritePath(XmlWriter xml, string className, string color, string data)
    {
        xml.WriteStartElement("path", Namespace);
        xml.WriteAttributeString("class", className);
        xml.WriteAttributeString("d", data);
        xml.WriteAttributeString("fill", color);
        xml.WriteEndElement();
    }

    /// <summary>
    /// The strap border as two path data strings, one per colour. Each side is a trapezoid mitred
    /// at the corners; along it, straps slanted at 45° repeat strap, gap, alternate strap, gap, a
    /// whole number of times so every side starts and ends the same way. The sides run clockwise,
    /// so the slant turns consistently around the envelope.
    /// </summary>
    internal static (string Straps, string Alternates) Straps(double x, double y, double width, double height, double unit)
    {
        var depth = StrapDepth * unit;
        var (straps, alternates) = (new StringBuilder(), new StringBuilder());

        // Local coordinates per side: t along the outer edge (clockwise), d inward from it.
        (double Length, Func<double, double, PointD> ToCanvas)[] sides =
        [
            (width, (t, d) => new PointD(x + t, y + d)),                     // top, left to right
            (height, (t, d) => new PointD(x + width - d, y + t)),            // right, top to bottom
            (width, (t, d) => new PointD(x + width - t, y + height - d)),    // bottom, right to left
            (height, (t, d) => new PointD(x + d, y + height - t)),           // left, bottom to top
        ];

        foreach (var (length, toCanvas) in sides)
        {
            PointD[] side = [new(0, 0), new(length, 0), new(length - depth, depth), new(depth, depth)];
            var repeats = (int)Math.Clamp(Math.Round(length / (4 * StrapWidth * unit)), 1, MaxRepeatsPerSide);
            var strap = length / repeats / 4;

            // Start a repeat early, so the straps cover the mitred start of the side.
            for (var k = -1; k <= repeats; k++)
            {
                foreach (var (offset, target) in new[] { (0, straps), (2, alternates) })
                {
                    var t = ((k * 4) + offset) * strap;
                    PointD[] band = [new(t, 0), new(t + strap, 0), new(t + strap + depth, depth), new(t + depth, depth)];
                    AppendPolygon(target, ClipConvex(band, side), toCanvas);
                }
            }
        }

        return (straps.ToString(), alternates.ToString());
    }

    private static void AppendPolygon(StringBuilder sb, List<PointD> polygon, Func<double, double, PointD> toCanvas)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        for (var i = 0; i < polygon.Count; i++)
        {
            var p = toCanvas(polygon[i].X, polygon[i].Y);
            sb.Append(i == 0 ? 'M' : 'L').Append(Number(p.X)).Append(' ').Append(Number(p.Y));
        }

        sb.Append('Z');
    }

    /// <summary>
    /// Sutherland–Hodgman: <paramref name="subject"/> cut down to the convex <paramref name="clip"/>,
    /// whose vertices run clockwise on screen (y down).
    /// </summary>
    internal static List<PointD> ClipConvex(IReadOnlyList<PointD> subject, IReadOnlyList<PointD> clip)
    {
        var output = subject.ToList();
        for (var e = 0; e < clip.Count && output.Count > 0; e++)
        {
            var (a, b) = (clip[e], clip[(e + 1) % clip.Count]);
            double Side(PointD p) => ((b.X - a.X) * (p.Y - a.Y)) - ((b.Y - a.Y) * (p.X - a.X));

            var input = output;
            output = [];
            for (var i = 0; i < input.Count; i++)
            {
                var (current, next) = (input[i], input[(i + 1) % input.Count]);
                var (sc, sn) = (Side(current), Side(next));
                if (sc >= 0)
                {
                    output.Add(current);
                }

                if ((sc >= 0) != (sn >= 0))
                {
                    var f = sc / (sc - sn);
                    output.Add(new PointD(current.X + ((next.X - current.X) * f), current.Y + ((next.Y - current.Y) * f)));
                }
            }
        }

        return output;
    }

    private static void WriteCrumpleDefinitions(XmlWriter xml, Ids ids, int width, int height, double unit, int seed, (double X, double Y, double Width, double Height) paper)
    {
        xml.WriteStartElement("defs", Namespace);

        // Irregular edges: the whole envelope, displaced a little by coarse noise.
        xml.WriteStartElement("filter", Namespace);
        xml.WriteAttributeString("id", ids.Edge);
        xml.WriteAttributeString("filterUnits", "userSpaceOnUse");
        xml.WriteAttributeString("x", "0");
        xml.WriteAttributeString("y", "0");
        xml.WriteAttributeString("width", Integer(width));
        xml.WriteAttributeString("height", Integer(height));
        xml.WriteStartElement("feTurbulence", Namespace);
        xml.WriteAttributeString("type", "fractalNoise");
        xml.WriteAttributeString("baseFrequency", Number(EdgeFrequency(unit)));
        xml.WriteAttributeString("numOctaves", "2");
        xml.WriteAttributeString("seed", Integer(seed));
        xml.WriteAttributeString("result", "noise");
        xml.WriteEndElement();
        xml.WriteStartElement("feDisplacementMap", Namespace);
        xml.WriteAttributeString("in", "SourceGraphic");
        xml.WriteAttributeString("in2", "noise");
        xml.WriteAttributeString("scale", Number(EdgeDisplacement * unit));
        xml.WriteAttributeString("xChannelSelector", "R");
        xml.WriteAttributeString("yChannelSelector", "G");
        xml.WriteEndElement();
        xml.WriteEndElement();

        // Paper grain: fine noise as a faint dark alpha.
        xml.WriteStartElement("filter", Namespace);
        xml.WriteAttributeString("id", Ids.Grain);
        xml.WriteStartElement("feTurbulence", Namespace);
        xml.WriteAttributeString("type", "fractalNoise");
        xml.WriteAttributeString("baseFrequency", Number(GrainFrequency));
        xml.WriteAttributeString("numOctaves", "1");
        xml.WriteAttributeString("seed", "1");
        xml.WriteEndElement();
        xml.WriteStartElement("feColorMatrix", Namespace);
        xml.WriteAttributeString("values", $"0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 {Number(GrainOpacity)} 0");
        xml.WriteEndElement();
        xml.WriteEndElement();

        // A crease's shadow: dark at the fold (the top of its rectangle), fading to nothing.
        xml.WriteStartElement("linearGradient", Namespace);
        xml.WriteAttributeString("id", Ids.Crease);
        xml.WriteAttributeString("x1", "0");
        xml.WriteAttributeString("y1", "0");
        xml.WriteAttributeString("x2", "0");
        xml.WriteAttributeString("y2", "1");
        WriteStop(xml, "0", "1");
        WriteStop(xml, "1", "0");
        xml.WriteEndElement();

        xml.WriteStartElement("clipPath", Namespace);
        xml.WriteAttributeString("id", ids.Paper);
        xml.WriteStartElement("rect", Namespace);
        xml.WriteAttributeString("x", Number(paper.X));
        xml.WriteAttributeString("y", Number(paper.Y));
        xml.WriteAttributeString("width", Number(paper.Width));
        xml.WriteAttributeString("height", Number(paper.Height));
        xml.WriteEndElement();
        xml.WriteEndElement();

        xml.WriteEndElement();
    }

    private static void WriteStop(XmlWriter xml, string offset, string opacity)
    {
        xml.WriteStartElement("stop", Namespace);
        xml.WriteAttributeString("offset", offset);
        xml.WriteAttributeString("stop-color", "#000");
        xml.WriteAttributeString("stop-opacity", opacity);
        xml.WriteEndElement();
    }

    private static double EdgeFrequency(double unit) => EdgeWaves / unit;

    /// <summary>
    /// The creases: each a rectangle in a rotated frame, its top edge on the fold line and filled
    /// with the shared fading gradient, plus a highlight just across the fold. A rotated rectangle
    /// lets every crease reuse one gradient, whose definition never changes.
    /// </summary>
    private static void WriteCreases(XmlWriter xml, Ids ids, double unit, int seed, (double X, double Y, double Width, double Height) paper)
    {
        var random = new SplitMix64(seed);
        var span = paper.Width + paper.Height; // longer than any line across the paper

        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "envelope-creases");
        xml.WriteAttributeString("clip-path", $"url(#{ids.Paper})");

        for (var i = 0; i < CreaseCount; i++)
        {
            var cx = paper.X + (paper.Width * random.Between(0.1, 0.9));
            var cy = paper.Y + (paper.Height * random.Between(0.1, 0.9));
            var angle = random.Between(0, 360); // which way the fold runs, and which side it shades
            var reach = CreaseReach * unit * random.Between(0.6, 1.4);
            var shade = CreaseShade * random.Between(0.6, 1.2);

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("transform", $"rotate({Number(angle)} {Number(cx)} {Number(cy)})");

            xml.WriteStartElement("rect", Namespace);
            xml.WriteAttributeString("x", Number(cx - span));
            xml.WriteAttributeString("y", Number(cy));
            xml.WriteAttributeString("width", Number(2 * span));
            xml.WriteAttributeString("height", Number(reach));
            xml.WriteAttributeString("fill", $"url(#{Ids.Crease})");
            xml.WriteAttributeString("fill-opacity", Number(shade));
            xml.WriteEndElement();

            var highlightY = cy - (HighlightOffset * unit);
            xml.WriteStartElement("line", Namespace);
            xml.WriteAttributeString("x1", Number(cx - span));
            xml.WriteAttributeString("y1", Number(highlightY));
            xml.WriteAttributeString("x2", Number(cx + span));
            xml.WriteAttributeString("y2", Number(highlightY));
            xml.WriteAttributeString("stroke", "#fff");
            xml.WriteAttributeString("stroke-opacity", Number(HighlightOpacity));
            xml.WriteAttributeString("stroke-width", Number(HighlightWidth * unit));
            xml.WriteEndElement();

            xml.WriteEndElement();
        }

        xml.WriteStartElement("rect", Namespace);
        xml.WriteAttributeString("class", "envelope-grain");
        xml.WriteAttributeString("x", Number(paper.X));
        xml.WriteAttributeString("y", Number(paper.Y));
        xml.WriteAttributeString("width", Number(paper.Width));
        xml.WriteAttributeString("height", Number(paper.Height));
        xml.WriteAttributeString("filter", $"url(#{Ids.Grain})");
        xml.WriteEndElement();

        xml.WriteEndElement();
    }

    /// <summary>
    /// A small seeded generator for the creases. <see cref="Random"/> isn't used because .NET
    /// doesn't promise its seeded sequence stays the same across versions, and the same seed must
    /// always give the same envelope.
    /// </summary>
    private struct SplitMix64(int seed)
    {
        private ulong state = (ulong)seed;

        public double Between(double min, double max) => min + ((max - min) * NextDouble());

        private double NextDouble()
        {
            var z = state += 0x9E3779B97F4A7C15;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / (1UL << 53));
        }
    }
}
