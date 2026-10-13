using SvgCreator.Imaging;
using SvgCreator.Svg;

namespace SvgCreator;

/// <summary>
/// Default <see cref="ISvgConverter"/>: traces the image (<see cref="LineArtTracer"/>), then renders
/// the drawing in the chosen <see cref="OutputStyle"/> (<see cref="SvgWriter"/>). A
/// <see cref="PostageStampLayout"/> embeds the image untraced instead (<see cref="PostageStampWriter"/>).
/// </summary>
public sealed class SvgConverter : ISvgConverter
{
    /// <inheritdoc/>
    public string Convert(Stream image, SvgConversionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= SvgConversionOptions.Default;
        options.Validate();

        if (options.Layout is PostageStampLayout stamp)
        {
            return PostageStampWriter.Write(ImageLoader.LoadEncoded(image), stamp, options.FillColor, options.Style, options.StampSeed);
        }

        return Run(ImageLoader.Load(image), options, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task<string> ConvertAsync(Stream image, SvgConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= SvgConversionOptions.Default;
        options.Validate();

        if (options.Layout is PostageStampLayout stamp)
        {
            var encoded = await ImageLoader.LoadEncodedAsync(image, cancellationToken).ConfigureAwait(false);
            return PostageStampWriter.Write(encoded, stamp, options.FillColor, options.Style, options.StampSeed);
        }

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
        var drawing = LineArtTracer.Trace(rgb, options, cancellationToken);

        // The stamp texture follows line thickness, which LineSigma sets in Lines mode.
        var stampScale = options.Mode == ExtractionMode.Lines ? options.LineSigma : 1.0;
        var style = SvgStyle.For(options.Style, options.StampSeed, stampScale, () => PostmarkCancellation.For(drawing));
        return SvgWriter.Write(drawing, options.FillColor, style);
    }
}
