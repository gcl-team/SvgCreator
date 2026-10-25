using System.Text.Json;
using System.Text.Json.Serialization;

namespace SvgCreator;

/// <summary>
/// A newsletter for <see cref="NewsletterGenerator"/>: a fixed frame (a header band, an optional
/// hero picture, an optional intro and a footer band) around a list of blocks in any order.
/// Pictures are named by reference (such as a path relative to the newsletter file), and the
/// generator asks its caller to open each one, so the newsletter itself holds no files.
/// </summary>
/// <remarks>
/// Text marked with double brackets, <c>[[like this]]</c>, is printed in the accent colour.
/// The types carry System.Text.Json attributes, so a JSON file maps onto them directly: blocks are
/// told apart by their <c>"type"</c>, and any text may be one string or an array of paragraphs.
/// </remarks>
public sealed record Newsletter
{
    /// <summary>The band at the top: the title, or a logo in its place.</summary>
    public required NewsletterHeader Header { get; init; }

    /// <summary>A full-width picture under the header, its bottom edge cut in a wave. Default: none.</summary>
    public NewsletterHero? Hero { get; init; }

    /// <summary>Paragraphs under the hero, centred. Default: none; null means none too.</summary>
    [JsonConverter(typeof(NewsletterTextJsonConverter))]
    public IReadOnlyList<string> Intro { get; init => field = value ?? []; } = [];

    /// <summary>The blocks between the intro and the footer, top to bottom. Default: none; null means none too.</summary>
    public IReadOnlyList<NewsletterBlock> Blocks { get; init => field = value ?? []; } = [];

    /// <summary>The band at the bottom: links and small print.</summary>
    public required NewsletterFooter Footer { get; init; }

    /// <summary>
    /// How it looks: a built-in theme, or one with settings changed. Default: none, which is
    /// <see cref="NewsletterTheme.Starlight"/>. <see cref="NewsletterOptions.Theme"/> takes its place when set.
    /// </summary>
    [JsonConverter(typeof(NewsletterThemeJsonConverter))]
    public NewsletterTheme? Theme { get; init; }
}

/// <summary>The newsletter's header band.</summary>
/// <param name="Title">
/// The newsletter's name: printed in the band (under the <see cref="Logo"/>, if there is one), and
/// always the document's <c>&lt;title&gt;</c>, which screen readers and browser tabs show.
/// </param>
public sealed record NewsletterHeader(string Title)
{
    /// <summary>A picture shown in the band, with the title under it. Default: none.</summary>
    public string? Logo { get; init; }

    /// <summary>
    /// Show the logo alone, for a logo that already says the name; the title is still the
    /// document's <c>&lt;title&gt;</c>. Ignored without a logo. Default: false.
    /// </summary>
    public bool HideTitle { get; init; }
}

/// <summary>The hero: a picture across the full width under the header, with a wave cut along its bottom.</summary>
/// <param name="Image">The picture. Its own shape sets the height, kept between wide (5:2) and nearly square (5:4).</param>
public sealed record NewsletterHero(string Image);

/// <summary>The newsletter's footer band. It needs at least one link or line.</summary>
public sealed record NewsletterFooter
{
    /// <summary>A row of links, underlined and divided by bars. Default: none.</summary>
    public IReadOnlyList<NewsletterLink> Links { get; init => field = value ?? []; } = [];

    /// <summary>Lines of small print under the links. Default: none.</summary>
    [JsonConverter(typeof(NewsletterTextJsonConverter))]
    public IReadOnlyList<string> Lines { get; init => field = value ?? []; } = [];
}

/// <summary>A link.</summary>
/// <param name="Text">What it says.</param>
/// <param name="Url">Where it goes: an absolute <c>http</c>, <c>https</c> or <c>mailto</c> address.</param>
public sealed record NewsletterLink(string Text, string Url);

