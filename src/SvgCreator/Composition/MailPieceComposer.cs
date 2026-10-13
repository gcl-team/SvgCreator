using System.Globalization;
using System.Xml;
using SvgCreator.Imaging;
using SvgCreator.Svg;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Composition;

/// <summary>
/// Lays out a mail piece: the envelope, the postage stamp in its top-right corner inside the strap
/// border, and the postmark with its ring over the stamp's left edge and its cancellation waves
/// across the stamp. Each piece is made by its own generator, unchanged, and nested as an inner
/// <c>&lt;svg&gt;</c>, which keeps its own coordinates; every id a piece uses already names what
/// its definition depends on, so pieces can't pick up each other's definitions.
/// </summary>
internal static class MailPieceComposer
{
    // Sizes are fractions of the envelope's shorter side, like the envelope's own.
    private const double StampHeight = 0.36;
    private const double MaxStampWidth = 0.45;   // of the envelope's width, for very wide pictures
    private const double CornerGap = 0.06;       // between the strap border and the stamp
    private const double BorderDepth = 0.07;     // EnvelopeWriter's strap border

    // The postmark ring, as fractions of the stamp's height: its radius, and its centre's height
    // from the stamp's top. The centre sits on the stamp's left edge, so the ring overlaps the
    // stamp and the waves, which start inside the ring's right side, cross it.
    private const double RingRadius = 0.42;
    private const double RingCentreY = 0.62;

    // A postmark without a circle is the whole traced picture: this tall, its right quarter over
    // the stamp's left edge.
    private const double PictureHeight = 0.8;
    private const double PictureOverlap = 0.25;

    public static string Compose(
        EnvelopeOptions envelope,
        (EncodedImage Image, PostageStampOptions Options)? postageStamp,
        (RgbImage Image, SvgConversionOptions Options)? postmark,
        CancellationToken cancellationToken)
    {
        var (width, height) = (envelope.Width, envelope.Height);
        double unit = Math.Min(width, height);

        // Where the stamp goes. Without a postage stamp, the postmark still goes where it would be.
        var stampSvg = postageStamp is { } p ? PostageStampWriter.Write(p.Image, p.Options) : null;
        var stamp = StampRect(stampSvg is null ? null : Size(stampSvg), width, unit);

        string? postmarkSvg = null;
        Rect postmarkRect = default;
        if (postmark is { } m)
        {
            var drawing = LineArtTracer.Trace(m.Image, m.Options, cancellationToken);
            postmarkSvg = SvgConverter.Render(drawing, m.Options);
            postmarkRect = PostmarkRect(Size(postmarkSvg), PostmarkCancellation.FrameOf(drawing), drawing.Height, stamp);
        }

        return SvgWriter.WriteDocument(width, height, envelope.PaperColor, SvgStyle.Clean, (xml, _) =>
        {
            Nest(xml, "mail-envelope", EnvelopeWriter.Write(envelope), new Rect(0, 0, width, height));
            if (stampSvg is not null)
            {
                Nest(xml, "mail-postage-stamp", stampSvg, stamp);
            }

            if (postmarkSvg is not null)
            {
                Nest(xml, "mail-postmark", postmarkSvg, postmarkRect);
            }
        });
    }

    internal readonly record struct Rect(double X, double Y, double Width, double Height);

    /// <summary>
    /// The postage stamp's place: in the top-right corner, a gap inside the strap border, scaled
    /// to a fixed share of the envelope's height (or narrower, for a very wide picture).
    /// </summary>
    internal static Rect StampRect((double Width, double Height)? size, double envelopeWidth, double unit)
    {
        var (w, h) = size ?? (3, 4); // no stamp: the shape of a typical portrait stamp
        var scale = Math.Min(StampHeight * unit / h, MaxStampWidth * envelopeWidth / w);
        var inset = (BorderDepth + CornerGap) * unit;
        return new Rect(envelopeWidth - inset - (w * scale), inset, w * scale, h * scale);
    }

    /// <summary>
    /// The postmark's place. With a circular frame, the ring is scaled to a share of the stamp's
    /// height and centred on the stamp's left edge; without one, the whole picture overlaps it.
    /// </summary>
    internal static Rect PostmarkRect((double Width, double Height) size, Fitting.Ellipse? frame, int drawingHeight, Rect stamp)
    {
        if (frame is { } ring)
        {
            var scale = RingRadius * stamp.Height / ring.RadiusY;
            var (cx, cy) = (stamp.X, stamp.Y + (RingCentreY * stamp.Height));
            return new Rect(cx - (ring.Center.X * scale), cy - (ring.Center.Y * scale), size.Width * scale, size.Height * scale);
        }

        var pictureScale = PictureHeight * stamp.Height / drawingHeight;
        var (w, h) = (size.Width * pictureScale, size.Height * pictureScale);
        return new Rect(stamp.X - (w * (1 - PictureOverlap)), stamp.Y + ((stamp.Height - h) / 2), w, h);
    }

    // A generated document's canvas size, from its root.
    private static (double Width, double Height) Size(string svg)
    {
        using var reader = XmlReader.Create(new StringReader(svg));
        reader.MoveToContent();
        return (double.Parse(reader.GetAttribute("width")!, CultureInfo.InvariantCulture), double.Parse(reader.GetAttribute("height")!, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes a generated document as an inner <c>&lt;svg&gt;</c> filling <paramref name="rect"/>:
    /// its view box maps its own canvas onto the rectangle, and its children are copied as they are.
    /// </summary>
    private static void Nest(XmlWriter xml, string className, string svg, Rect rect)
    {
        using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings { IgnoreWhitespace = true });
        reader.MoveToContent();

        xml.WriteStartElement("svg", Namespace);
        xml.WriteAttributeString("class", className);
        xml.WriteAttributeString("x", Number(rect.X));
        xml.WriteAttributeString("y", Number(rect.Y));
        xml.WriteAttributeString("width", Number(rect.Width));
        xml.WriteAttributeString("height", Number(rect.Height));
        xml.WriteAttributeString("viewBox", reader.GetAttribute("viewBox"));

        if (!reader.IsEmptyElement)
        {
            reader.Read();
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                xml.WriteNode(reader, defattr: true);
            }
        }

        xml.WriteEndElement();
    }
}
