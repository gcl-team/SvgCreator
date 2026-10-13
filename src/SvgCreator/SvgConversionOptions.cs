using System.Text.RegularExpressions;

namespace SvgCreator;

/// <summary>
/// Options controlling how an image is turned into line art.
/// </summary>
public sealed partial record SvgConversionOptions
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

    /// <summary>
    /// Recognize outlines that are circles or ellipses and write them as native SVG shapes
    /// (<c>&lt;circle&gt;</c>, <c>&lt;ellipse&gt;</c>, or a stroked one for rings) instead of Bézier
    /// curves: exact, smaller and easy to edit. Default: <see langword="true"/>.
    /// </summary>
    public bool DetectEllipses { get; init; } = true;

    /// <summary>How the ink is rendered. Default: <see cref="OutputStyle.Clean"/>.</summary>
    public OutputStyle Style { get; init; } = OutputStyle.Clean;

    /// <summary>
    /// <see cref="OutputStyle.Stamp"/>: seed for the stamp texture. The same seed always gives the
    /// same impression; a different one gives a differently worn print. Default: 1.
    /// </summary>
    public int StampSeed { get; init; } = 1;

    /// <summary>
    /// Ink colour, as a CSS colour: hex (<c>#c0392b</c>, <c>#000</c>, with optional alpha),
    /// a named colour (<c>navy</c>) or <c>rgb()</c>/<c>hsl()</c> notation. Default: <c>#000</c>.
    /// </summary>
    public string FillColor { get; init; } = "#000";

    /// <summary>
    /// Arranges the picture into a larger design, such as a <see cref="PostageStampLayout"/>.
    /// Default: <see langword="null"/>, the traced drawing alone.
    /// </summary>
    public OutputLayout? Layout { get; init; }

    internal void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unknown extraction mode.");
        }

        if (!Enum.IsDefined(Style))
        {
            throw new ArgumentOutOfRangeException(nameof(Style), Style, "Unknown output style.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(StampSeed);

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
        if (!CssColor().IsMatch(FillColor))
        {
            throw new ArgumentException(
                $"'{FillColor}' is not a valid colour. Use hex (#c0392b), a CSS name (navy) or rgb()/hsl().",
                nameof(FillColor));
        }

        Layout?.Validate();
    }

    // Hex (#rgb, #rgba, #rrggbb, #rrggbbaa), a CSS named colour, or rgb()/rgba()/hsl()/hsla().
    // Source-generated, so it is trimming- and Native AOT-safe.
    [GeneratedRegex(@"^(#([0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})|[a-zA-Z]+|(rgba?|hsla?)\([0-9a-z.,%/+\- ]+\))$")]
    private static partial Regex CssColor();
}
