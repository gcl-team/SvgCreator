using SvgCreator.Fitting;
using SvgCreator.Imaging;
using SvgCreator.Svg;
using SvgCreator.Tracing;

namespace SvgCreator;

/// <summary>
/// Default <see cref="ISvgConverter"/>: grayscale → line extraction → connected components →
/// edge contour tracing → Bézier curve fitting → SVG.
/// </summary>
public sealed class SvgConverter : ISvgConverter
{
    /// <inheritdoc/>
    public string Convert(Stream image, SvgConversionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= SvgConversionOptions.Default;
        options.Validate();

        return Run(ImageLoader.Load(image), options, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task<string> ConvertAsync(Stream image, SvgConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= SvgConversionOptions.Default;
        options.Validate();

        var rgb = await ImageLoader.LoadAsync(image, cancellationToken).ConfigureAwait(false);
        return Run(rgb, options, cancellationToken);
    }

    /// <inheritdoc/>
    public string ConvertFile(string path, SvgConversionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Convert(stream, options);
    }

    private static string Run(RgbImage rgb, SvgConversionOptions options, CancellationToken cancellationToken)
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
        return SvgWriter.Write(gray.Width, gray.Height, curves, options.FillColor);
    }
}
