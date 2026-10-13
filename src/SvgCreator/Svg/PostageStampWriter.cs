using System.Globalization;
using System.Text;
using System.Xml;
using SvgCreator.Imaging;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Svg;

/// <summary>
/// Lays out a <see cref="PostageStampOptions"/> stamp: white paper with a perforated edge, the picture
/// embedded unchanged in a thin ink frame, and rows of text above and below it. The picture keeps its
/// pixel size, so it is never resampled.
/// </summary>
internal static class PostageStampWriter
{
    // Sizes are fractions of the picture's shorter side, so the stamp scales with it.
    private const double Margin = 0.08;            // paper edge to the printed area
    private const double PerforationPitch = 0.05;  // distance between perforation holes
    private const double PerforationRadius = 0.3;  // hole radius, as a fraction of the pitch

    // The pitch follows the shorter side, so a very elongated picture (1 × 1,000,000 px) would
    // otherwise get millions of holes along its long side. Past this count, which a 10:1 stamp
    // reaches, the holes spread out instead, keeping the path's size bounded for any input.
    private const int MaxHolesPerSide = 200;
    private const double TextGap = 0.035;          // between a row of text and the picture
    private const double ItemGap = 0.06;           // between two items in one row
    private const double CountrySize = 0.12;
    private const double FeeSize = 0.11;
    private const double CaptionSize = 0.05;
    private const double FrameStrokeWidth = 0.006;
    private const double PaperStrokeWidth = 0.003;

    // Text is native <text>, so its width is unknown until the viewer picks a font. Each printable
    // ASCII character's advance (em) is the widest measured across the font stack, bold and
    // italic: Georgia and Times New Roman (which Chrome's generic serif resolves to), rounded up.
    // Per-character values, not averages: a row of W and M runs half as wide again as a row of
    // average capitals, and fitting has to hold for every row.
    private static readonly double[] AsciiAdvance =
    [
0.26, 0.38, 0.56, 0.71, 0.65, 1.00, 0.84, 0.28, // space ! " # $ % & '
        0.45, 0.45, 0.50, 0.71, 0.33, 0.38, 0.33, 0.48, // ( ) * + , - . /
        0.71, 0.50, 0.63, 0.63, 0.65, 0.60, 0.65, 0.56, // 0 1 2 3 4 5 6 7
        0.68, 0.65, 0.39, 0.39, 0.71, 0.71, 0.71, 0.55, // 8 9 : ; < = > ?
        0.97, 0.76, 0.76, 0.73, 0.84, 0.73, 0.68, 0.81, // @ A B C D E F G
        0.92, 0.45, 0.60, 0.82, 0.69, 1.03, 0.84, 0.82, // H I J K L M N O
        0.71, 0.82, 0.80, 0.65, 0.69, 0.84, 0.77, 1.13, // P Q R S T U V W
        0.81, 0.74, 0.69, 0.45, 0.48, 0.45, 0.71, 0.71, // X Y Z [ \\ ] ^ _
        0.50, 0.60, 0.65, 0.54, 0.67, 0.58, 0.40, 0.58, // ` a b c d e f g
        0.68, 0.36, 0.35, 0.64, 0.35, 1.02, 0.69, 0.64, // h i j k l m n o
        0.66, 0.65, 0.52, 0.52, 0.40, 0.68, 0.57, 0.87, // p q r s t u v w
        0.59, 0.57, 0.53, 0.50, 0.39, 0.50, 0.71, // x y z { | } ~
    ];

    // Anything not measured gets the widest measured glyph (W): 1 em is not an upper bound, and
    // overestimating only shrinks the text a little, while underestimating lets it overrun.
    internal static readonly double WidestAdvance = AsciiAdvance.Max();

    // Base letter of each U+00C0–U+024F character (é → e, Ö → O), so accented Latin takes its base
    // letter's advance; '*' where there is none (æ, ø, ß, ł), which takes the widest. From Unicode
    // canonical decomposition, as a table because the CLI runs with invariant globalization.
    private const string LatinBaseLetters =
        "AAAAAA*CEEEEIIII*NOOOOO**UUUUY**aaaaaa*ceeeeiiii" +
        "*nooooo**uuuuy*yAaAaAaCcCcCcCcDd**EeEeEeEeEeGgGg" +
        "GgGgHh**IiIiIiIiI***JjKk*LlLlLl****NnNnNn***OoOo" +
        "Oo**RrRrRrSsSsSsSsTtTt**UuUuUuUuUuUuWwYyYZzZzZz*" +
        "********************************Oo*************U" +
        "u****************************AaIiOoUuUuUuUuUu*Aa" +
        "Aa****GgKkOoOo**j***Gg**NnAa****AaAaEeEeIiIiOoOo" +
        "RrRrUuUuSsTt**Hh******AaEeOoOoOoOoYy************" +
        "****************";

    // Baseline below the top of a row, and the row's height, in em.
    private const double Ascent = 0.8;
    private const double LineHeight = 1.05;

