using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SvgCreator;

/// <summary>A newsletter's typeface: one of the font stacks whose widths are measured, so text wraps right.</summary>
public enum NewsletterFont
{
    /// <summary>Helvetica or Arial, with the viewer's CJK sans-serif (PingFang, YaHei, Noto Sans CJK).</summary>
    Sans,

    /// <summary>Georgia or Times New Roman, with the viewer's CJK serif (Songti, SimSun, Noto Serif CJK).</summary>
    Serif,
}

/// <summary>How a newsletter's <see cref="NewsletterRibbon"/> headings are drawn.</summary>
public enum NewsletterRibbonStyle
{
    /// <summary>A bar in the accent colour with stepped pixel ends and a pixel chevron off each, the heading centred on it.</summary>
    Pixel,

    /// <summary>A plain bar in the accent colour across the column, the heading centred on it.</summary>
    Plain,

    /// <summary>The heading on the left in bold, underlined in the accent colour, like a magazine's section tab.</summary>
    Tab,

    /// <summary>The heading on a pill in the accent colour, just wide enough for it and centred, with a sparkle either side.</summary>
    Tag,
}

/// <summary>How the bottom of a newsletter's hero picture is cut.</summary>
public enum NewsletterHeroEdge
{
    /// <summary>A gentle wave, lined in the accent colour.</summary>
    Wave,

    /// <summary>Straight across.</summary>
    Straight,

    /// <summary>A planet's horizon: the page rises into the picture in a wide arc, with a glow in the accent colour along it.</summary>
    Arc,
}

/// <summary>The pattern on the backdrop around a newsletter's column.</summary>
public enum NewsletterBackdrop
{
    /// <summary>Diamonds of small squares in the page colour, their edges breaking up into pixels.</summary>
    Diamonds,

    /// <summary>A fine grid of dots in the page colour.</summary>
    Dots,

    /// <summary>A starfield: points of light in the band text colour, some with a four-pointed sparkle.</summary>
    Stars,

    /// <summary>A night sky: the starfield, with faint ringed planets and plump five-pointed stars among it.</summary>
    Sky,

    /// <summary>The backdrop colour alone.</summary>
    None,
}

/// <summary>How a newsletter's photos (the hero, cards and image blocks) are coloured.</summary>
public enum NewsletterPhotoTint
{
    /// <summary>In their own colours.</summary>
    None,

    /// <summary>
    /// The photo's light and shade, in the colours of a diagonal gradient from the tint's start
    /// colour (top left) to its end colour (bottom right): a <c>color</c> blend, as in image editors.
    /// </summary>
    Gradient,

    /// <summary>Redrawn in the two tint colours alone: the start colour for its darks, the end colour for its lights.</summary>
    Duotone,
}

/// <summary>The corners of a newsletter's pictures, boxes, maps and buttons.</summary>
public enum NewsletterCorners
{
    /// <summary>Rounded, and buttons pill-shaped.</summary>
    Round,

    /// <summary>Square.</summary>
    Square,
}

/// <summary>
/// How a newsletter looks: its colours, typeface and shapes. Start from a built-in theme
/// (<see cref="Starlight"/>, <see cref="Postcard"/>, <see cref="Minimal"/>, <see cref="Cosmos"/>,
/// <see cref="Greenery"/>)
/// or name one as <see cref="Base"/> and set only what should differ; anything left null comes
/// from the base. The layout is the same in every theme, so every theme fits every newsletter.
/// </summary>
/// <remarks>
/// Every setting is nullable, not defaulted, because a JSON reader that calls a type's initializer,
/// as source-generated ones do, sets those missing from the file to null or 0; null then means
/// "from the base" rather than black or the first choice. In JSON a theme can also be just a
/// built-in theme's name: <c>"theme": "postcard"</c>.
/// </remarks>
public sealed record NewsletterTheme
{
    /// <summary>The built-in theme that settings left null come from. Default (null): <c>starlight</c>.</summary>
    public string? Base { get; init; }

    /// <summary>The backdrop around the column, as a CSS colour.</summary>
    public string? BackdropColor { get; init; }

    /// <summary>The column's background, and the backdrop's diamonds and dots.</summary>
    public string? PageColor { get; init; }

    /// <summary>The header and footer bands.</summary>
    public string? BandColor { get; init; }

    /// <summary>Text on the bands (the title and the footer), and the backdrop's stars: the light colour for dark grounds.</summary>
    public string? BandTextColor { get; init; }

