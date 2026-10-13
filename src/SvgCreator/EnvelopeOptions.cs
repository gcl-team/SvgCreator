namespace SvgCreator;

/// <summary>How the envelope's paper looks.</summary>
public enum EnvelopeStyle
{
    /// <summary>Crisp, flat paper.</summary>
    Flat,

    /// <summary>
    /// Crumpled paper: seeded straight creases, each a fading shadow with a highlight along the fold,
    /// which overlap into facets, plus slightly irregular edges and paper grain. The creases sit in
    /// their own group, so removing it flattens the envelope.
    /// </summary>
    Crumpled,
}

/// <summary>
/// Options for <see cref="EnvelopeGenerator"/>: an airmail envelope, paper with a border of
/// slanted straps alternating between two colours with paper-coloured gaps. The envelope is a
/// separate piece of artwork, to be combined with others (such as a postage stamp) on a canvas later.
/// </summary>
public sealed record EnvelopeOptions
{
    /// <summary>Width of the envelope (and the SVG canvas) in pixels. Default: 1100.</summary>
    public int Width { get; init; } = 1100;

    /// <summary>Height of the envelope (and the SVG canvas) in pixels. Default: 550.</summary>
    public int Height { get; init; } = 550;

    /// <summary>How the paper looks. Default: <see cref="EnvelopeStyle.Flat"/>.</summary>
    public EnvelopeStyle Style { get; init; } = EnvelopeStyle.Flat;

    /// <summary>
    /// <see cref="EnvelopeStyle.Crumpled"/>: seed for the creases. The same seed always gives the
    /// same crumple; a different one gives a differently crumpled envelope. Default: 1.
    /// </summary>
    public int Seed { get; init; } = 1;

    /// <summary>Paper colour, as a CSS colour. Default: off-white <c>#fbf8f1</c>.</summary>
    public string PaperColor { get; init; } = "#fbf8f1";

    /// <summary>Colour of every other strap, as a CSS colour. Default: airmail red <c>#d22630</c>.</summary>
    public string StrapColor { get; init; } = "#d22630";

    /// <summary>Colour of the straps in between, as a CSS colour. Default: airmail blue <c>#1d3f8f</c>.</summary>
    public string AlternateStrapColor { get; init; } = "#1d3f8f";

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Height);
        if (!Enum.IsDefined(Style))
        {
            throw new ArgumentOutOfRangeException(nameof(Style), Style, "Unknown envelope style.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(Seed);
        CssColor.ThrowIfInvalid(PaperColor, nameof(PaperColor));
        CssColor.ThrowIfInvalid(StrapColor, nameof(StrapColor));
        CssColor.ThrowIfInvalid(AlternateStrapColor, nameof(AlternateStrapColor));
    }
}
