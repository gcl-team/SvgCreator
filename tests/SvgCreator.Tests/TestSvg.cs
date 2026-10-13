using SvgCreator.Fitting;
using SvgCreator.Svg;

namespace SvgCreator.Tests;

internal static class TestSvg
{
    /// <summary>Renders contours on a canvas in a preset style, as <see cref="SvgConverter"/> does.</summary>
    public static string Write(
        int width, int height, IReadOnlyList<CurveContour> contours, string fillColor,
        OutputStyle style = OutputStyle.Clean, int stampSeed = 1, double stampScale = 1)
    {
        var drawing = new TracedDrawing(width, height, contours);
        return SvgWriter.Write(drawing, fillColor, SvgStyle.For(style, drawing, stampSeed, stampScale));
    }
}