    /// <summary>The text on the page, a map's markers and route, and a postmark sticker's ink.</summary>
    public string? TextColor { get; init; }

    /// <summary>Ribbons and the hero's edge.</summary>
    public string? AccentColor { get; init; }

    /// <summary>
    /// <c>[[accented]]</c> text and a map's waypoints: the accent where it sits on the page as
    /// text, so it can be darker than the accent on a light page. Left null in a theme that sets
    /// <see cref="AccentColor"/>, it's that accent; otherwise it's the base's.
    /// </summary>
    public string? HighlightColor { get; init; }

    /// <summary>Text on the accent and button colours: headings on ribbon bars, and button labels.</summary>
    public string? LabelColor { get; init; }

    /// <summary>The buttons.</summary>
    public string? ButtonColor { get; init; }

    /// <summary>The stats boxes and the maps' background.</summary>
    public string? PanelColor { get; init; }

    /// <summary>The typeface.</summary>
    public NewsletterFont? Font { get; init; }

    /// <summary>How ribbon headings are drawn.</summary>
    public NewsletterRibbonStyle? Ribbon { get; init; }

    /// <summary>How the hero's bottom is cut.</summary>
    public NewsletterHeroEdge? HeroEdge { get; init; }

    /// <summary>The backdrop's pattern.</summary>
    public NewsletterBackdrop? Backdrop { get; init; }

    /// <summary>Rounded or square corners.</summary>
    public NewsletterCorners? Corners { get; init; }

    /// <summary>How photos are coloured. Every built-in theme leaves them alone (<see cref="NewsletterPhotoTint.None"/>).</summary>
    public NewsletterPhotoTint? PhotoTint { get; init; }

    /// <summary>The tint's dark colour, as hex (<c>#2b1d6b</c>): a gradient's top left, a duotone's darks.</summary>
    public string? TintStartColor { get; init; }

    /// <summary>The tint's light colour, as hex: a gradient's bottom right, a duotone's lights.</summary>
    public string? TintEndColor { get; init; }

    /// <summary>How strongly the tint is laid over the photo, from 0 (not at all) to 1. Built-in themes: 0.8.</summary>
    public double? TintStrength { get; init; }

    /// <summary>
    /// Purple and orange with pixel-art ribbons, a wavy hero and a diamond backdrop, after a game
    /// studio's update email: for games and fun trips. The default.
    /// </summary>
    public static NewsletterTheme Starlight { get; } = new()
    {
        BackdropColor = "#231a45",
        PageColor = "#5a4d7b",
        BandColor = "#0b0b0f",
        BandTextColor = "#ffffff",
        TextColor = "#ffffff",
        AccentColor = "#f0764a",
        HighlightColor = "#f0764a",
        LabelColor = "#ffffff",
        ButtonColor = "#a3292b",
        PanelColor = "#463a66",
        Font = NewsletterFont.Sans,
        Ribbon = NewsletterRibbonStyle.Pixel,
        HeroEdge = NewsletterHeroEdge.Wave,
        Backdrop = NewsletterBackdrop.Diamonds,
        Corners = NewsletterCorners.Round,
        PhotoTint = NewsletterPhotoTint.None,
        TintStartColor = "#2b1d6b",
        TintEndColor = "#f0764a",
        TintStrength = 0.8,
    };

    /// <summary>
    /// Navy on cream paper with red accents and serif type, plain ribbons and a straight hero: for
    /// trips, alongside postage stamps and postmarks.
    /// </summary>
    public static NewsletterTheme Postcard { get; } = new()
    {
        BackdropColor = "#e8dcc2",
        PageColor = "#fbf6ea",
        BandColor = "#1f2a44",
        BandTextColor = "#fbf6ea",
        TextColor = "#1f2a44",
        AccentColor = "#c0392b",
        HighlightColor = "#c0392b",
        LabelColor = "#fbf6ea",
        ButtonColor = "#1f2a44",
        PanelColor = "#f1e7cf",
        Font = NewsletterFont.Serif,
        Ribbon = NewsletterRibbonStyle.Plain,
        HeroEdge = NewsletterHeroEdge.Straight,
        Backdrop = NewsletterBackdrop.None,
        Corners = NewsletterCorners.Round,
        PhotoTint = NewsletterPhotoTint.None,
        TintStartColor = "#1f2a44",
        TintEndColor = "#e8c99b",
        TintStrength = 0.8,
    };

