namespace SvgCreator;

/// <summary>
/// Options controlling how an image is turned into line art.
/// </summary>
public sealed record SvgConversionOptions
{
    /// <summary>The default options.</summary>
    public static SvgConversionOptions Default { get; } = new();

    /// <summary>How ink is extracted from the image. Default: <see cref="ExtractionMode.Lines"/>.</summary>
    public ExtractionMode Mode { get; init; } = ExtractionMode.Lines;

    /// <summary>
    /// <see cref="ExtractionMode.Lines"/>: how many luminance levels (0-255) darker than its surroundings
    /// a pixel must be to count as a line. Lower finds fainter lines (and more noise). Default: 12.
    /// </summary>
    public double LineContrast { get; init; } = 12;

    /// <summary>
    /// <see cref="ExtractionMode.Lines"/>: weaker contrast that <em>continues</em> a line already started
    /// by <see cref="LineContrast"/> (hysteresis). Keeps lines closed where they fade, such as hair
    /// strands thinning to a tip, without picking up isolated faint texture. Set equal to
    /// <see cref="LineContrast"/> to disable. Default: 6.
    /// </summary>
    public double WeakLineContrast { get; init; } = 6;

    /// <summary>
    /// <see cref="ExtractionMode.Lines"/>: blur radius (Gaussian sigma, in pixels) applied before
    /// comparing a pixel with its surroundings. Larger ignores fine texture and suits thicker lines
    /// and higher-resolution images. Default: 1.
    /// </summary>
    public double LineSigma { get; init; } = 1.0;

    /// <summary>
    /// <see cref="ExtractionMode.Lines"/>: how different in colour (CIE ΔE in the a*b* plane,
    /// ignoring brightness) two neighbouring regions must be for their boundary to become a line.
    /// Finds boundaries between hues of similar brightness that the artist did not outline, such as
    /// pink hair on pale skin; boundaries that already have a drawn line are not doubled. Lower finds
    /// subtler boundaries (and more noise); 0 disables colour edges. Default: 20.
    /// </summary>
    public double ColorContrast { get; init; } = 20;

    /// <summary>
    /// <see cref="ExtractionMode.Threshold"/>: luminance (0-255) below which a pixel counts as ink.
    /// Transparent pixels are treated as white. Default: 128.
    /// </summary>
    public int Threshold { get; init; } = 128;

    /// <summary>
    /// Ink components with fewer pixels than this are discarded as noise. Default: 4.
    /// </summary>
    public int MinComponentArea { get; init; } = 4;

    /// <summary>
    /// Maximum distance in pixels the fitted Bézier curves may deviate from the traced outline.
    /// Larger values give fewer, smoother curves. Must be greater than 0. Default: 1.
    /// </summary>
    public double CurveTolerance { get; init; } = 1.0;

    /// <summary>
    /// Outline turns sharper than this angle in degrees (180 = straight) are kept as crisp corners
    /// instead of being smoothed. Must be in (0, 180]. Default: 135.
    /// </summary>
    public double CornerAngle { get; init; } = 135;

    /// <summary>SVG fill colour for the ink. Default: <c>#000</c>.</summary>
    public string FillColor { get; init; } = "#000";

    internal void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unknown extraction mode.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(LineContrast);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(WeakLineContrast);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(WeakLineContrast, LineContrast);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(LineSigma);
        ArgumentOutOfRangeException.ThrowIfNegative(ColorContrast);
        ArgumentOutOfRangeException.ThrowIfNegative(Threshold);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Threshold, 255);
        ArgumentOutOfRangeException.ThrowIfNegative(MinComponentArea);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(CurveTolerance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(CornerAngle);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(CornerAngle, 180);
        ArgumentException.ThrowIfNullOrWhiteSpace(FillColor);
    }
}