/// <summary>
/// A block of the newsletter, stacked between the intro and the footer. In JSON, its
/// <c>"type"</c> says which: <c>ribbon</c>, <c>text</c>, <c>cards</c>, <c>image</c>,
/// <c>button</c>, <c>stats</c>, <c>callout</c> or <c>map</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NewsletterRibbon), "ribbon")]
[JsonDerivedType(typeof(NewsletterText), "text")]
[JsonDerivedType(typeof(NewsletterCards), "cards")]
[JsonDerivedType(typeof(NewsletterImage), "image")]
[JsonDerivedType(typeof(NewsletterButton), "button")]
[JsonDerivedType(typeof(NewsletterStats), "stats")]
[JsonDerivedType(typeof(NewsletterCallout), "callout")]
[JsonDerivedType(typeof(NewsletterMap), "map")]
public abstract record NewsletterBlock;

/// <summary>A section heading on a pixel-art ribbon in the accent colour.</summary>
/// <param name="Text">The heading, on one line.</param>
public sealed record NewsletterRibbon(string Text) : NewsletterBlock;

/// <summary>Paragraphs of body text.</summary>
public sealed record NewsletterText : NewsletterBlock
{
    /// <summary>The paragraphs.</summary>
    [JsonConverter(typeof(NewsletterTextJsonConverter))]
    public required IReadOnlyList<string> Text { get; init; }

    /// <summary>How the lines line up. Default: <see cref="NewsletterAlignment.Center"/>.</summary>
    public NewsletterAlignment Align { get; init; } = NewsletterAlignment.Center;
}

/// <summary>How a <see cref="NewsletterText"/> block's lines line up.</summary>
public enum NewsletterAlignment
{
    /// <summary>Each line centred.</summary>
    Center,

    /// <summary>Each line from the left edge.</summary>
    Left,
}

/// <summary>Rows of a picture beside a title and text, the picture swapping sides each row.</summary>
/// <param name="Items">The rows, top to bottom; the first has its picture on the left.</param>
public sealed record NewsletterCards(IReadOnlyList<NewsletterCard> Items) : NewsletterBlock;

/// <summary>One row of a <see cref="NewsletterCards"/> block.</summary>
/// <param name="Image">The picture, cropped to 16:9.</param>
/// <param name="Title">The title beside it, in bold.</param>
public sealed record NewsletterCard(string Image, string Title)
{
    /// <summary>Paragraphs under the title. Default: none.</summary>
    [JsonConverter(typeof(NewsletterTextJsonConverter))]
    public IReadOnlyList<string> Text { get; init => field = value ?? []; } = [];

    /// <summary>Where the row links to. Default: nowhere.</summary>
    public string? Link { get; init; }
}

/// <summary>A picture across the column, with an optional caption under it.</summary>
/// <param name="Image">The picture. Its own shape sets the height, kept between 10:3 and 4:5.</param>
public sealed record NewsletterImage(string Image) : NewsletterBlock
{
    /// <summary>A line under the picture, in small type. Default: none.</summary>
    public string? Caption { get; init; }

    /// <summary>Where the picture links to. Default: nowhere.</summary>
    public string? Link { get; init; }
}

/// <summary>A pill-shaped button.</summary>
/// <param name="Text">What it says, on one line.</param>
public sealed record NewsletterButton(string Text) : NewsletterBlock
{
    /// <summary>Where it links to. Default: nowhere, which leaves it as decoration.</summary>
    public string? Link { get; init; }
}

/// <summary>
/// A value set large in a box, with a small label above it: a voucher code, or a trip in numbers.
/// </summary>
/// <param name="Value">The value, on one line.</param>
public sealed record NewsletterStats(string Value) : NewsletterBlock
{
    /// <summary>The line above the box. Default: none.</summary>
    public string? Label { get; init; }

    /// <summary>Where the value links to; it's underlined when it does. Default: nowhere.</summary>
    public string? Link { get; init; }
}

/// <summary>Text beside a sticker: a picture, or one made into a postage stamp or a postmark.</summary>
public sealed record NewsletterCallout : NewsletterBlock
{
    /// <summary>The paragraphs.</summary>
    [JsonConverter(typeof(NewsletterTextJsonConverter))]
    public required IReadOnlyList<string> Text { get; init; }

    /// <summary>The sticker on the left. Default: none, and the text takes the whole width.</summary>
    public NewsletterSticker? Sticker { get; init; }
}

/// <summary>
/// A <see cref="NewsletterCallout"/>'s sticker: the picture as it is, as a postage stamp (with
/// <see cref="Stamp"/>), or traced into a postmark (with <see cref="Postmark"/>).
/// </summary>
/// <param name="Image">The picture.</param>
public sealed record NewsletterSticker(string Image)
{
    /// <summary>Make the picture a postage stamp with this text, as <see cref="PostageStampGenerator"/> does. Default: none.</summary>
    public NewsletterStamp? Stamp { get; init; }

