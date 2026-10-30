using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using SkiaSharp;
using SvgCreator.Composition;
using SvgCreator.Imaging;

namespace SvgCreator.Tests;

public class NewsletterTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    // 60 × 40, the postage stamp tests' picture: fixed bytes, so the golden file is the same everywhere.
    private static byte[] Picture => Convert.FromBase64String(PostageStampTests.PictureBase64);

    private static readonly Newsletter Minimal = new() { Header = new("Hello"), Footer = new() { Lines = ["Bye"] } };

    private static readonly TravelStop[] Stops =
    [
        new(1, "City Hall", 1.2931, 103.8520) { Time = "Day 1 · 9 am" },
        new(2, "Singapore Zoo", 1.4043, 103.7930),
    ];

    private static string Create(Newsletter newsletter, NewsletterOptions? options = null, IReadOnlyDictionary<string, byte[]>? images = null, List<string>? opened = null) =>
        new NewsletterGenerator().Create(newsletter, options ?? new NewsletterOptions(), name =>
        {
            opened?.Add(name);
            return new MemoryStream(images?.GetValueOrDefault(name) ?? Picture);
        });

    private static XElement Root(Newsletter newsletter, NewsletterOptions? options = null, IReadOnlyDictionary<string, byte[]>? images = null) =>
        XDocument.Parse(Create(newsletter, options, images)).Root!;

    private static double Number(XElement e, string attribute) => double.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);

    private static IEnumerable<XElement> ByClass(XElement root, string className) =>
        root.Descendants().Where(e => (string?)e.Attribute("class") == className);

    private static XElement Column(XElement root) => ByClass(root, "newsletter-column").Single();

    // A circular seal: a black ring on white, set pixel by pixel (see MailPieceTests).
    private static byte[] Seal()
    {
        using var bitmap = new SKBitmap(120, 120);
        for (var y = 0; y < 120; y++)
        {
            for (var x = 0; x < 120; x++)
            {
                var d = Math.Sqrt(Math.Pow(x + 0.5 - 60, 2) + Math.Pow(y + 0.5 - 60, 2));
                bitmap.SetPixel(x, y, d is >= 47 and <= 53 ? SKColors.Black : SKColors.White);
            }
        }

        return bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    // A photo-sized picture of noise, which PNG can't compress, so a smaller JPEG is much smaller.
    private static byte[] Noise(int width, int height, byte alpha = 255)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var random = new Random(1);
        var pixels = new byte[width * height * 4];
        random.NextBytes(pixels);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = alpha;
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        return bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static Newsletter Everything() => new()
    {
        Header = new("Shanghai") { Logo = "logo.png" },
        Hero = new("hero.png"),
        Intro = ["Hi! Three days and far too many [[xiaolongbao]].", "Here's the best of it."],
        Blocks =
        [
            new NewsletterRibbon("Day 1"),
            new NewsletterText { Text = ["Left-aligned notes."], Align = NewsletterAlignment.Left },
            new NewsletterCards(
            [
                new NewsletterCard("hero.png", "The Bund") { Text = ["Lights at 7 pm."], Link = "https://example.com/bund" },
                new NewsletterCard("card.png", "Yu Garden"),
            ]),
            new NewsletterImage("card.png") { Caption = "Worth the queue" },
            new NewsletterButton("Full itinerary") { Link = "https://example.com" },
            new NewsletterStats("CA395VK8") { Label = "Reader code", Link = "https://example.com/redeem" },
            new NewsletterCallout { Text = ["Postcards sent."], Sticker = new("card.png") { Stamp = new("CHINA") { Fee = "¥1.20" } } },
            new NewsletterCallout { Text = ["Sealed."], Sticker = new("seal.png") { Postmark = true } },
            new NewsletterCallout { Text = ["Plain sticker."], Sticker = new("logo.png") },
            new NewsletterMap { Stops = Stops },
        ],
        Footer = new()
        {
            Links = [new("Home", "https://example.com"), new("Contact", "mailto:hello@example.com")],
            Lines = ["Copyright"],
        },
    };

    private static Dictionary<string, byte[]> EverythingImages() => new() { ["seal.png"] = Seal() };

    [Fact]
    public void Golden() => SvgGoldenTests.AssertGolden("newsletter.svg", Create(Everything(), images: EverythingImages()));

    [Theory]
    [InlineData("postcard")]
    [InlineData("minimal")]
    [InlineData("cosmos")]
    [InlineData("greenery")]
    public void Golden_Themes(string theme) =>
        SvgGoldenTests.AssertGolden($"newsletter-{theme}.svg", Create(Everything() with { Theme = new NewsletterTheme { Base = theme } }, images: EverythingImages()));

    [Fact]
    public async Task CreateAsync_MakesTheSameDocument()
    {
        var images = EverythingImages();
        var svg = await new NewsletterGenerator().CreateAsync(Everything(), new NewsletterOptions(), (name, _) =>
            Task.FromResult<Stream>(new MemoryStream(images.GetValueOrDefault(name) ?? Picture)), TestContext.Current.CancellationToken);

        Assert.Equal(Create(Everything(), images: images), svg);
    }

    [Fact]
    public void Create_StacksTheFrameAroundTheBlocksInOrder()
    {
        var newsletter = Minimal with
        {
            Hero = new("hero.png"),
            Intro = ["Hi"],
            Blocks = [new NewsletterRibbon("Day 1"), new NewsletterText { Text = ["Text"] }, new NewsletterButton("Go")],
        };

        var classes = Column(Root(newsletter)).Elements().Select(e => (string?)e.Attribute("class"));

        Assert.Equal(
            ["newsletter-page", "newsletter-header", "newsletter-hero", "newsletter-intro", "newsletter-ribbon", "newsletter-text", "newsletter-button", "newsletter-footer"],
            classes);
    }

    [Fact]
    public void Create_CanvasIsTheColumnPlusTheMargin_AndAsTallAsTheContent()
    {
        var bare = Root(Minimal, new NewsletterOptions { Margin = 25 });
        var longer = Root(Minimal with { Blocks = [new NewsletterText { Text = ["More"] }] }, new NewsletterOptions { Margin = 25 });

        Assert.Equal(640 + 50, Number(bare, "width"));
        Assert.True(Number(longer, "height") > Number(bare, "height"));
        Assert.Equal(Number(bare, "height") - 50, Number(Column(bare).Elements().First(), "height"));
    }

    [Fact]
    public void Create_ScalesEverythingWithTheWidth()
    {
        var newsletter = Everything();
        var normal = Root(newsletter, new NewsletterOptions { Margin = 0 }, EverythingImages());
        var doubled = Root(newsletter, new NewsletterOptions { Width = 1280, Margin = 0 }, EverythingImages());

        Assert.Equal(2 * Number(normal, "height"), Number(doubled, "height"), 2.0);
        Assert.Equal(
            2 * Number(ByClass(normal, "newsletter-intro").Single(), "font-size"),
            Number(ByClass(doubled, "newsletter-intro").Single(), "font-size"),
            6);
    }

    [Fact]
    public void Create_NoMargin_LeavesOutTheBackdrop()
    {
        var root = Root(Minimal, new NewsletterOptions { Margin = 0 });

        Assert.Empty(ByClass(root, "newsletter-backdrop"));
        Assert.Null(Column(root).Attribute("transform"));
    }

    [Fact]
    public void Header_TitleIsTheDocumentsTitle_AndPrintedInTheBand()
    {
        var root = Root(Minimal with { Header = new("Shanghai [[trip]]") });

        Assert.Equal("Shanghai trip", root.Element(Svg + "title")!.Value);
        Assert.Equal("Shanghai trip", ByClass(root, "newsletter-title").Single().Value);
    }

    [Fact]
    public void Header_WithALogo_StacksTheTitleUnderIt_InATallerBand()
    {
        var plain = Root(Minimal with { Header = new("Shanghai") });
        var root = Root(Minimal with { Header = new("Shanghai") { Logo = "logo.png" } });
        var header = ByClass(root, "newsletter-header").Single();

        var logo = ByClass(header, "newsletter-picture").Single();
        var logoTop = double.Parse(((string)logo.Element(Svg + "use")!.Attribute("transform")!).Split('(', ' ', ')')[2], CultureInfo.InvariantCulture);
        var titleBaseline = Number(ByClass(header, "newsletter-title").Single().Element(Svg + "tspan")!, "y");
        Assert.True(titleBaseline > logoTop + 44, $"title at {titleBaseline}, logo from {logoTop}");
        Assert.True(Number(header.Element(Svg + "rect")!, "height") > Number(ByClass(plain, "newsletter-header").Single().Element(Svg + "rect")!, "height"));
    }

    [Fact]
    public void Header_HideTitle_LeavesTheLogoAlone_ButKeepsTheDocumentsTitle()
    {
        var root = Root(Minimal with { Header = new("Shanghai") { Logo = "logo.png", HideTitle = true } });

        Assert.Empty(ByClass(root, "newsletter-title"));
        Assert.Equal("Shanghai", root.Element(Svg + "title")!.Value);
        Assert.Equal(84, Number(ByClass(root, "newsletter-header").Single().Element(Svg + "rect")!, "height"));
    }

    [Fact]
    public void Text_MarkedSpansAreInTheAccentColour()
    {
        var root = Root(Minimal with { Intro = ["Far too many [[xiaolongbao]]!"] }, new NewsletterOptions { Theme = new NewsletterTheme { AccentColor = "#ff8800" } });
        var accent = ByClass(root, "newsletter-accent").Single();

        Assert.Equal("xiaolongbao", accent.Value);
        Assert.Equal("#ff8800", (string?)accent.Attribute("fill"));
        Assert.Equal("Far too many xiaolongbao!", ByClass(root, "newsletter-intro").Single().Value);
    }

    [Fact]
    public void Text_AHighlightColour_TakesTheAccentsPlaceOnMarkedSpans_ButNotOnRibbons()
    {
        var theme = new NewsletterTheme { Ribbon = NewsletterRibbonStyle.Plain, AccentColor = "#ff8800", HighlightColor = "#225500" };
        var root = Root(Minimal with { Intro = ["Far too many [[xiaolongbao]]!"], Blocks = [new NewsletterRibbon("Day 1")], Theme = theme });

        Assert.Equal("#225500", (string?)ByClass(root, "newsletter-accent").Single().Attribute("fill"));
        Assert.Equal("#ff8800", (string?)ByClass(root, "newsletter-ribbon").Single().Element(Svg + "rect")!.Attribute("fill"));
    }

    [Fact]
    public void Theme_HighlightLeftOut_FollowsTheThemesOwnAccent_ElseTheBases()
    {
        Assert.Equal("#e4572e", new NewsletterTheme { Base = "greenery", AccentColor = "#e4572e" }.Resolve().HighlightColor);
        Assert.Equal(NewsletterTheme.Greenery.HighlightColor, new NewsletterTheme { Base = "greenery", PageColor = "#ffffff" }.Resolve().HighlightColor);
    }

    [Theory]
    [InlineData("Unclosed [[accent")]
    [InlineData("Stray ]] close")]
    [InlineData("[[Nested [[twice]]]]")]
    public void Text_UnbalancedMarkers_AreRejected(string text)
    {
        var ex = Assert.Throws<ArgumentException>(() => Create(Minimal with { Intro = [text] }));

        Assert.Contains("Unbalanced accent markers", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_CentredParagraphs_DontLeaveAWordAloneOnTheLastLine()
    {
        var text = "3 days, 12 stops and far too many xiaolongbao. Here's the best of it~";
        var lines = ByClass(Root(Minimal with { Intro = [text] }), "newsletter-intro").Single().Elements(Svg + "tspan").Select(t => t.Value).ToList();

        Assert.True(lines.Count > 1);
        Assert.Contains(" ", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Links_WrapWhatTheyLinkFrom()
    {
        var root = Root(Minimal with { Blocks = [new NewsletterButton("Go") { Link = "https://example.com/go" }, new NewsletterButton("Decoration")] });

        var links = root.Descendants(Svg + "a").ToList();
        Assert.Equal("https://example.com/go", (string?)links.Single().Attribute("href"));
        Assert.Equal("newsletter-button", (string?)links.Single().Elements().Single().Attribute("class"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("example.com")]
    [InlineData("/relative/path")]
    [InlineData("file:///etc/passwd")]
    public void Links_OnlyWebAndMailAddresses(string url)
    {
        var ex = Assert.Throws<ArgumentException>(() => Create(Minimal with { Blocks = [new NewsletterButton("Go") { Link = url }] }));

        Assert.Contains("isn't a link", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NeedsATitleAndAFooter()
    {
        Assert.Throws<ArgumentException>(() => Create(Minimal with { Header = new(" ") }));
        Assert.Throws<ArgumentException>(() => Create(Minimal with { Footer = new() }));
    }

    [Fact]
    public void Validate_FailsBeforeAnyPictureIsRead()
    {
        var opened = new List<string>();
        var newsletter = Minimal with { Hero = new("hero.png"), Blocks = [new NewsletterCards([])] };

        Assert.Throws<ArgumentException>(() => Create(newsletter, opened: opened));
        Assert.Empty(opened);
    }

    [Fact]
    public void Validate_AStickerIsAStampOrAPostmark_NotBoth()
    {
        var sticker = new NewsletterSticker("a.png") { Stamp = new("CHINA"), Postmark = true };

        Assert.Throws<ArgumentException>(() => Create(Minimal with { Blocks = [new NewsletterCallout { Text = ["Hi"], Sticker = sticker }] }));
    }

    [Fact]
    public void Images_EachIsReadAndEmbeddedOnce_HoweverOftenItsDrawn()
    {
        var opened = new List<string>();
        var newsletter = Minimal with { Hero = new("bund.png"), Blocks = [new NewsletterCards([new NewsletterCard("bund.png", "The Bund")])] };

        var root = XDocument.Parse(Create(newsletter, opened: opened)).Root!;

        Assert.Equal(["bund.png"], opened);
        Assert.Single(root.Descendants(Svg + "image"));
        Assert.Equal(2, root.Descendants(Svg + "use").Count(u => ((string?)u.Attribute("href"))!.StartsWith("#newsletter-image-", StringComparison.Ordinal)));
    }

    [Fact]
    public void Images_LargerThanNeeded_AreScaledDownToJpeg_AtTheirDisplayedSize()
    {
        var photo = Noise(1200, 800);
        var root = Root(Minimal with { Blocks = [new NewsletterCards([new NewsletterCard("photo.png", "Photo")])] }, images: new Dictionary<string, byte[]> { ["photo.png"] = photo });
        var image = root.Descendants(Svg + "image").Single();
        var href = (string)image.Attribute("href")!;

        Assert.StartsWith("data:image/jpeg;base64,", href, StringComparison.Ordinal);
        Assert.True(href.Length < photo.Length / 10, $"{href.Length} vs {photo.Length}");
        Assert.Equal((1200.0, 800.0), (Number(image, "width"), Number(image, "height")));
    }

    [Fact]
    public void Images_Transparent_StayPng()
    {
        var root = Root(Minimal with { Header = new("Logo") { Logo = "logo.png" } }, images: new Dictionary<string, byte[]> { ["logo.png"] = Noise(1200, 300, alpha: 128) });

        Assert.StartsWith("data:image/png;base64,", (string)root.Descendants(Svg + "image").Single().Attribute("href")!, StringComparison.Ordinal);
    }

    [Fact]
    public void Images_DensityZero_EmbedsTheOriginal()
    {
        var photo = Noise(400, 300);
        var root = Root(
            Minimal with { Blocks = [new NewsletterCards([new NewsletterCard("photo.png", "Photo")])] },
            new NewsletterOptions { ImageDensity = 0 },
            new Dictionary<string, byte[]> { ["photo.png"] = photo });

        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(photo)}", (string)root.Descendants(Svg + "image").Single().Attribute("href")!);
    }

    [Fact]
    public void Images_ABrokenOne_IsNamedInTheError()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Create(Minimal with { Hero = new("broken.jpg") }, images: new Dictionary<string, byte[]> { ["broken.jpg"] = [1, 2, 3] }));

        Assert.StartsWith("broken.jpg: ", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cards_ThePictureSwapsSidesEachRow()
    {
        var cards = new NewsletterCards([new NewsletterCard("a.png", "First"), new NewsletterCard("a.png", "Second")]);
        var rows = ByClass(Root(Minimal with { Blocks = [cards] }), "newsletter-card").ToList();

        double PictureX(XElement card) => double.Parse(((string)card.Descendants(Svg + "use").Single().Attribute("transform")!).Split('(', ' ')[1], CultureInfo.InvariantCulture);
        double TextX(XElement card) => Number(ByClass(card, "newsletter-card-title").Single().Element(Svg + "tspan")!, "x");

        Assert.True(PictureX(rows[0]) < TextX(rows[0]));
        Assert.True(PictureX(rows[1]) > TextX(rows[1]));
    }

    [Fact]
    public void Callout_StampAndPostmarkStickers_AreTheirGeneratorsDrawings()
    {
        var root = Root(Everything(), images: EverythingImages());

        Assert.Single(root.Descendants(Svg + "svg"), e => (string?)e.Attribute("class") == "newsletter-sticker-stamp");
        var postmark = root.Descendants(Svg + "svg").Single(e => (string?)e.Attribute("class") == "newsletter-sticker-postmark");
        Assert.Contains(postmark.Descendants(), e => (string?)e.Attribute("stroke") == "#ffffff" || (string?)e.Attribute("fill") == "#ffffff");
    }

    [Fact]
    public void Map_IsTheTravelMapNestedInThePanelColour()
    {
        var root = Root(Minimal with { Blocks = [new NewsletterMap { Stops = Stops }] }, new NewsletterOptions { Theme = new NewsletterTheme { PanelColor = "#123456" } });
        var map = root.Descendants(Svg + "svg").Single(e => (string?)e.Attribute("class") == "newsletter-map-drawing");

        Assert.Equal(2, ByClass(map, "travel-marker").Count());
        Assert.Equal("#123456", (string?)ByClass(map, "travel-background").Single().Attribute("fill"));
    }

    [Fact]
    public void Map_ShowsOnlyThePlacesNames_UnlessAskedOtherwise()
    {
        var map = new NewsletterMap { Stops = Stops };
        var names = Root(Minimal with { Blocks = [map] });
        var labels = Root(Minimal with { Blocks = [map with { Layout = TravelMapLayout.Labels }] });

        Assert.Empty(ByClass(names, "travel-time"));
        Assert.NotEmpty(ByClass(labels, "travel-time"));
    }

    [Fact]
    public void Options_AreValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(Minimal, new NewsletterOptions { Width = 100 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(Minimal, new NewsletterOptions { Margin = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(Minimal, new NewsletterOptions { ImageDensity = double.NaN }));
        Assert.Throws<ArgumentException>(() => Create(Minimal, new NewsletterOptions { Theme = new NewsletterTheme { AccentColor = "orangeish" } }));
    }

    [Fact]
    public void Theme_SettingsLeftOutComeFromTheBase()
    {
        var theme = new NewsletterTheme { Base = "postcard", AccentColor = "#e4572e" }.Resolve();

        Assert.Equal("#e4572e", theme.AccentColor);
        Assert.Equal(NewsletterTheme.Postcard.PageColor, theme.PageColor);
        Assert.Equal(NewsletterFont.Serif, theme.Font);
        Assert.Equal(NewsletterTheme.Starlight, new NewsletterTheme().Resolve());
    }

    [Fact]
    public void Theme_UnknownName_ListsTheBuiltIns()
    {
        var ex = Assert.Throws<ArgumentException>(() => Create(Minimal with { Theme = new NewsletterTheme { Base = "galaxy" } }));

        Assert.Contains("starlight, postcard, minimal, cosmos or greenery", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_TheOptionsThemeTakesTheNewslettersPlace()
    {
        var root = Root(Minimal with { Theme = NewsletterTheme.Postcard }, new NewsletterOptions { Theme = NewsletterTheme.Cosmos });

        Assert.Equal(NewsletterTheme.Cosmos.PageColor, (string?)ByClass(root, "newsletter-page").Single().Attribute("fill"));
    }

    [Fact]
    public void Theme_Serif_SetsTheFontAndWrapsWithItsWidths()
    {
        var root = Root(Minimal with { Theme = NewsletterTheme.Postcard });

        Assert.StartsWith("Georgia", (string?)Column(root).Attribute("font-family"), StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_SquareCorners_RoundNothing()
    {
        var newsletter = Everything() with { Theme = NewsletterTheme.Minimal };
        var root = Root(newsletter, images: EverythingImages());

        Assert.DoesNotContain(Column(root).Descendants(), e => e.Attribute("rx") is not null && e.Ancestors(Svg + "svg").Count() == 1);
    }

    [Fact]
    public void Theme_BandsAndLabels_HaveTheirOwnTextColours()
    {
        var root = Root(Minimal with { Blocks = [new NewsletterButton("Go")] }, new NewsletterOptions { Theme = NewsletterTheme.Postcard });

        Assert.Equal(NewsletterTheme.Postcard.BandTextColor, (string?)ByClass(root, "newsletter-header").Single().Attribute("fill"));
        Assert.Equal(NewsletterTheme.Postcard.LabelColor, (string?)ByClass(root, "newsletter-button-text").Single().Attribute("fill"));
    }

    [Theory]
    [InlineData("starlight", true)]
    [InlineData("cosmos", true)]
    [InlineData("greenery", true)]
    [InlineData("minimal", false)]
    public void Theme_BackdropPattern_OnlyWhenTheThemeHasOne(string theme, bool pattern)
    {
        var root = Root(Minimal with { Theme = NewsletterTheme.Named(theme) });

        Assert.Equal(pattern, root.Descendants(Svg + "pattern").Any());
    }

    [Fact]
    public void Theme_TagRibbon_IsAPillAsWideAsItsHeading_CentredBetweenTwoSparkles()
    {
        var theme = new NewsletterTheme { Ribbon = NewsletterRibbonStyle.Tag };
        var shortPill = ByClass(Root(Minimal with { Theme = theme, Blocks = [new NewsletterRibbon("Go")] }), "newsletter-ribbon").Single();
        var longPill = ByClass(Root(Minimal with { Theme = theme, Blocks = [new NewsletterRibbon("Ready to join us?")] }), "newsletter-ribbon").Single();

        var pill = shortPill.Descendants(Svg + "rect").Single();
        Assert.Equal(320, Number(pill, "x") + (Number(pill, "width") / 2), 6);
        Assert.Equal(Number(pill, "height") / 2, Number(pill, "rx"), 6);
        Assert.True(Number(pill, "width") < Number(longPill.Descendants(Svg + "rect").Single(), "width"));
        Assert.Equal(2, shortPill.Descendants(Svg + "path").Count());
    }

    [Theory]
    [InlineData("gradient")]
    [InlineData("duotone")]
    public void Golden_PhotoTints(string tint) =>
        SvgGoldenTests.AssertGolden($"newsletter-tint-{tint}.svg", Create(Everything() with { Theme = new NewsletterTheme { Base = "cosmos", PhotoTint = Enum.Parse<NewsletterPhotoTint>(tint, ignoreCase: true) } }, images: EverythingImages()));

    [Fact]
    public void PhotoTint_Gradient_BlendsAGradientOverEachPhoto_ButNotLogosOrStickers()
    {
        var theme = new NewsletterTheme { PhotoTint = NewsletterPhotoTint.Gradient };
        var root = Root(Everything() with { Theme = theme }, images: EverythingImages());

        // The hero, two cards and the image block; not the logo or the plain sticker.
        var tints = ByClass(root, "newsletter-tint").ToList();
        Assert.Equal(4, tints.Count);
        Assert.All(tints, t => Assert.Equal("mix-blend-mode:color", (string?)t.Attribute("style")));
        Assert.All(tints, t => Assert.Equal("0.8", (string?)t.Attribute("opacity")));
        var stops = root.Descendants(Svg + "linearGradient").Single(g => (string?)g.Attribute("id") == "newsletter-tint-gradient").Elements(Svg + "stop");
        Assert.Equal([NewsletterTheme.Starlight.TintStartColor, NewsletterTheme.Starlight.TintEndColor], stops.Select(s => (string?)s.Attribute("stop-color")));
    }

    [Fact]
    public void PhotoTint_Duotone_MapsGreysFromTheStartToTheEndColour()
    {
        var theme = new NewsletterTheme { PhotoTint = NewsletterPhotoTint.Duotone, TintStartColor = "#000", TintEndColor = "#ff8000", TintStrength = 1 };
        var root = Root(Minimal with { Hero = new("hero.png"), Theme = theme });

        var filter = root.Descendants(Svg + "filter").Single(f => (string?)f.Attribute("id") == "newsletter-tint-duotone");
        Assert.Equal(["0 1", "0 0.502", "0 0"], filter.Descendants().Where(e => e.Name.LocalName.StartsWith("feFunc", StringComparison.Ordinal)).Select(f => (string?)f.Attribute("tableValues")));
        Assert.Single(root.Descendants(Svg + "use"), u => (string?)u.Attribute("filter") == "url(#newsletter-tint-duotone)");
    }

    [Fact]
    public void PhotoTint_APhotoCanOptOut()
    {
        var theme = new NewsletterTheme { PhotoTint = NewsletterPhotoTint.Gradient };
        var root = Root(Minimal with { Hero = new("hero.png") { Tint = false }, Blocks = [new NewsletterImage("a.png")], Theme = theme });

        Assert.Single(ByClass(root, "newsletter-tint"));
    }

    [Fact]
    public void PhotoTint_None_WritesNothing()
    {
        var root = Root(Everything(), images: EverythingImages());

        Assert.Empty(ByClass(root, "newsletter-tint"));
        Assert.DoesNotContain(root.Descendants(), e => ((string?)e.Attribute("id"))?.StartsWith("newsletter-tint", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("navy", null)]
    [InlineData("#12345", null)]
    [InlineData("#123", 1.5)]
    public void PhotoTint_ColoursAreHex_AndStrengthIsAFraction(string color, double? strength)
    {
        var theme = new NewsletterTheme { PhotoTint = NewsletterPhotoTint.Gradient, TintStartColor = color, TintStrength = strength };

        Assert.ThrowsAny<ArgumentException>(() => Create(Minimal with { Theme = theme }));
    }

    [Fact]
    public void Footer_IconLinks_AreCirclesWithTheirIconCutOut()
    {
        var footer = new NewsletterFooter { Social = [new("GitHub", "https://github.com/gcl-team/SvgCreator")], Lines = ["Bye"] };
        var root = Root(Minimal with { Footer = footer, Theme = NewsletterTheme.Postcard });

        var link = ByClass(root, "newsletter-footer-social").Single().Element(Svg + "a")!;
        Assert.Equal("https://github.com/gcl-team/SvgCreator", (string?)link.Attribute("href"));
        var icon = link.Element(Svg + "g")!;
        Assert.Equal("newsletter-icon newsletter-icon-github", (string?)icon.Attribute("class"));
        Assert.NotNull(icon.Element(Svg + "circle"));
        Assert.Equal(NewsletterTheme.Postcard.BandColor, (string?)icon.Element(Svg + "path")!.Attribute("fill"));
    }

    [Fact]
    public void Footer_IconsAlone_AreEnough()
    {
        var footer = new NewsletterFooter { Social = [new("github", "https://github.com/gcl-team")] };

        Assert.Single(ByClass(Root(Minimal with { Footer = footer }), "newsletter-icon newsletter-icon-github"));
    }

    [Theory]
    [InlineData("bilibili", "https://example.com")]
    [InlineData("github", "javascript:alert(1)")]
    public void Footer_IconLinks_AreCheckedIconAndUrl(string icon, string url) =>
        Assert.Throws<ArgumentException>(() => Create(Minimal with { Footer = new() { Social = [new(icon, url)] } }));

    [Fact]
    public void Template_LeavesThePhotosOut_AndCutsAHoleForEach()
    {
        var root = Root(Everything(), new NewsletterOptions { Template = true }, EverythingImages());

        // The hero, two cards and the image block are slots; the logo and the plain sticker stay.
        var slots = ByClass(root, "newsletter-slot").ToList();
        Assert.Equal(["hero.png", "hero.png", "card.png", "card.png"], slots.Select(s => (string?)s.Attribute("data-image")));
        Assert.Equal(2, ByClass(root, "newsletter-picture").Count());
        Assert.DoesNotContain(root.Descendants(Svg + "use"), u => (string?)u.Parent?.Parent?.Attribute("class") == "newsletter-hero");

        // Everything drawn is under the mask, which is white with a black hole per slot.
        var masked = ByClass(root, "newsletter-template").Single();
        Assert.Equal("url(#newsletter-template-holes)", (string?)masked.Attribute("mask"));
        Assert.Contains(masked.Elements(), e => (string?)e.Attribute("class") == "newsletter-column");
        var holes = root.Descendants(Svg + "mask").Single().Element(Svg + "g")!;
        Assert.Equal("#000000", (string?)holes.Attribute("fill"));
        Assert.Equal("translate(40 40)", (string?)holes.Attribute("transform"));
        Assert.Equal(4, holes.Elements(Svg + "rect").Count());
    }

    [Fact]
    public void Template_TheHerosHoleHasItsEdge_AndRoundedPhotosRoundedHoles()
    {
        var newsletter = Minimal with { Hero = new("hero.png"), Blocks = [new NewsletterImage("a.png")] };
        var holes = Root(newsletter, new NewsletterOptions { Template = true }).Descendants(Svg + "mask").Single().Descendants(Svg + "rect").Skip(1).ToList();

        Assert.Equal("url(#newsletter-hero-edge-clip)", (string?)holes[0].Attribute("clip-path"));
        Assert.Equal("8", (string?)holes[1].Attribute("rx"));
    }

    [Fact]
    public void Template_EmbedsNoPhotos()
    {
        var photo = Noise(400, 300);
        var newsletter = Minimal with { Hero = new("photo.png"), Blocks = [new NewsletterCards([new NewsletterCard("photo.png", "A")])] };

        var root = Root(newsletter, new NewsletterOptions { Template = true }, new Dictionary<string, byte[]> { ["photo.png"] = photo });

        Assert.Empty(root.Descendants(Svg + "image"));
    }

    [Fact]
    public void Template_Off_HasNoMaskOrSlots()
    {
        var root = Root(Everything(), images: EverythingImages());

        Assert.Empty(root.Descendants(Svg + "mask"));
        Assert.Empty(ByClass(root, "newsletter-slot"));
    }

    [Fact]
    public void AccentText_Wrap_FindsTheAccentedRunsOnEachLine()
    {
        var lines = AccentText.Wrap("Try   the [[小笼包 at 南翔]] today", 10, 60);
        var flat = lines.SelectMany(l => l).Where(r => r.Accent).Select(r => r.Text);

        Assert.True(lines.Count > 1);
        Assert.Equal("小笼包 at 南翔", string.Join(' ', flat).Replace("  ", " ", StringComparison.Ordinal));
        Assert.Equal("Try the 小笼包 at 南翔 today", string.Join(' ', lines.Select(l => string.Concat(l.Select(r => r.Text)))));
    }

    [Fact]
    public void Downscale_KeepsGifs_WhichMayBeAnimated()
    {
        var image = new EncodedImage([1, 2, 3], "image/gif", 400, 400);

        Assert.Same(image, ImageLoader.Downscale(image, 100, 100));
    }

    // The CLI reads the file with these settings, source-generated (see NewsletterJson).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Json_BlocksByType_TextAsAStringOrParagraphs()
    {
        var json = """
            {
              "header": { "title": "Blog" },
              "intro": "One paragraph.",
              "blocks": [
                { "text": ["Two", "paragraphs"], "align": "left", "type": "text" },
                { "type": "cards", "items": [ { "image": "a.png", "title": "Post", "text": "Teaser", "link": "https://example.com" } ] },
                { "type": "callout", "text": "Hi", "sticker": { "image": "s.png", "stamp": { "country": "CHINA" } } },
                { "type": "map", "route": "lines", "stops": [ { "sequence": 1, "name": "A", "latitude": 1, "longitude": 2 } ] },
              ],
              "footer": { "lines": "Bye" }
            }
            """;

        var newsletter = JsonSerializer.Deserialize<Newsletter>(json, JsonOptions)!;

        Assert.Null(newsletter.Theme);

        Assert.Equal(["One paragraph."], newsletter.Intro);
        var text = Assert.IsType<NewsletterText>(newsletter.Blocks[0]);
        Assert.Equal(["Two", "paragraphs"], text.Text);
        Assert.Equal(NewsletterAlignment.Left, text.Align);
        Assert.Equal(["Teaser"], Assert.IsType<NewsletterCards>(newsletter.Blocks[1]).Items[0].Text);
        Assert.Equal(StampCorner.TopRight, Assert.IsType<NewsletterCallout>(newsletter.Blocks[2]).Sticker!.Stamp!.ToOptions().FeeCorner);
        Assert.Equal(TravelMapRoute.Lines, Assert.IsType<NewsletterMap>(newsletter.Blocks[3]).Route);
        Assert.Equal(["Bye"], newsletter.Footer.Lines);
    }

    [Theory]
    [InlineData("\"cosmos\"", "cosmos", null)]
    [InlineData("{ \"base\": \"minimal\", \"accentColor\": \"#e4572e\", \"ribbon\": \"plain\" }", "minimal", "#e4572e")]
    public void Json_ThemeIsANameOrSettings(string theme, string expectedBase, string? expectedAccent)
    {
        var json = $$"""{ "header": { "title": "T" }, "footer": { "lines": "x" }, "theme": {{theme}} }""";

        var parsed = JsonSerializer.Deserialize<Newsletter>(json, JsonOptions)!.Theme!;

        Assert.Equal((expectedBase, expectedAccent), (parsed.Base, parsed.AccentColor));
        Assert.Null(parsed.PageColor);
    }

    [Fact]
    public void Json_TextThatIsntStrings_IsRejected() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Newsletter>("""{ "header": { "title": "T" }, "intro": [1], "footer": { "lines": "x" } }""", JsonOptions));
}
