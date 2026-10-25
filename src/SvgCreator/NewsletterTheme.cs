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

    /// <summary>A starfield: points of light in the text colour, some with a four-pointed sparkle.</summary>
    Stars,

    /// <summary>The backdrop colour alone.</summary>
    None,
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
/// (<see cref="Starlight"/>, <see cref="Postcard"/>, <see cref="Minimal"/>, <see cref="Cosmos"/>)
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

    /// <summary>Text on the bands: the title and the footer.</summary>
    public string? BandTextColor { get; init; }

    /// <summary>The text on the page, a map's markers and route, a postmark sticker's ink, and the stars.</summary>
    public string? TextColor { get; init; }

    /// <summary>Ribbons, the hero's edge, <c>[[accented]]</c> text and a map's waypoints.</summary>
    public string? AccentColor { get; init; }

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
        LabelColor = "#ffffff",
        ButtonColor = "#a3292b",
        PanelColor = "#463a66",
        Font = NewsletterFont.Sans,
        Ribbon = NewsletterRibbonStyle.Pixel,
        HeroEdge = NewsletterHeroEdge.Wave,
        Backdrop = NewsletterBackdrop.Diamonds,
        Corners = NewsletterCorners.Round,
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
        LabelColor = "#fbf6ea",
        ButtonColor = "#1f2a44",
        PanelColor = "#f1e7cf",
        Font = NewsletterFont.Serif,
        Ribbon = NewsletterRibbonStyle.Plain,
        HeroEdge = NewsletterHeroEdge.Straight,
        Backdrop = NewsletterBackdrop.None,
        Corners = NewsletterCorners.Round,
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
        LabelColor = "#ffffff",
        ButtonColor = "#2f6fde",
        PanelColor = "#eef2f9",
        Font = NewsletterFont.Sans,
        Ribbon = NewsletterRibbonStyle.Tab,
        HeroEdge = NewsletterHeroEdge.Straight,
        Backdrop = NewsletterBackdrop.None,
        Corners = NewsletterCorners.Square,
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
        LabelColor = "#ffffff",
        ButtonColor = "#7b5cff",
        PanelColor = "#161d45",
        Font = NewsletterFont.Sans,
        Ribbon = NewsletterRibbonStyle.Tab,
        HeroEdge = NewsletterHeroEdge.Arc,
        Backdrop = NewsletterBackdrop.Stars,
        Corners = NewsletterCorners.Round,
    };

    private static readonly (string Name, NewsletterTheme Theme)[] BuiltIns =
        [("starlight", Starlight), ("postcard", Postcard), ("minimal", Minimal), ("cosmos", Cosmos)];

    /// <summary>The built-in themes' names: <c>starlight</c>, <c>postcard</c>, <c>minimal</c> and <c>cosmos</c>.</summary>
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
            LabelColor = LabelColor ?? b.LabelColor,
            ButtonColor = ButtonColor ?? b.ButtonColor,
            PanelColor = PanelColor ?? b.PanelColor,
            Font = Font ?? b.Font,
            Ribbon = Ribbon ?? b.Ribbon,
            HeroEdge = HeroEdge ?? b.HeroEdge,
            Backdrop = Backdrop ?? b.Backdrop,
            Corners = Corners ?? b.Corners,
        };

        CssColor.ThrowIfInvalid(theme.BackdropColor!, nameof(BackdropColor));
        CssColor.ThrowIfInvalid(theme.PageColor!, nameof(PageColor));
        CssColor.ThrowIfInvalid(theme.BandColor!, nameof(BandColor));
        CssColor.ThrowIfInvalid(theme.BandTextColor!, nameof(BandTextColor));
        CssColor.ThrowIfInvalid(theme.TextColor!, nameof(TextColor));
        CssColor.ThrowIfInvalid(theme.AccentColor!, nameof(AccentColor));
        CssColor.ThrowIfInvalid(theme.LabelColor!, nameof(LabelColor));
        CssColor.ThrowIfInvalid(theme.ButtonColor!, nameof(ButtonColor));
        CssColor.ThrowIfInvalid(theme.PanelColor!, nameof(PanelColor));
        if (!Enum.IsDefined(theme.Font!.Value) || !Enum.IsDefined(theme.Ribbon!.Value) || !Enum.IsDefined(theme.HeroEdge!.Value)
            || !Enum.IsDefined(theme.Backdrop!.Value) || !Enum.IsDefined(theme.Corners!.Value))
        {
            throw new ArgumentException("The theme has an unknown font, ribbon, hero edge, backdrop or corners setting.", nameof(Font));
        }

        return theme;
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
