namespace SvgCreator;

/// <summary>
/// Options for <see cref="NewsletterGenerator"/>: the column's width, the margin, how pictures are
/// embedded, and optionally a theme in place of the newsletter's own. The height follows from the
/// content. Every size in the layout is a fraction of the width, so a wider column is the same
/// newsletter drawn bigger, as an email is when zoomed.
/// </summary>
public sealed record NewsletterOptions
{
    /// <summary>Width of the newsletter's column in pixels, without the margin. Default: 640.</summary>
    public int Width { get; init; } = 640;

    /// <summary>
    /// The backdrop around the column on every side, in pixels, in the theme's backdrop colour and
    /// pattern. 0 leaves the column alone. Default: 40.
    /// </summary>
    public int Margin { get; init; } = 40;

    /// <summary>
    /// Pixels of each embedded picture per pixel it's drawn at. Pictures larger than that are
    /// scaled down (and saved as JPEG, or PNG where they're transparent) before they're embedded,
    /// which keeps a newsletter of phone photos small. 2 stays sharp on high-density screens.
    /// 0 embeds every picture unchanged. Default: 2.
    /// </summary>
    public double ImageDensity { get; init; } = 2;

    /// <summary>
    /// A theme to use in place of the newsletter's own <see cref="Newsletter.Theme"/>, such as one
    /// picked on the command line. Default: none, so the newsletter's theme is used, or
    /// <see cref="NewsletterTheme.Starlight"/> if it has none.
    /// </summary>
    public NewsletterTheme? Theme { get; init; }

    /// <summary>
    /// Make a template: every photo (the hero, cards and image blocks) left out, and a see-through
    /// hole of its shape cut through the page and backdrop in its place, so the newsletter can be
    /// laid over photos like a frame. Logos and stickers stay. Each hole has an invisible marker,
    /// <c>&lt;rect class="newsletter-slot" data-image="…"&gt;</c>, naming its photo. Default: false.
    /// </summary>
    public bool Template { get; init; }

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Width, 200);
        ArgumentOutOfRangeException.ThrowIfNegative(Margin);
        if (!double.IsFinite(ImageDensity) || ImageDensity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ImageDensity), ImageDensity, "ImageDensity must be 0 or a positive number.");
        }
    }
}
