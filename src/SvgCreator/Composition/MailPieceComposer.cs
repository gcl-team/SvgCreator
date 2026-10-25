using SvgCreator.Imaging;
using SvgCreator.Svg;

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

    // The postmark ring, as fractions of the stamp's height: how far it reaches above and below
    // its centre, and its centre's height from the stamp's top. Both reaches are measured to the
    // ring's outer edge, which is what the eye sees: a thick ring is written as one circle stroked
    // along its midline, but the stroke reaches out to the outer ellipse, and that visible extent
    // is also what the cover rule below must bound. For an elliptical frame, the vertical reach
    // is sized and the horizontal reach is what the cover rule bounds. The centre sits on the stamp's left edge, so the ring overlaps the
    // stamp and the waves, which start inside the ring's right side, cross it.
    private const double RingRadius = 0.42;
    private const double RingCentreY = 0.62;

    // The ring's size follows the stamp's height, so on a narrow stamp it would cover most of the
    // picture: a portrait stamp is only about 0.75 of its height wide. The ring is moved left until
    // it covers at most this share of the stamp's width. Wide stamps don't move; the largest shift
    // (about 0.17 of the height) leaves the waves, which reach about 0.67 past the centre, well
    // across the stamp.
    private const double MaxRingCover = 1.0 / 3;

    // A postmark without a circle is the whole traced picture: this tall (or less, if it is too
    // wide for the envelope), its right quarter over the stamp's left edge.
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
        var stamp = StampRect(stampSvg is null ? null : NestedSvg.Size(stampSvg), width, unit);

        string? postmarkSvg = null;
        Rect postmarkRect = default;
        if (postmark is { } m)
        {
            var drawing = LineArtTracer.Trace(m.Image, m.Options, cancellationToken);
            postmarkSvg = SvgConverter.Render(drawing, m.Options);
            postmarkRect = PostmarkRect(NestedSvg.Size(postmarkSvg), PostmarkCancellation.FrameOf(drawing), drawing.Height, stamp, (width, height));
        }

        return SvgWriter.WriteDocument(width, height, envelope.PaperColor, SvgStyle.Clean, (xml, _) =>
        {
            NestedSvg.Nest(xml, "mail-envelope", EnvelopeWriter.Write(envelope), (0, 0, width, height));
            if (stampSvg is not null)
            {
                NestedSvg.Nest(xml, "mail-postage-stamp", stampSvg, (stamp.X, stamp.Y, stamp.Width, stamp.Height));
            }

            if (postmarkSvg is not null)
            {
                NestedSvg.Nest(xml, "mail-postmark", postmarkSvg, (postmarkRect.X, postmarkRect.Y, postmarkRect.Width, postmarkRect.Height));
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
    /// Either way, the whole postmark is kept on the envelope, made smaller if it must be.
    /// </summary>
    internal static Rect PostmarkRect((double Width, double Height) size, Fitting.Ellipse? frame, int drawingHeight, Rect stamp, (double Width, double Height) envelope)
    {
        var envelopeWidth = envelope.Width;
        if (frame is { } ring)
        {
            // The frame may be an ellipse, and rotated: size its vertical reach, and bound the
            // cover by its horizontal reach at that size.
            var (halfWidth, halfHeight) = ring.HalfExtents;
            var cover = MaxRingCover * stamp.Width;

            // A wide frame, moved left by the cover rule, may run off the envelope, and its
            // waves off the right. The canvas's left edge is stamp.X − Cx·s − max(0, hw·s − cover)
            // and its right edge that plus W·s. Each is the lesser of two lines in the scale s (the
            // ring moved or not): the left edge fits only if both lines do, the right if either does.
            // Vertically the ring's centre doesn't move, so a frame off the drawing's centre (with
            // artwork above or below it) only has to fit the room above and below that centre.
            var cy = stamp.Y + (RingCentreY * stamp.Height);
            var (left, right) = (ring.Center.X, size.Width - ring.Center.X);
            var (above, below) = (ring.Center.Y, size.Height - ring.Center.Y);
            var scale = Math.Min(
                Math.Min(RingRadius * stamp.Height / halfHeight, Math.Min(Limit(cy, above), Limit(envelope.Height - cy, below))),
                Math.Min(
                    Math.Min(Limit(stamp.X, left), Limit(stamp.X + cover, left + halfWidth)),
                    Math.Max(Limit(envelopeWidth - stamp.X, right), Limit(envelopeWidth - stamp.X - cover, right - halfWidth))));
            var shift = Math.Max(0, (halfWidth * scale) - cover);
            var cx = stamp.X - shift;
            return new Rect(cx - (ring.Center.X * scale), cy - (ring.Center.Y * scale), size.Width * scale, size.Height * scale);
        }

        // A wide picture is narrowed until it fits on the envelope on both sides of the stamp's
        // left edge, keeping its quarter over the stamp. It always fits vertically: it is shorter
        // than the stamp and centred on it.
        var pictureScale = Math.Min(
            PictureHeight * stamp.Height / drawingHeight,
            Math.Min(stamp.X / (1 - PictureOverlap), (envelopeWidth - stamp.X) / PictureOverlap) / size.Width);
        var (w, h) = (size.Width * pictureScale, size.Height * pictureScale);
        return new Rect(stamp.X - (w * (1 - PictureOverlap)), stamp.Y + ((stamp.Height - h) / 2), w, h);
    }

    // The largest scale at which a length growing by perScale fits in room (none if it doesn't grow).
    private static double Limit(double room, double perScale) => perScale > 0 ? room / perScale : double.PositiveInfinity;
}