    /// <summary>
    /// Near-black on white with one blue accent, tab headings and square corners: for a blog or a
    /// small business.
    /// </summary>
    public static NewsletterTheme Minimal { get; } = new()
    {
        BackdropColor = "#f2f3f5",
        PageColor = "#ffffff",
        BandColor = "#111111",
        BandTextColor = "#ffffff",
        TextColor = "#1a1a1a",
        AccentColor = "#2f6fde",
        HighlightColor = "#2f6fde",
        LabelColor = "#ffffff",
        ButtonColor = "#2f6fde",
        PanelColor = "#eef2f9",
        Font = NewsletterFont.Sans,
        Ribbon = NewsletterRibbonStyle.Tab,
        HeroEdge = NewsletterHeroEdge.Straight,
        Backdrop = NewsletterBackdrop.None,
        Corners = NewsletterCorners.Square,
        PhotoTint = NewsletterPhotoTint.None,
        TintStartColor = "#1a1a1a",
        TintEndColor = "#6fa3ff",
        TintStrength = 0.8,
    };

    /// <summary>
    /// Cyan and violet on deep space navy, a starfield backdrop and a hero cut as a planet's glowing
    /// horizon: for writing about outer space and planets.
    /// </summary>
    public static NewsletterTheme Cosmos { get; } = new()
    {
        BackdropColor = "#05060f",
        PageColor = "#0d1230",
        BandColor = "#000000",
        BandTextColor = "#e8ecff",
        TextColor = "#e8ecff",
        AccentColor = "#7fdbff",
        HighlightColor = "#7fdbff",
        LabelColor = "#ffffff",
        ButtonColor = "#7b5cff",
        PanelColor = "#161d45",
        Font = NewsletterFont.Sans,
        Ribbon = NewsletterRibbonStyle.Tab,
        HeroEdge = NewsletterHeroEdge.Arc,
        Backdrop = NewsletterBackdrop.Stars,
        Corners = NewsletterCorners.Round,
        PhotoTint = NewsletterPhotoTint.None,
        TintStartColor = "#1b1150",
        TintEndColor = "#7fdbff",
        TintStrength = 0.8,
    };

    /// <summary>
    /// Brown on a cream page under a blue starry sky, with leaf-green tags, soft yellow buttons and a
    /// planet's glowing horizon, after a storybook game's sign-up page: for cosy games, gardens and
    /// growing things.
    /// </summary>
    public static NewsletterTheme Greenery { get; } = new()
    {
        BackdropColor = "#1c2c6e",
        PageColor = "#f3ecd4",
        BandColor = "#15215a",
        BandTextColor = "#f6efd8",
        TextColor = "#7a4e2b",
        AccentColor = "#86bd4c",
        HighlightColor = "#3f7a1c",
        LabelColor = "#5a3d1c",
        ButtonColor = "#f0d47a",
        PanelColor = "#e8dcb8",
        Font = NewsletterFont.Sans,
        Ribbon = NewsletterRibbonStyle.Tag,
        HeroEdge = NewsletterHeroEdge.Arc,
        Backdrop = NewsletterBackdrop.Sky,
        Corners = NewsletterCorners.Round,
        PhotoTint = NewsletterPhotoTint.None,
        TintStartColor = "#1c2c6e",
        TintEndColor = "#c8e48a",
        TintStrength = 0.8,
    };

    private static readonly (string Name, NewsletterTheme Theme)[] BuiltIns =
        [("starlight", Starlight), ("postcard", Postcard), ("minimal", Minimal), ("cosmos", Cosmos), ("greenery", Greenery)];

    /// <summary>The built-in themes' names: <c>starlight</c>, <c>postcard</c>, <c>minimal</c>, <c>cosmos</c> and <c>greenery</c>.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. BuiltIns.Select(b => b.Name)];

    /// <summary>The built-in theme of this name, in any case.</summary>
    /// <exception cref="ArgumentException">There's no built-in theme of that name.</exception>
    public static NewsletterTheme Named(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var (builtIn, theme) in BuiltIns)
        {
            if (string.Equals(builtIn, name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return theme;
            }
        }

        throw new ArgumentException($"There's no '{name}' theme. Use {string.Join(", ", Names.SkipLast(1))} or {Names[^1]}.");
    }

