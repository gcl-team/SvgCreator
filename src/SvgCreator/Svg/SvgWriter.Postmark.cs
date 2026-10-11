using System.Text;
using System.Xml;
using SvgCreator.Fitting;

namespace SvgCreator.Svg;

/// <summary>
/// Postal cancellation for <see cref="OutputStyle.Postmark"/>. The drawing's circular frame (if one
/// was recognized) is the postmark; otherwise a rectangular border inset from the canvas edges turns
/// the whole picture into the stamp. Parallel waves then overlap the frame's right side and run a
/// little past it, as real cancellations do, which keeps the canvas growth modest.
/// </summary>
internal static partial class SvgWriter
{
    // Sizes are fractions of the frame's vertical half-height, so the mark scales with the frame.
    private const int CancelLineCount = 6;
    private const double CancelBandHeight = 1.2;    // the lines span 60% of the frame's height
    private const double CancelOverlap = 0.5;       // how far the lines reach into the frame
    private const double CancelOverhang = 0.6;      // how far they run past it
    private const int CancelHalfWaves = 6;
    private const double CancelAmplitude = 0.3;     // fraction of the line spacing
    private const double CancelStrokeWidth = 0.018; // ≈ the ring's own stroke on typical seals
    private const double CancelMinStrokeWidth = 1.5;

    // An ellipse only becomes the postmark when it is a frame, not a small detail like an eye.
    private const double MinFrameFraction = 0.25;

    // Rectangular frame inset from the canvas edge, as a fraction of the shorter side.
    private const double RectFrameInset = 0.01;

    private readonly record struct Cancellation(string PathData, double StrokeWidth, int RequiredWidth, RectFrame? Frame);

    private readonly record struct RectFrame(double X, double Y, double Width, double Height);

    private static Cancellation BuildCancellation(IReadOnlyList<CurveContour> contours, int width, int height)
    {
        PointD center;
        double halfWidth, halfHeight, strokeWidth;
        RectFrame? rect = null;

        if (FindEllipseFrame(contours, width, height) is { } frame)
        {
            var angle = frame.AngleDegrees * Math.PI / 180;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            center = frame.Center;
            halfWidth = Math.Sqrt(Math.Pow(frame.RadiusX * cos, 2) + Math.Pow(frame.RadiusY * sin, 2));
            halfHeight = Math.Sqrt(Math.Pow(frame.RadiusX * sin, 2) + Math.Pow(frame.RadiusY * cos, 2));
            strokeWidth = Math.Max(CancelMinStrokeWidth, CancelStrokeWidth * halfHeight);
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
        var halfWaveTenths = (long)Math.Round(halfWave * 10, MidpointRounding.AwayFromZero);
        var controlXTenths = halfWaveTenths / 2;
        var controlYTenths = (long)Math.Round(-2 * amplitude * 10, MidpointRounding.AwayFromZero);

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
        return new Cancellation(sb.ToString(), strokeWidth, (int)Math.Ceiling(end), rect);
    }

    private static void WriteCancellation(XmlWriter xml, Cancellation cancellation, string color, string? filterId)
    {
        xml.WriteStartElement("g", SvgNamespace);
        xml.WriteAttributeString("class", "postmark-cancel");
        xml.WriteAttributeString("fill", "none");
        xml.WriteAttributeString("stroke", color);
        xml.WriteAttributeString("stroke-width", Format(cancellation.StrokeWidth));
        xml.WriteAttributeString("stroke-linecap", "round");
        if (filterId is not null)
        {
            xml.WriteAttributeString("filter", $"url(#{filterId})");
        }

        if (cancellation.Frame is { } frame)
        {
            xml.WriteStartElement("rect", SvgNamespace);
            xml.WriteAttributeString("x", Format(frame.X));
            xml.WriteAttributeString("y", Format(frame.Y));
            xml.WriteAttributeString("width", Format(frame.Width));
            xml.WriteAttributeString("height", Format(frame.Height));
            xml.WriteEndElement();
        }

        xml.WriteStartElement("path", SvgNamespace);
        xml.WriteAttributeString("d", cancellation.PathData);
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    // The largest recognized ellipse big enough to be the drawing's frame.
    private static Ellipse? FindEllipseFrame(IReadOnlyList<CurveContour> contours, int width, int height)
    {
        Ellipse? frame = null;
        foreach (var contour in contours)
        {
            if (contour is { IsHole: false, Ellipse: { } e }
                && e.RadiusY >= MinFrameFraction * Math.Min(width, height)
                && (frame is null || e.RadiusX > frame.Value.RadiusX))
            {
                frame = e;
            }
        }

        return frame;
    }
}