    private const string PaperColor = "#fff";
    private const string PaperEdgeColor = "#bbb";
    private const string FontFamily = "Georgia, 'Times New Roman', serif";

    internal sealed record Item(string ClassName, string Text, StampCorner Corner, double Size, bool Bold, bool Italic)
    {
        public bool IsTop => Corner is StampCorner.TopLeft or StampCorner.TopRight;

        public bool IsRight => Corner is StampCorner.TopRight or StampCorner.BottomRight;
    }

    public static string Write(EncodedImage image, PostageStampOptions options)
    {
        double unit = Math.Min(image.Width, image.Height);
        var inkColor = options.InkColor;
        var items = Items(options, unit);
        var top = FitRow([.. items.Where(i => i.IsTop)], image.Width, unit);
        var bottom = FitRow([.. items.Where(i => !i.IsTop)], image.Width, unit);

        // The paper sits inset by its edge stroke, so the stroke isn't clipped by the canvas.
        var origin = PaperStrokeWidth * unit;
        var margin = Margin * unit;
        var gap = TextGap * unit;
        var topHeight = RowHeight(top, gap);
        var picture = (X: origin + margin, Y: origin + margin + topHeight, image.Width, image.Height);
        var paperWidth = image.Width + (2 * margin);
        var paperHeight = topHeight + image.Height + RowHeight(bottom, gap) + (2 * margin);
        var width = (int)Math.Ceiling(paperWidth + (2 * origin));
        var height = (int)Math.Ceiling(paperHeight + (2 * origin));

        // Always clean: effects and postmarks are separate artwork, combined on a canvas later.
        // Every value, the perforated path included, is a fraction of the picture, so all are
        // written with Number: at tenths, thin strokes round to 0 on small pictures, the paper's
        // inset rounds away and clips its edge, the frame drifts off the picture, and font sizes
        // that FitRow made just fit can overflow again.
        return SvgWriter.WriteDocument(width, height, inkColor, SvgStyle.Clean, (xml, _) =>
        {
            xml.WriteStartElement("path", Namespace);
            xml.WriteAttributeString("class", "stamp-paper");
            xml.WriteAttributeString("d", PerforatedEdge(origin, paperWidth, paperHeight, PerforationPitch * unit));
            xml.WriteAttributeString("fill", PaperColor);
            xml.WriteAttributeString("stroke", PaperEdgeColor);
            xml.WriteAttributeString("stroke-width", Number(PaperStrokeWidth * unit));
            xml.WriteEndElement();

            xml.WriteStartElement("image", Namespace);
            xml.WriteAttributeString("class", "stamp-picture");
            xml.WriteAttributeString("x", Number(picture.X));
            xml.WriteAttributeString("y", Number(picture.Y));
            xml.WriteAttributeString("width", Integer(picture.Width));
            xml.WriteAttributeString("height", Integer(picture.Height));
            xml.WriteAttributeString("preserveAspectRatio", "none");
            xml.WriteAttributeString("href", image.DataUri);
            xml.WriteEndElement();

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "stamp-print");
            xml.WriteAttributeString("fill", inkColor);
            xml.WriteAttributeString("font-family", FontFamily);

            var frameWidth = FrameStrokeWidth * unit;
            xml.WriteStartElement("rect", Namespace);
            xml.WriteAttributeString("x", Number(picture.X - (frameWidth / 2)));
            xml.WriteAttributeString("y", Number(picture.Y - (frameWidth / 2)));
            xml.WriteAttributeString("width", Number(picture.Width + frameWidth));
            xml.WriteAttributeString("height", Number(picture.Height + frameWidth));
            xml.WriteAttributeString("fill", "none");
            xml.WriteAttributeString("stroke", inkColor);
            xml.WriteAttributeString("stroke-width", Number(frameWidth));
            xml.WriteEndElement();

            var left = picture.X;
            var right = picture.X + picture.Width;
            WriteRow(xml, top, origin + margin + (RowSize(top) * Ascent), left, right);
            WriteRow(xml, bottom, picture.Y + picture.Height + gap + (RowSize(bottom) * Ascent), left, right);

            xml.WriteEndElement();
        });
    }

    private static List<Item> Items(PostageStampOptions options, double unit)
    {
        List<Item> items = [new("stamp-country", options.Country, options.CountryCorner, CountrySize * unit, Bold: true, Italic: false)];
        if (options.Fee is { } fee)
        {
            items.Add(new("stamp-fee", fee, options.FeeCorner, FeeSize * unit, Bold: true, Italic: false));
        }

        if (options.Caption is { } caption)
        {
            items.Add(new("stamp-caption", caption, options.CaptionCorner, CaptionSize * unit, Bold: false, Italic: true));
        }

        return items;
    }

    // Shrinks a row's type evenly when its estimated width would run past the picture. Only the
    // text scales: the items are anchored to the picture's edges, so the gap between them stays
    // fixed and the text gets the space that remains. (A row holds at most two items, and the gap
    // is a small fraction of the picture's shorter side, so that space is always positive.)
    internal static List<Item> FitRow(List<Item> row, double available, double unit)
    {
        var text = row.Sum(EstimateWidth);
        var gap = Gap(row, unit);
        if (text + gap <= available)
        {
            return row;
        }

        var scale = (available - gap) / text;
        return [.. row.Select(i => i with { Size = i.Size * scale })];
    }

    /// <summary>The estimated width a row takes: its text plus the fixed gaps between items.</summary>
    internal static double RowWidth(List<Item> row, double unit) => row.Sum(EstimateWidth) + Gap(row, unit);

    private static double Gap(List<Item> row, double unit) => Math.Max(0, row.Count - 1) * ItemGap * unit;

    private static double EstimateWidth(Item item)
    {
        var em = 0.0;
        foreach (var rune in item.Text.EnumerateRunes())
        {
            em += rune.Value switch
            {
                >= 0x20 and < 0x7F => AsciiAdvance[rune.Value - 0x20],
                >= 0xC0 and <= 0x24F when LatinBaseLetters[rune.Value - 0xC0] is not '*' and var b => AsciiAdvance[b - 0x20],
                _ when Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark => 0,
                < 0x20 or 0x7F => 0, // control characters
                _ => WidestAdvance,
            };
        }

        return em * item.Size;
    }

    private static double RowSize(List<Item> row) => row.Count == 0 ? 0 : row.Max(i => i.Size);

    private static double RowHeight(List<Item> row, double gap) => row.Count == 0 ? 0 : (RowSize(row) * LineHeight) + gap;

    // Items in one row share a baseline, so different sizes still line up.
    private static void WriteRow(XmlWriter xml, List<Item> row, double baseline, double left, double right)
    {
        foreach (var item in row)
        {
            xml.WriteStartElement("text", Namespace);
            xml.WriteAttributeString("class", item.ClassName);
            xml.WriteAttributeString("x", Number(item.IsRight ? right : left));
            xml.WriteAttributeString("y", Number(baseline));
            xml.WriteAttributeString("font-size", Number(item.Size));
            if (item.Bold)
            {
                xml.WriteAttributeString("font-weight", "bold");
            }

            if (item.Italic)
            {
                xml.WriteAttributeString("font-style", "italic");
            }

            if (item.IsRight)
            {
                xml.WriteAttributeString("text-anchor", "end");
            }

            xml.WriteString(item.Text);
            xml.WriteEndElement();
        }
    }

    /// <summary>
    /// The paper outline: a rectangle with semicircular bites along every edge, where the sheet
    /// was torn along its perforations. Each side gets a whole number of holes, spaced evenly.
    /// The outline runs clockwise, so sweep flag 0 turns each arc into the paper. Like every other
    /// layout value it is written with <see cref="SvgFormat.Number"/>, in absolute coordinates so
    /// rounding can't build up along the edge; next to the embedded picture its size is negligible.
    /// </summary>
    internal static string PerforatedEdge(double origin, double width, double height, double pitch)
    {
        var sb = new StringBuilder();
        var r = PerforationRadius * pitch;
        var radius = Number(r);
        sb.Append('M').Append(Number(origin)).Append(' ').Append(Number(origin));

        void LineTo(double x, double y) =>
            sb.Append('L').Append(Number(origin + x)).Append(' ').Append(Number(origin + y));

        void BiteTo(double x, double y) =>
            sb.Append('A').Append(radius).Append(' ').Append(radius).Append(" 0 0 0 ")
              .Append(Number(origin + x)).Append(' ').Append(Number(origin + y));

        var (across, down) = (Holes(width, pitch), Holes(height, pitch));
        for (var i = 0; i < across.Count; i++)
        {
            LineTo(across.Centre(i) - r, 0);
            BiteTo(across.Centre(i) + r, 0);
        }

        LineTo(width, 0);
        for (var i = 0; i < down.Count; i++)
        {
            LineTo(width, down.Centre(i) - r);
            BiteTo(width, down.Centre(i) + r);
        }

        LineTo(width, height);
        for (var i = across.Count - 1; i >= 0; i--)
        {
            LineTo(across.Centre(i) + r, height);
            BiteTo(across.Centre(i) - r, height);
        }

        LineTo(0, height);
        for (var i = down.Count - 1; i >= 0; i--)
        {
            LineTo(0, down.Centre(i) + r);
            BiteTo(0, down.Centre(i) - r);
        }

        sb.Append('Z');
        return sb.ToString();
    }

    private readonly record struct HoleRow(int Count, double Step)
    {
        public double Centre(int i) => (i + 0.5) * Step;
    }

    private static HoleRow Holes(double length, double pitch)
    {
        // Clamp before converting: length / pitch can exceed int.MaxValue.
        var count = (int)Math.Clamp(Math.Round(length / pitch), 2, MaxHolesPerSide);
        return new HoleRow(count, length / count);
    }
}