    /// <summary>The theme with every setting filled: those left null from <see cref="Base"/>.</summary>
    internal NewsletterTheme Resolve()
    {
        var b = Named(Base ?? "starlight");
        var theme = new NewsletterTheme
        {
            BackdropColor = BackdropColor ?? b.BackdropColor,
            PageColor = PageColor ?? b.PageColor,
            BandColor = BandColor ?? b.BandColor,
            BandTextColor = BandTextColor ?? b.BandTextColor,
            TextColor = TextColor ?? b.TextColor,
            AccentColor = AccentColor ?? b.AccentColor,
            HighlightColor = HighlightColor ?? AccentColor ?? b.HighlightColor,
            LabelColor = LabelColor ?? b.LabelColor,
            ButtonColor = ButtonColor ?? b.ButtonColor,
            PanelColor = PanelColor ?? b.PanelColor,
            Font = Font ?? b.Font,
            Ribbon = Ribbon ?? b.Ribbon,
            HeroEdge = HeroEdge ?? b.HeroEdge,
            Backdrop = Backdrop ?? b.Backdrop,
            Corners = Corners ?? b.Corners,
            PhotoTint = PhotoTint ?? b.PhotoTint,
            TintStartColor = TintStartColor ?? b.TintStartColor,
            TintEndColor = TintEndColor ?? b.TintEndColor,
            TintStrength = TintStrength ?? b.TintStrength,
        };

        CssColor.ThrowIfInvalid(theme.BackdropColor!, nameof(BackdropColor));
        CssColor.ThrowIfInvalid(theme.PageColor!, nameof(PageColor));
        CssColor.ThrowIfInvalid(theme.BandColor!, nameof(BandColor));
        CssColor.ThrowIfInvalid(theme.BandTextColor!, nameof(BandTextColor));
        CssColor.ThrowIfInvalid(theme.TextColor!, nameof(TextColor));
        CssColor.ThrowIfInvalid(theme.AccentColor!, nameof(AccentColor));
        CssColor.ThrowIfInvalid(theme.HighlightColor!, nameof(HighlightColor));
        CssColor.ThrowIfInvalid(theme.LabelColor!, nameof(LabelColor));
        CssColor.ThrowIfInvalid(theme.ButtonColor!, nameof(ButtonColor));
        CssColor.ThrowIfInvalid(theme.PanelColor!, nameof(PanelColor));
        if (!Enum.IsDefined(theme.Font!.Value) || !Enum.IsDefined(theme.Ribbon!.Value) || !Enum.IsDefined(theme.HeroEdge!.Value)
            || !Enum.IsDefined(theme.Backdrop!.Value) || !Enum.IsDefined(theme.Corners!.Value) || !Enum.IsDefined(theme.PhotoTint!.Value))
        {
            throw new ArgumentException("The theme has an unknown font, ribbon, hero edge, backdrop, corners or photo tint setting.", nameof(Font));
        }

        // Hex, because a duotone needs the colours' red, green and blue as numbers.
        _ = Rgb(theme.TintStartColor!, nameof(TintStartColor));
        _ = Rgb(theme.TintEndColor!, nameof(TintEndColor));
        if (theme.TintStrength is not (>= 0 and <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(TintStrength), theme.TintStrength, "TintStrength must be between 0 and 1.");
        }

        return theme;
    }

    /// <summary>A hex colour's red, green and blue, each from 0 to 1.</summary>
    internal static (double R, double G, double B) Rgb(string hex, string paramName)
    {
        var digits = hex.StartsWith('#') ? hex[1..] : "";
        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(d => $"{d}{d}"));
        }

        if (digits.Length != 6 || !int.TryParse(digits, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"'{hex}' isn't a hex colour. Tint colours are hex, such as #2b1d6b or #f74.", paramName);
        }

        return (((value >> 16) & 0xff) / 255.0, ((value >> 8) & 0xff) / 255.0, (value & 0xff) / 255.0);
    }
}

/// <summary>
/// Reads a newsletter's theme as either a built-in theme's name (<c>"theme": "postcard"</c>) or
/// an object of settings. Writes an object.
/// </summary>
public sealed class NewsletterThemeJsonConverter : JsonConverter<NewsletterTheme>
{
    /// <inheritdoc/>
    public override NewsletterTheme? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return reader.TokenType == JsonTokenType.String
            ? new NewsletterTheme { Base = reader.GetString() }
            : JsonSerializer.Deserialize(ref reader, (JsonTypeInfo<NewsletterTheme>)options.GetTypeInfo(typeof(NewsletterTheme)));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, NewsletterTheme value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        JsonSerializer.Serialize(writer, value, (JsonTypeInfo<NewsletterTheme>)options.GetTypeInfo(typeof(NewsletterTheme)));
    }
}
