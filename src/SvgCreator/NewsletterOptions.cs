namespace SvgCreator;

/// <summary>
/// Options for <see cref="NewsletterGenerator"/>: the column's width and the colours. The height
/// follows from the content. Every size in the layout is a fraction of the width, so a wider
/// column is the same newsletter drawn bigger, as an email is when zoomed.
/// </summary>
public sealed record NewsletterOptions
{
    /// <summary>Width of the newsletter's column in pixels, without the margin. Default: 640.</summary>
    public int Width { get; init; } = 640;

    /// <summary>
    /// The backdrop around the column on every side, in pixels, patterned with pixel diamonds.
    /// 0 leaves the column alone. Default: 40.
    /// </summary>
    public int Margin { get; init; } = 40;

    /// <summary>
    /// Pixels of each embedded picture per pixel it's drawn at. Pictures larger than that are
    /// scaled down (and saved as JPEG, or PNG where they're transparent) before they're embedded,
    /// which keeps a newsletter of phone photos small. 2 stays sharp on high-density screens.
    /// 0 embeds every picture unchanged. Default: 2.
    /// </summary>
    public double ImageDensity { get; init; } = 2;

    /// <summary>The backdrop around the column, as a CSS colour. Default: deep indigo <c>#231a45</c>.</summary>
    public string BackdropColor { get; init; } = "#231a45";

    /// <summary>The column's background, as a CSS colour. Default: purple <c>#5a4d7b</c>.</summary>
    public string PageColor { get; init; } = "#5a4d7b";

    /// <summary>The header and footer bands, as a CSS colour. Default: near black <c>#0b0b0f</c>.</summary>
    public string BandColor { get; init; } = "#0b0b0f";

    /// <summary>The text, as a CSS colour. Default: white <c>#ffffff</c>.</summary>
    public string TextColor { get; init; } = "#ffffff";

    /// <summary>
    /// The ribbons, the hero's wave and <c>[[marked]]</c> text, as a CSS colour. Default: orange <c>#f0764a</c>.
    /// </summary>
    public string AccentColor { get; init; } = "#f0764a";

    /// <summary>The buttons, as a CSS colour. Default: red <c>#a3292b</c>.</summary>
    public string ButtonColor { get; init; } = "#a3292b";

    /// <summary>The stats boxes and the maps' background, as a CSS colour. Default: dark purple <c>#463a66</c>.</summary>
    public string PanelColor { get; init; } = "#463a66";

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Width, 200);
        ArgumentOutOfRangeException.ThrowIfNegative(Margin);
        if (!double.IsFinite(ImageDensity) || ImageDensity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ImageDensity), ImageDensity, "ImageDensity must be 0 or a positive number.");
        }

        CssColor.ThrowIfInvalid(BackdropColor, nameof(BackdropColor));
        CssColor.ThrowIfInvalid(PageColor, nameof(PageColor));
        CssColor.ThrowIfInvalid(BandColor, nameof(BandColor));
        CssColor.ThrowIfInvalid(TextColor, nameof(TextColor));
        CssColor.ThrowIfInvalid(AccentColor, nameof(AccentColor));
        CssColor.ThrowIfInvalid(ButtonColor, nameof(ButtonColor));
        CssColor.ThrowIfInvalid(PanelColor, nameof(PanelColor));
    }
}
