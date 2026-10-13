using SvgCreator.Fitting;
using SvgCreator.Imaging;
using SvgCreator.Tracing;

namespace SvgCreator;

/// <summary>
/// The tracing half of the pipeline: grayscale → line extraction → connected components →
/// edge contour tracing → Bézier curve fitting.
/// </summary>
internal static class LineArtTracer
{
    public static TracedDrawing Trace(RgbImage rgb, SvgConversionOptions options, CancellationToken cancellationToken)
    {
        var gray = rgb.ToGray();
        var binary = options.Mode == ExtractionMode.Lines
            ? LineExtractor.Extract(
                gray,
                options.ColorContrast > 0 ? rgb.ToLabChroma() : null,
                options.LineSigma,
                options.LineContrast,
                options.WeakLineContrast,
                options.ColorContrast)
            : Binarizer.Apply(gray, options.Threshold);
        cancellationToken.ThrowIfCancellationRequested();

        var components = ComponentLabeler.Label(binary, options.MinComponentArea);
        cancellationToken.ThrowIfCancellationRequested();

        var contours = ContourTracer.Trace(components);
        cancellationToken.ThrowIfCancellationRequested();

        var curves = CurveBuilder.Build(contours, options.CurveTolerance, options.CornerAngle, options.DetectEllipses);
        return new TracedDrawing(gray.Width, gray.Height, curves);
    }
}
