using SvgCreator.Composition;
using SvgCreator.Imaging;

namespace SvgCreator;

/// <summary>
/// Default <see cref="INewsletterGenerator"/>: checks the content, reads each picture it names
/// once, and lays the column out (<see cref="NewsletterComposer"/>).
/// </summary>
public sealed class NewsletterGenerator : INewsletterGenerator
{
    /// <inheritdoc/>
    public string Create(Newsletter newsletter, NewsletterOptions options, Func<string, Stream> openImage)
    {
        ArgumentNullException.ThrowIfNull(openImage);
        Validate(newsletter, options);

        var images = new Dictionary<string, EncodedImage>(StringComparer.Ordinal);
        foreach (var name in ImageNames(newsletter))
        {
            using var stream = openImage(name);
            images[name] = Load(name, () => ImageLoader.LoadEncoded(stream));
        }

        return NewsletterComposer.Compose(newsletter, options, Theme(newsletter, options), images, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task<string> CreateAsync(Newsletter newsletter, NewsletterOptions options, Func<string, CancellationToken, Task<Stream>> openImage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openImage);
        Validate(newsletter, options);

        var images = new Dictionary<string, EncodedImage>(StringComparer.Ordinal);
        foreach (var name in ImageNames(newsletter))
        {
            var stream = await openImage(name, cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                buffer.Position = 0;
                images[name] = Load(name, () => ImageLoader.LoadEncoded(buffer));
            }
        }

        return NewsletterComposer.Compose(newsletter, options, Theme(newsletter, options), images, cancellationToken);
    }

    // The options' theme, else the newsletter's, else Starlight, with every setting filled.
    private static NewsletterTheme Theme(Newsletter newsletter, NewsletterOptions options) =>
        (options.Theme ?? newsletter.Theme ?? NewsletterTheme.Starlight).Resolve();

    // Names the picture in the message, so a newsletter of twenty photos says which one is broken.
    private static EncodedImage Load(string name, Func<EncodedImage> load)
    {
        try
        {
            return load();
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"{name}: {ex.Message}", ex);
        }
    }

    /// <summary>Every picture the newsletter names, each once, in the order they appear.</summary>
    internal static IEnumerable<string> ImageNames(Newsletter newsletter)
    {
        IEnumerable<string?> All()
        {
            yield return newsletter.Header.Logo;
            yield return newsletter.Hero?.Image;
            foreach (var block in newsletter.Blocks)
            {
                switch (block)
                {
                    case NewsletterCards cards:
                        foreach (var card in cards.Items)
                        {
                            yield return card.Image;
                        }

                        break;
                    case NewsletterImage image:
                        yield return image.Image;
                        break;
                    case NewsletterCallout { Sticker: { } sticker }:
                        yield return sticker.Image;
                        break;
                }
            }
        }

        return All().OfType<string>().Distinct(StringComparer.Ordinal);
    }

