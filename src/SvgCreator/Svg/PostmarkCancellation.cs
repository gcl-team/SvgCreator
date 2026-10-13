using System.Text;
using System.Xml;
using SvgCreator.Fitting;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Svg;

/// <summary>
/// Postal cancellation for <see cref="OutputStyle.Postmark"/>. The drawing's circular frame (if one
/// was recognized) is the postmark; otherwise a rectangular border inset from the canvas edges turns
/// the whole picture into the stamp. Parallel waves then overlap the frame's right side and run a
/// little past it, as real cancellations do, which keeps the canvas growth modest.
/// </summary>
internal sealed class PostmarkCancellation : IDecoration
{
    // Sizes are fractions of the frame's vertical half-height, so the mark scales with the frame.
    private const int CancelLineCount = 6;
    private const double CancelBandHeight = 1.2;    // the lines span 60% of the frame's height
    private const double CancelOverlap = 0.5;       // how far the lines reach into the frame
    private const double CancelOverhang = 0.6;      // how far they run past it
    private const int CancelHalfWaves = 6;
    private const double CancelAmplitude = 0.3;     // fraction of the line spacing
    // The waves are drawn as thick as the frame's ring when it is a band (an outline with a
    // concentric hole), so they read as part of the same stamp. A frame without a band, such as a
    // solid disc, and the rectangle fallback use this share of the frame's half-height instead.
    // Either way, the floor keeps them visible through the worn-ink filter.
    private const double CancelStrokeWidth = 0.018;
    private const double CancelMinStrokeWidth = 1.5;

    // An ellipse only becomes the postmark when it is a frame, not a small detail like an eye.
    private const double MinFrameFraction = 0.25;

    // Rectangular frame inset from the canvas edge, as a fraction of the shorter side.
    private const double RectFrameInset = 0.01;

    private readonly record struct RectFrame(double X, double Y, double Width, double Height);

    private readonly string pathData;
    private readonly double strokeWidth;
    private readonly RectFrame? frame;

    private PostmarkCancellation(string pathData, double strokeWidth, int requiredWidth, RectFrame? frame)
    {
        this.pathData = pathData;
        this.strokeWidth = strokeWidth;
        this.frame = frame;
        RequiredWidth = requiredWidth;
    }

    public int RequiredWidth { get; }

    public static PostmarkCancellation For(TracedDrawing drawing)
    {
        var (width, height) = (drawing.Width, drawing.Height);
        PointD center;
        double halfWidth, halfHeight, strokeWidth;
        RectFrame? rect = null;

        if (FindFrame(drawing.Contours, width, height) is { Ellipse: { } frame } frameContour)
        {
            var angle = frame.AngleDegrees * Math.PI / 180;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            center = frame.Center;
            halfWidth = Math.Sqrt(Math.Pow(frame.RadiusX * cos, 2) + Math.Pow(frame.RadiusY * sin, 2));
            halfHeight = Math.Sqrt(Math.Pow(frame.RadiusX * sin, 2) + Math.Pow(frame.RadiusY * cos, 2));
            strokeWidth = Math.Max(CancelMinStrokeWidth, RingWidth(drawing.Contours, frameContour) ?? (CancelStrokeWidth * halfHeight));
        }
        else
        {
            // No circle: the whole picture is the stamp.
            strokeWidth = Math.Max(CancelMinStrokeWidth, CancelStrokeWidth * height / 2.0);
            var inset = (strokeWidth / 2) + Math.Max(strokeWidth, RectFrameInset * Math.Min(width, height));
            rect = new RectFrame(inset, inset, width - (2 * inset), height - (2 * inset));
            center = new PointD(width / 2.0, height / 2.0);
            halfWidth = (width / 2.0) - inset;
            halfHeight = (height / 2.0) - inset;
        }

        var band = CancelBandHeight * halfHeight;
        var spacing = band / (CancelLineCount - 1);
        var length = (CancelOverlap + CancelOverhang) * halfHeight;
        var halfWave = length / CancelHalfWaves;
        var amplitude = CancelAmplitude * spacing;
        var startX = center.X + halfWidth - (CancelOverlap * halfHeight);

        // A quadratic whose control point is offset by 2A peaks at A, so q + repeated t (which
        // mirrors the previous control point) traces a sine-like wave in a few bytes per hump.
        var halfWaveTenths = ToTenths(halfWave);
        var controlXTenths = halfWaveTenths / 2;
        var controlYTenths = ToTenths(-2 * amplitude);

        var sb = new StringBuilder();
        for (var i = 0; i < CancelLineCount; i++)
        {
            var start = Tenths(new PointD(startX, center.Y - (band / 2) + (i * spacing)));
            sb.Append('M');
            AppendNumber(sb, start.X);
            AppendNumber(sb, start.Y);
            sb.Append('q');
            AppendNumber(sb, controlXTenths);
            AppendNumber(sb, controlYTenths);
            AppendNumber(sb, halfWaveTenths);
            AppendNumber(sb, 0);
            for (var k = 1; k < CancelHalfWaves; k++)
            {
                sb.Append('t');
                AppendNumber(sb, halfWaveTenths);
                AppendNumber(sb, 0);
            }
        }

        var end = startX + length + (2 * strokeWidth);
        return new PostmarkCancellation(sb.ToString(), strokeWidth, (int)Math.Ceiling(end), rect);
    }

    public void Write(XmlWriter xml, string color, string? filterId)
    {
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "postmark-cancel");
        xml.WriteAttributeString("fill", "none");
        xml.WriteAttributeString("stroke", color);
        xml.WriteAttributeString("stroke-width", Format(strokeWidth));
        xml.WriteAttributeString("stroke-linecap", "round");
        if (filterId is not null)
        {
            xml.WriteAttributeString("filter", $"url(#{filterId})");
        }

        if (frame is { } rect)
        {
            xml.WriteStartElement("rect", Namespace);
            xml.WriteAttributeString("x", Format(rect.X));
            xml.WriteAttributeString("y", Format(rect.Y));
            xml.WriteAttributeString("width", Format(rect.Width));
            xml.WriteAttributeString("height", Format(rect.Height));
            xml.WriteEndElement();
        }

        xml.WriteStartElement("path", Namespace);
        xml.WriteAttributeString("d", pathData);
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    /// <summary>The drawing's circular frame, which the cancellation runs from; none when it has no circle.</summary>
    public static Ellipse? FrameOf(TracedDrawing drawing) => FindFrame(drawing.Contours, drawing.Width, drawing.Height)?.Ellipse;

    // The outline of the largest recognized ellipse big enough to be the drawing's frame.
    private static CurveContour? FindFrame(IReadOnlyList<CurveContour> contours, int width, int height)
    {
        CurveContour? frame = null;
        foreach (var contour in contours)
        {
            if (contour is { IsHole: false, Ellipse: { } e }
                && e.RadiusY >= MinFrameFraction * Math.Min(width, height)
                && (frame?.Ellipse is not { } best || e.RadiusX > best.RadiusX))
            {
                frame = contour;
            }
        }

        return frame;
    }

    // The width of the frame's ring: its outline paired with a concentric elliptical hole in the
    // same component, as the writer pairs them into one stroked circle. None for a solid frame.
    private static double? RingWidth(IReadOnlyList<CurveContour> contours, CurveContour frame)
    {
        foreach (var contour in contours)
        {
            if (contour is { IsHole: true, Ellipse: { } hole }
                && contour.Label == frame.Label
                && Ellipse.TryMakeRing(frame.Ellipse!.Value, hole, SvgWriter.RingTolerance, out _, out var width))
            {
                return width;
            }
        }

        return null;
    }
}
