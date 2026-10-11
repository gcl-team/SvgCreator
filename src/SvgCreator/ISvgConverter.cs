namespace SvgCreator;

/// <summary>
/// Converts raster images into line-art SVG documents.
/// </summary>
public interface ISvgConverter
{
    /// <summary>Converts an encoded image (PNG, JPEG, BMP, GIF, WebP, ...) to an SVG document.</summary>
    /// <param name="image">Stream containing the encoded image.</param>
    /// <param name="options">Conversion options; <see cref="SvgConversionOptions.Default"/> when <see langword="null"/>.</param>
    /// <returns>The SVG document as a string.</returns>
    string Convert(Stream image, SvgConversionOptions? options = null);

    /// <inheritdoc cref="Convert(Stream, SvgConversionOptions?)"/>
    /// <param name="image">Stream containing the encoded image.</param>
    /// <param name="options">Conversion options; <see cref="SvgConversionOptions.Default"/> when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to cancel the conversion.</param>
    Task<string> ConvertAsync(Stream image, SvgConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Converts the image file at <paramref name="path"/> to an SVG document.</summary>
    /// <param name="path">Path to the image file.</param>
    /// <param name="options">Conversion options; <see cref="SvgConversionOptions.Default"/> when <see langword="null"/>.</param>
    /// <returns>The SVG document as a string.</returns>
    string ConvertFile(string path, SvgConversionOptions? options = null);
}