    // Everything is checked before any picture is read, so a typo fails fast.
    internal static void Validate(Newsletter newsletter, NewsletterOptions options)
    {
        ArgumentNullException.ThrowIfNull(newsletter);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _ = Theme(newsletter, options);

        if (newsletter.Header is null || string.IsNullOrWhiteSpace(newsletter.Header.Title))
        {
            throw new ArgumentException("The newsletter needs a header with a title.", nameof(newsletter));
        }

        if (newsletter.Footer is null || newsletter.Footer is { Links.Count: 0, Social.Count: 0, Lines.Count: 0 })
        {
            throw new ArgumentException("The newsletter needs a footer with at least one link, icon link or line.", nameof(newsletter));
        }

        ImageName(newsletter.Header.Logo, "The header's logo", optional: true);
        if (newsletter.Hero is not null)
        {
            ImageName(newsletter.Hero.Image, "The hero's image");
        }

        Paragraphs(newsletter.Intro, "The intro", optional: true);
        foreach (var link in newsletter.Footer.Links)
        {
            if (link is null || string.IsNullOrWhiteSpace(link.Text))
            {
                throw new ArgumentException("Each footer link needs text.", nameof(newsletter));
            }

            Url(link.Url, $"The footer link '{link.Text}'", optional: false);
        }

        foreach (var social in newsletter.Footer.Social)
        {
            if (social is null || string.IsNullOrWhiteSpace(social.Icon) || NewsletterIcons.Path(social.Icon) is null)
            {
                throw new ArgumentException($"The footer's icon '{social?.Icon}' isn't one there is. Use {string.Join(", ", NewsletterIcons.Names)}.", nameof(newsletter));
            }

            Url(social.Url, $"The footer's {social.Icon} icon", optional: false);
        }

        Paragraphs(newsletter.Footer.Lines, "The footer's lines", optional: true);

        for (var i = 0; i < newsletter.Blocks.Count; i++)
        {
            var where = $"Block {i + 1}";
            switch (newsletter.Blocks[i])
            {
                case null:
                    throw new ArgumentException($"{where} is missing.", nameof(newsletter));
                case NewsletterRibbon ribbon:
                    Line(ribbon.Text, $"{where} (ribbon)");
                    break;
                case NewsletterText text:
                    Paragraphs(text.Text, $"{where} (text)");
                    if (!Enum.IsDefined(text.Align))
                    {
                        throw new ArgumentException($"{where} (text): unknown alignment {text.Align}.", nameof(newsletter));
                    }

                    break;
                case NewsletterCards cards:
                    if (cards.Items is null || cards.Items.Count == 0)
                    {
                        throw new ArgumentException($"{where} (cards) needs at least one card.", nameof(newsletter));
                    }

                    for (var j = 0; j < cards.Items.Count; j++)
                    {
                        var card = cards.Items[j] ?? throw new ArgumentException($"{where} (cards): card {j + 1} is missing.", nameof(newsletter));
                        var cardWhere = $"{where} (cards), card {j + 1}";
                        ImageName(card.Image, cardWhere);
                        Line(card.Title, cardWhere);
                        Paragraphs(card.Text, cardWhere, optional: true);
                        Url(card.Link, cardWhere);
                    }

                    break;
                case NewsletterImage image:
                    ImageName(image.Image, $"{where} (image)");
                    Url(image.Link, $"{where} (image)");
                    break;
                case NewsletterButton button:
                    Line(button.Text, $"{where} (button)");
                    Url(button.Link, $"{where} (button)");
                    break;
                case NewsletterStats stats:
                    Line(stats.Value, $"{where} (stats)");
                    Url(stats.Link, $"{where} (stats)");
                    break;
                case NewsletterCallout callout:
                    Paragraphs(callout.Text, $"{where} (callout)");
                    if (callout.Sticker is { } sticker)
                    {
                        ImageName(sticker.Image, $"{where} (callout)'s sticker");
                        if (sticker is { Stamp: not null, Postmark: true })
                        {
                            throw new ArgumentException($"{where} (callout): a sticker is a postage stamp or a postmark, not both.", nameof(newsletter));
                        }

                        sticker.Stamp?.ToOptions().Validate();
                    }

                    break;
                case NewsletterMap map:
                    TravelMapOptions.Validate(map.Stops);
                    new TravelMapOptions { Route = map.Route, Breaks = map.Breaks, Layout = map.Layout ?? TravelMapLayout.Names }.Validate();
                    break;
                default:
                    throw new ArgumentException($"{where}: unknown block type {newsletter.Blocks[i].GetType().Name}.", nameof(newsletter));
            }
        }
    }

    private static void ImageName(string? name, string what, bool optional = false)
    {
        if (name is null && optional)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"{what} needs an image.");
        }
    }

    private static void Line(string? text, string what)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{what} needs text.");
        }

        _ = AccentText.Parse(text);
    }

    private static void Paragraphs(IReadOnlyList<string>? paragraphs, string what, bool optional = false)
    {
        if (paragraphs is null || paragraphs.Count == 0)
        {
            if (optional)
            {
                return;
            }

            throw new ArgumentException($"{what} needs text.");
        }

        foreach (var paragraph in paragraphs)
        {
            if (paragraph is null)
            {
                throw new ArgumentException($"{what}: paragraphs can't be null.");
            }

            _ = AccentText.Parse(paragraph);
        }
    }

    // Only web and mail links: a javascript: link would run when the SVG is opened in a browser.
    private static void Url(string? url, string what, bool optional = true)
    {
        if (url is null && optional)
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "mailto"))
        {
            throw new ArgumentException($"{what}: '{url}' isn't a link. Use an absolute http, https or mailto address.");
        }
    }
}