    /// <summary>Trace the picture into a postmark, as <see cref="OutputStyle.Postmark"/> does. Default: false.</summary>
    public bool Postmark { get; init; }
}

/// <summary>
/// The text of a <see cref="NewsletterSticker"/> made into a postage stamp: the
/// <see cref="PostageStampOptions"/> a stamp takes, where anything left out keeps the stamp's default.
/// </summary>
/// <remarks>
/// It isn't <see cref="PostageStampOptions"/> itself because a JSON reader that calls a type's
/// initializer, as source-generated ones do, sets every property, those left out of the file to
/// null or 0: the ink would be null and every corner top left.
/// </remarks>
/// <param name="Country">The country name, in the largest type.</param>
public sealed record NewsletterStamp(string Country)
{
    /// <summary>The fee, such as <c>50c</c> or <c>¥1.20</c>. Default: none.</summary>
    public string? Fee { get; init; }

    /// <summary>A small caption. Default: none.</summary>
    public string? Caption { get; init; }

    /// <summary>Where the country name goes. Default: the stamp's (top left).</summary>
    public StampCorner? CountryCorner { get; init; }

    /// <summary>Where the fee goes. Default: the stamp's (top right).</summary>
    public StampCorner? FeeCorner { get; init; }

    /// <summary>Where the caption goes. Default: the stamp's (bottom left).</summary>
    public StampCorner? CaptionCorner { get; init; }

    /// <summary>Colour of the text and the picture's frame, as a CSS colour. Default: the stamp's (black).</summary>
    public string? InkColor { get; init; }

    internal PostageStampOptions ToOptions()
    {
        var defaults = new PostageStampOptions { Country = Country };
        return defaults with
        {
            Fee = Fee,
            Caption = Caption,
            CountryCorner = CountryCorner ?? defaults.CountryCorner,
            FeeCorner = FeeCorner ?? defaults.FeeCorner,
            CaptionCorner = CaptionCorner ?? defaults.CaptionCorner,
            InkColor = InkColor ?? defaults.InkColor,
        };
    }
}

/// <summary>A travel map of the stops, as <see cref="TravelMapGenerator"/> draws it, across the column.</summary>
public sealed record NewsletterMap : NewsletterBlock
{
    /// <summary>The stops, in any order; see <see cref="TravelStop"/>.</summary>
    public required IReadOnlyList<TravelStop> Stops { get; init; }

    /// <summary>How the route is drawn. Default: <see cref="TravelMapRoute.Arrows"/>.</summary>
    public TravelMapRoute Route { get; init; } = TravelMapRoute.Arrows;

    /// <summary>Whether far stops are pulled in. Default: <see cref="TravelMapBreaks.Auto"/>.</summary>
    public TravelMapBreaks Breaks { get; init; } = TravelMapBreaks.Auto;

    /// <summary>
    /// Where each stop's time and description go. Default (null): <see cref="TravelMapLayout.Names"/>,
    /// the places' names alone, since a map the column's width is too small for a day's details;
    /// the cards around it tell the story.
    /// </summary>
    /// <remarks>Nullable, not defaulting to Names, because a JSON reader sets a missing enum to 0 (<see cref="TravelMapLayout.Auto"/>).</remarks>
    public TravelMapLayout? Layout { get; init; }
}

/// <summary>
/// Reads newsletter text as either one string or an array of paragraphs, so a JSON file can say
/// <c>"text": "One paragraph."</c> without wrapping it in an array. Writes an array.
/// </summary>
public sealed class NewsletterTextJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <inheritdoc/>
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return [reader.GetString()!];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected text: a string, or an array of strings.");
        }

        var paragraphs = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            paragraphs.Add(reader.TokenType == JsonTokenType.String
                ? reader.GetString()!
                : throw new JsonException("Expected an array of strings: each paragraph is a string."));
        }

        return paragraphs;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartArray();
        foreach (var paragraph in value)
        {
            writer.WriteStringValue(paragraph);
        }

        writer.WriteEndArray();
    }
}
