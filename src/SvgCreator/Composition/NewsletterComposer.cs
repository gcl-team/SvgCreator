using System.Xml;
using SvgCreator.Imaging;
using SvgCreator.Mapping;
using SvgCreator.Svg;
using static SvgCreator.Composition.AccentText;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Composition;

/// <summary>
/// Lays out a newsletter: one column of sections stacked top to bottom (the header band, the
/// hero, the body with the intro and each block, the footer band) on a patterned backdrop. Each
/// section is measured first, so the canvas is exactly as tall as the content, then written at its
/// place. Every size is in pixels at a 640 px column and scales with the column's width; text is
/// native <c>&lt;text&gt;</c>, wrapped by <see cref="TextWrap"/>. The colours, typeface and shapes
/// come from the <see cref="NewsletterTheme"/>, resolved, so every setting is there.
/// </summary>
internal static class NewsletterComposer
{
    private const double ReferenceWidth = 640;

    // The font stacks: Latin in the faces FontMetrics measured, CJK from whichever of the rest the
    // viewer has. Every CJK face sets its characters 1 em wide, as FontMetrics assumes.
    internal const string SansFamily = "Helvetica, Arial, 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', 'Noto Sans CJK SC', sans-serif";
    internal const string SerifFamily = "Georgia, 'Times New Roman', 'Songti SC', 'STSong', 'SimSun', 'Noto Serif CJK SC', serif";

    private const double Padding = 32;           // the column's edge to the body's content
    private const double Ascent = 0.8;           // a line's baseline below the top of its glyphs, in em
    private const double MutedOpacity = 0.75;    // captions, labels and small print

    private const double HeaderHeight = 84;
    private const double LogoWidth = 300;
    private const double LogoHeight = 52;
    private const double TitleSize = 28;
    private const double StackedLogoHeight = 44; // a logo with the title under it
    private const double StackedTitleSize = 22;
    private const double StackedGap = 6;         // between the logo and the title under it
    private const double BandPadding = 16;       // above and below a stacked logo and title

    private const double HeroMinShape = 0.4;     // height per width: 5:2
    private const double HeroMaxShape = 0.8;     // 5:4
    private const double WaveDepth = 18;         // how far the hero's bottom edge rises and falls
    private const double WaveStroke = 3;
    private const double ArcRise = 30;           // how far a planet's horizon rises into the hero at its middle
    private const double ArcGlow = 10;           // the glow's width along it
    private const double ArcLine = 2;

    private const double BodyTop = 32;
    private const double BlockGap = 28;
    private const double BodyBottom = 44;
    private const double BodySize = 15;
    private const double BodyLineHeight = 1.7;   // in em
    private const double ParagraphGap = 6;

    private const double RibbonHeight = 42;
    private const double RibbonInset = 44;       // between the content's edge and the bar, for its pixel ends
    private const double RibbonStep = 8;         // the stepped ends' size
    private const double RibbonPixel = 6;        // the chevrons' squares
    private const double RibbonTextSize = 19;
    private const double RibbonRadius = 6;       // a plain bar's corners
    private const double TabGap = 8;             // between a tab heading and its underline
    private const double TabRule = 1.5;          // the underline across the column
    private const double TabMark = 4;            // the thicker underline under the heading

    private const double CardGap = 20;           // between a card's picture and its text
    private const double CardShape = 9.0 / 16;
    private const double CardRadius = 8;
    private const double CardTextTop = 14;
    private const double CardTitleSize = 19;
    private const double CardTitleGap = 8;
    private const double CardTextSize = 14;
    private const double CardRowGap = 18;

    private const double ImageMinShape = 0.3;    // 10:3
    private const double ImageMaxShape = 1.25;   // 4:5
    private const double ImageRadius = 8;
    private const double CaptionSize = 12.5;
    private const double CaptionGap = 8;

    private const double ButtonHeight = 52;
    private const double ButtonMinWidth = 0.5;   // of the content's width
    private const double ButtonTextPadding = 48; // each side of its text
    private const double ButtonTextSize = 16;

    private const double StatsLabelSize = 12;
    private const double StatsLabelGap = 10;
    private const double StatsHeight = 62;
    private const double StatsMinWidth = 0.6;    // of the content's width
    private const double StatsTextPadding = 40;
    private const double StatsValueSize = 26;
    private const double StatsRadius = 6;

    private const double StickerSize = 132;
    private const double StickerGap = 22;
    private const double StickerTilt = -6;       // degrees: a stamp or postmark is stuck on at a slant

    private const double MapShape = 0.62;
    private const double MapRadius = 8;
    private const double MapTextScale = 0.6;     // TravelMapOptions.Scale for a map the content's width at 640 px

    private const double FooterTop = 30;
    private const double FooterBottom = 32;
    private const double FooterLinkSize = 15;
    private const double FooterRowGap = 8;
    private const double FooterLinesGap = 18;
    private const double FooterTextSize = 11;
    private const double FooterLineHeight = 1.6;

    private const double PatternTile = 96;
    private const double PatternPitch = 6;
    private const double PatternSquare = 3.5;
    private const int PatternRadius = 4;         // squares from a diamond's centre to its tip
    private const double DotPitch = 14;
    private const double DotRadius = 1.4;
    private const double StarTile = 200;
    private const int StarCount = 46;
    private const int SparkleCount = 4;

    // Between footer links: a bar with an en space each side, which SVG doesn't collapse as it does spaces.
    private const string Separator = "\u2002|\u2002";

    private const string SheenId = "newsletter-sheen";
    private const string DiamondId = "newsletter-diamond";
    private const string PatternId = "newsletter-backdrop-pattern";

    /// <summary>A part of the column: its height, and how to write it with its top at a given y.</summary>
    private sealed record Section(double Height, Action<XmlWriter, double> Write);

    /// <summary>What every section is laid out with.</summary>
    private sealed record Context(NewsletterOptions Options, NewsletterTheme Theme, double Unit, NewsletterImages Images, CancellationToken CancellationToken)
    {
        public double Width => Options.Width;

        public double Padding => NewsletterComposer.Padding * Unit;

        public double Content => Width - (2 * Padding);

        public FontMetrics Metrics => Theme.Font == NewsletterFont.Serif ? FontMetrics.Serif : FontMetrics.Sans;

        public string FontFamily => Theme.Font == NewsletterFont.Serif ? SerifFamily : SansFamily;

        public bool Square => Theme.Corners == NewsletterCorners.Square;

        /// <summary>A corner radius given in pixels at 640 px; none with square corners.</summary>
        public double Radius(double radius) => Square ? 0 : radius * Unit;

        public string BackdropColor => Theme.BackdropColor!;

        public string PageColor => Theme.PageColor!;

        public string BandColor => Theme.BandColor!;

        public string BandTextColor => Theme.BandTextColor!;

        public string TextColor => Theme.TextColor!;

        public string AccentColor => Theme.AccentColor!;

        public string LabelColor => Theme.LabelColor!;

        public string ButtonColor => Theme.ButtonColor!;

        public string PanelColor => Theme.PanelColor!;
    }

    /// <summary>Text broken into lines: each line's runs and top, and the block's size.</summary>
    private sealed record Text(IReadOnlyList<(IReadOnlyList<Run> Runs, double Top)> Lines, double Size, double Width, double Height)
    {
        public double LineHeight { get; init; } = Size * BodyLineHeight;
    }

    public static string Compose(Newsletter newsletter, NewsletterOptions options, NewsletterTheme theme, IReadOnlyDictionary<string, EncodedImage> images, CancellationToken cancellationToken)
    {
        var c = new Context(options, theme, options.Width / ReferenceWidth, new NewsletterImages(images, options.ImageDensity), cancellationToken);

        var sections = new List<Section> { Header(c, newsletter.Header) };
        if (newsletter.Hero is { } hero)
        {
            sections.Add(Hero(c, hero));
        }

        var body = new List<Section>();
        if (newsletter.Intro.Count > 0)
        {
            body.Add(Paragraphs(c, newsletter.Intro, NewsletterAlignment.Center, "newsletter-intro"));
        }

        foreach (var block in newsletter.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            body.Add(block switch
            {
                NewsletterRibbon ribbon => Ribbon(c, ribbon),
                NewsletterText text => Paragraphs(c, text.Text, text.Align, "newsletter-text"),
                NewsletterCards cards => Cards(c, cards),
                NewsletterImage image => Image(c, image),
                NewsletterButton button => Button(c, button),
                NewsletterStats stats => Stats(c, stats),
                NewsletterCallout callout => Callout(c, callout),
                NewsletterMap map => Map(c, map),
                _ => throw new ArgumentException($"Unknown newsletter block {block.GetType().Name}.", nameof(newsletter)),
            });
        }

        sections.Add(Body(c, body));
        sections.Add(Footer(c, newsletter.Footer));

        var margin = options.Margin;
        var columnHeight = (int)Math.Ceiling(sections.Sum(s => s.Height));
        var (width, height) = (options.Width + (2 * margin), columnHeight + (2 * margin));
        return SvgWriter.WriteDocument(width, height, c.TextColor, SvgStyle.Clean, (xml, _) =>
        {
            xml.WriteElementString("title", Namespace, AccentText.Plain(newsletter.Header.Title));

            xml.WriteStartElement("defs", Namespace);
            WriteSheen(xml);
            var pattern = margin > 0 && WritePattern(xml, c);

            c.Images.WriteDefinitions(xml);
            xml.WriteEndElement();

            if (margin > 0)
            {
                xml.WriteStartElement("g", Namespace);
                xml.WriteAttributeString("class", "newsletter-backdrop");
                NewsletterImages.Rect(xml, new RectD(0, 0, width, height), 0, close: false);
                xml.WriteAttributeString("fill", c.BackdropColor);
                xml.WriteEndElement();
                if (pattern)
                {
                    NewsletterImages.Rect(xml, new RectD(0, 0, width, height), 0, close: false);
                    xml.WriteAttributeString("fill", $"url(#{PatternId})");
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
            }

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-column");
            if (margin > 0)
            {
                xml.WriteAttributeString("transform", $"translate({Integer(margin)} {Integer(margin)})");
            }

            xml.WriteAttributeString("font-family", c.FontFamily);
            xml.WriteAttributeString("fill", c.TextColor);
            NewsletterImages.Rect(xml, new RectD(0, 0, options.Width, columnHeight), 0, close: false);
            xml.WriteAttributeString("class", "newsletter-page");
            xml.WriteAttributeString("fill", c.PageColor);
            xml.WriteEndElement();

            var top = 0.0;
            foreach (var section in sections)
            {
                section.Write(xml, top);
                top += section.Height;
            }

            xml.WriteEndElement();
        });
    }

    // The header band: the title alone, the logo alone, or by default the logo with the title
    // stacked under it, the pair centred in a taller band.
    private static Section Header(Context c, NewsletterHeader header)
    {
        var logo = header.Logo;
        var showTitle = logo is null || !header.HideTitle;
        var stacked = logo is not null && showTitle;
        var logoHeight = (stacked ? StackedLogoHeight : LogoHeight) * c.Unit;
        var title = showTitle ? OneLine(c, header.Title, (stacked ? StackedTitleSize : TitleSize) * c.Unit, c.Content) : null;
        var gap = StackedGap * c.Unit;

        // Stacked, the band is as tall as the pair plus room above and below; otherwise it's fixed.
        var height = stacked ? logoHeight + gap + title!.Height + (2 * BandPadding * c.Unit) : HeaderHeight * c.Unit;
        var logoBox = default(RectD);
        if (logo is not null)
        {
            var width = Math.Min(LogoWidth * c.Unit, c.Content);
            var y = stacked ? BandPadding * c.Unit : (height - logoHeight) / 2;
            logoBox = new RectD((c.Width - width) / 2, y, width, logoHeight);
            c.Images.Need(logo, width, logoHeight, cover: false);
        }

        return new Section(height, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-header");
            xml.WriteAttributeString("fill", c.BandTextColor);
            NewsletterImages.Rect(xml, new RectD(0, top, c.Width, height), 0, close: false);
            xml.WriteAttributeString("fill", c.BandColor);
            xml.WriteEndElement();

            if (logo is not null)
            {
                c.Images.Draw(xml, logo, logoBox with { Y = top + logoBox.Y }, cover: false);
            }

            if (title is not null)
            {
                var titleTop = stacked ? top + logoBox.Bottom + gap : top + ((height - title.Height) / 2);
                WriteText(xml, c, title, c.Width / 2, titleTop, "middle", "newsletter-title", bold: true);
            }

            xml.WriteEndElement();
        });
    }

    // The hero: the picture across the column, its bottom edge cut as the theme says: in a wave
    // lined in the accent, straight, or in a planet's arc with a glow along it.
    private static Section Hero(Context c, NewsletterHero hero)
    {
        var (w, h) = c.Images.Size(hero.Image);
        var height = c.Width * Math.Clamp(h / w, HeroMinShape, HeroMaxShape);
        c.Images.Need(hero.Image, c.Width, height, cover: true);

        return new Section(height, (xml, top) =>
        {
            var box = new RectD(0, top, c.Width, height);
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-hero");
            if (c.Theme.HeroEdge == NewsletterHeroEdge.Straight)
            {
                c.Images.Draw(xml, hero.Image, box, cover: true);
                xml.WriteEndElement();
                return;
            }

            var bottom = top + height;
            var (edge, outline) = c.Theme.HeroEdge == NewsletterHeroEdge.Arc
                ? Arc(c.Width, bottom, ArcRise * c.Unit)
                : Wave(c.Width, bottom, WaveDepth * c.Unit);
            var clip = "newsletter-hero-edge-clip";
            xml.WriteStartElement("clipPath", Namespace);
            xml.WriteAttributeString("id", clip);
            xml.WriteStartElement("path", Namespace);
            xml.WriteAttributeString("d", $"M0 {Number(top)}H{Number(c.Width)}{outline}Z");
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("clip-path", $"url(#{clip})");
            c.Images.Draw(xml, hero.Image, box, cover: true);
            xml.WriteEndElement();

            // An arc's glow is a wide, faint stroke under the line, so it needs no filter.
            var strokes = c.Theme.HeroEdge == NewsletterHeroEdge.Arc
                ? new[] { (ArcGlow, 0.25), (ArcLine, 1.0) }
                : [(WaveStroke, 1.0)];
            foreach (var (stroke, opacity) in strokes)
            {
                xml.WriteStartElement("path", Namespace);
                xml.WriteAttributeString("class", "newsletter-hero-edge");
                xml.WriteAttributeString("d", edge);
                xml.WriteAttributeString("fill", "none");
                xml.WriteAttributeString("stroke", c.AccentColor);
                xml.WriteAttributeString("stroke-width", Number(stroke * c.Unit));
                if (opacity < 1)
                {
                    xml.WriteAttributeString("stroke-opacity", Number(opacity));
                }

                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        });
    }

    /// <summary>
    /// A planet's horizon along the hero's bottom: one quadratic from corner to corner, rising
    /// <paramref name="rise"/> at its middle (its control point twice as high), as a path from left
    /// to right and as the clip path's way back from the right side.
    /// </summary>
    private static (string Edge, string Outline) Arc(double width, double bottom, double rise)
    {
        var control = $"{Number(width / 2)} {Number(bottom - (2 * rise))}";
        return ($"M0 {Number(bottom)}Q{control} {Number(width)} {Number(bottom)}", $"V{Number(bottom)}Q{control} 0 {Number(bottom)}");
    }

    /// <summary>
    /// The hero's bottom edge, a gentle wave between <paramref name="bottom"/> and
    /// <paramref name="depth"/> above it: as a path from left to right, and as the same curve from
    /// right to left to close the clip path, starting with the line down the right side.
    /// </summary>
    private static (string Edge, string Outline) Wave(double width, double bottom, double depth)
    {
        string P(double x, double rise) => $"{Number(x * width)} {Number(bottom - (rise * depth))}";

        // Two cubics meeting smoothly: the controls either side of the join are mirror images.
        var edge = $"M{P(0, 0.3)}C{P(0.25, 0.05)} {P(0.45, 0.05)} {P(0.6, 0.55)}C{P(0.75, 1.05)} {P(0.9, 1)} {P(1, 0.6)}";
        var outline = $"V{Number(bottom - (0.6 * depth))}C{P(0.9, 1)} {P(0.75, 1.05)} {P(0.6, 0.55)}C{P(0.45, 0.05)} {P(0.25, 0.05)} {P(0, 0.3)}";
        return (edge, outline);
    }

    // The intro and every block between it and the footer, with room above, between and below.
    private static Section Body(Context c, List<Section> blocks)
    {
        var (top, gap, bottom) = (BodyTop * c.Unit, BlockGap * c.Unit, BodyBottom * c.Unit);
        var height = blocks.Count == 0 ? top : top + blocks.Sum(b => b.Height) + (gap * (blocks.Count - 1)) + bottom;
        return new Section(height, (xml, y) =>
        {
            y += top;
            foreach (var block in blocks)
            {
                block.Write(xml, y);
                y += block.Height + gap;
            }
        });
    }

    private static Section Paragraphs(Context c, IReadOnlyList<string> paragraphs, NewsletterAlignment align, string className)
    {
        var text = Lay(c, paragraphs, BodySize * c.Unit, c.Content, ParagraphGap * c.Unit, balance: align == NewsletterAlignment.Center);
        return new Section(text.Height, (xml, top) =>
        {
            var (x, anchor) = align == NewsletterAlignment.Left ? (c.Padding, "start") : (c.Width / 2, "middle");
            WriteText(xml, c, text, x, top, anchor, className);
        });
    }

    private static Section Ribbon(Context c, NewsletterRibbon ribbon) => c.Theme.Ribbon switch
    {
        NewsletterRibbonStyle.Plain => PlainRibbon(c, ribbon),
        NewsletterRibbonStyle.Tab => TabRibbon(c, ribbon),
        _ => PixelRibbon(c, ribbon),
    };

    // A heading on a bar across the column in the accent colour.
    private static Section PlainRibbon(Context c, NewsletterRibbon ribbon)
    {
        var height = RibbonHeight * c.Unit;
        var text = OneLine(c, ribbon.Text, RibbonTextSize * c.Unit, c.Content - (2 * RibbonStep * c.Unit));
        return new Section(height, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-ribbon");
            NewsletterImages.Rect(xml, new RectD(c.Padding, top, c.Content, height), c.Radius(RibbonRadius), close: false);
            xml.WriteAttributeString("fill", c.AccentColor);
            xml.WriteEndElement();
            WriteText(xml, c, text, c.Width / 2, top + ((height - text.Height) / 2), "middle", "newsletter-ribbon-text", bold: true, fill: c.LabelColor);
            xml.WriteEndElement();
        });
    }

    // A heading on the left in bold, with a thin rule in the accent across the column under it
    // and a thicker one under the heading itself.
    private static Section TabRibbon(Context c, NewsletterRibbon ribbon)
    {
        var text = OneLine(c, ribbon.Text, RibbonTextSize * c.Unit, c.Content);
        var (gap, rule, mark) = (TabGap * c.Unit, TabRule * c.Unit, TabMark * c.Unit);
        return new Section(text.Height + gap + mark, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-ribbon");
            WriteText(xml, c, text, c.Padding, top, "start", "newsletter-ribbon-text", bold: true);
            var line = top + text.Height + gap;
            NewsletterImages.Rect(xml, new RectD(c.Padding, line + mark - rule, c.Content, rule), 0, close: false);
            xml.WriteAttributeString("fill", c.AccentColor);
            xml.WriteAttributeString("fill-opacity", "0.35");
            xml.WriteEndElement();
            NewsletterImages.Rect(xml, new RectD(c.Padding, line, text.Width, mark), 0, close: false);
            xml.WriteAttributeString("fill", c.AccentColor);
            xml.WriteEndElement();
            xml.WriteEndElement();
        });
    }

    // A heading on a bar in the accent colour, with stepped pixel ends and a pixel chevron off each.
    private static Section PixelRibbon(Context c, NewsletterRibbon ribbon)
    {
        var (height, inset, step, pixel) = (RibbonHeight * c.Unit, RibbonInset * c.Unit, RibbonStep * c.Unit, RibbonPixel * c.Unit);
        var barWidth = c.Content - (2 * inset);
        var text = OneLine(c, ribbon.Text, RibbonTextSize * c.Unit, barWidth - (2 * step));

        return new Section(height, (xml, top) =>
        {
            var bar = new RectD(c.Padding + inset, top, barWidth, height);
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-ribbon");
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("fill", c.AccentColor);
            NewsletterImages.Rect(xml, bar, 0);
            foreach (var side in new[] { -1, 1 })
            {
                // Each end steps in twice, then a "<" (or ">") of three squares stands off it.
                var edge = side < 0 ? bar.X : bar.Right;
                double Out(double distance, double size) => side < 0 ? edge - distance - size : edge + distance;
                NewsletterImages.Rect(xml, new RectD(Out(0, step), top + step, step, height - (2 * step)), 0);
                NewsletterImages.Rect(xml, new RectD(Out(step, step), top + (2 * step), step, height - (4 * step)), 0);

                var middle = top + (height / 2) - (pixel / 2);
                var tip = Out((2 * step) + (2.5 * pixel), pixel);
                var back = Out((2 * step) + (1.5 * pixel), pixel);
                xml.WriteStartElement("g", Namespace);
                xml.WriteAttributeString("opacity", "0.8");
                NewsletterImages.Rect(xml, new RectD(tip, middle, pixel, pixel), 0);
                NewsletterImages.Rect(xml, new RectD(back, middle - pixel, pixel, pixel), 0);
                NewsletterImages.Rect(xml, new RectD(back, middle + pixel, pixel, pixel), 0);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();

            // A sheen from clear at the top to light at the bottom, and a highlight along it.
            NewsletterImages.Rect(xml, bar, 0, close: false);
            xml.WriteAttributeString("fill", $"url(#{SheenId})");
            xml.WriteEndElement();
            NewsletterImages.Rect(xml, new RectD(bar.X, bar.Bottom - (3 * c.Unit), bar.Width, 1.5 * c.Unit), 0, close: false);
            xml.WriteAttributeString("fill", "#ffffff");
            xml.WriteAttributeString("fill-opacity", "0.35");
            xml.WriteEndElement();

            WriteText(xml, c, text, c.Width / 2, top + ((height - text.Height) / 2), "middle", "newsletter-ribbon-text", bold: true, fill: c.LabelColor);
            xml.WriteEndElement();
        });
    }

    // Rows of a picture and its text, the picture on the left in the first row and swapping after.
    private static Section Cards(Context c, NewsletterCards cards)
    {
        var gap = CardGap * c.Unit;
        var pictureWidth = (c.Content - gap) / 2;
        var pictureHeight = pictureWidth * CardShape;
        var textWidth = c.Content - pictureWidth - gap;

        var rows = new List<(NewsletterCard Card, Text Title, Text? Body, double Height)>();
        foreach (var card in cards.Items)
        {
            c.Images.Need(card.Image, pictureWidth, pictureHeight, cover: true);
            var title = Lay(c, [card.Title], CardTitleSize * c.Unit, textWidth, 0, 1.35);
            var body = card.Text.Count > 0 ? Lay(c, card.Text, CardTextSize * c.Unit, textWidth, ParagraphGap * c.Unit * 0.5) : null;
            var textHeight = (CardTextTop * c.Unit) + title.Height + (body is null ? 0 : (CardTitleGap * c.Unit) + body.Height);
            rows.Add((card, title, body, Math.Max(pictureHeight, textHeight)));
        }

        var rowGap = CardRowGap * c.Unit;
        var height = rows.Sum(r => r.Height) + (rowGap * (rows.Count - 1));
        return new Section(height, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-cards");
            for (var i = 0; i < rows.Count; i++)
            {
                var (card, title, body, rowHeight) = rows[i];
                var pictureLeft = i % 2 == 0;
                var pictureX = pictureLeft ? c.Padding : c.Width - c.Padding - pictureWidth;
                var textX = pictureLeft ? c.Padding + pictureWidth + gap : c.Padding;

                OpenLink(xml, card.Link);
                xml.WriteStartElement("g", Namespace);
                xml.WriteAttributeString("class", "newsletter-card");
                c.Images.Draw(xml, card.Image, new RectD(pictureX, top, pictureWidth, pictureHeight), cover: true, c.Radius(CardRadius));
                var textTop = top + (CardTextTop * c.Unit);
                WriteText(xml, c, title, textX, textTop, "start", "newsletter-card-title", bold: true);
                if (body is not null)
                {
                    WriteText(xml, c, body, textX, textTop + title.Height + (CardTitleGap * c.Unit), "start", "newsletter-card-text");
                }

                xml.WriteEndElement();
                CloseLink(xml, card.Link);
                top += rowHeight + rowGap;
            }

            xml.WriteEndElement();
        });
    }

    // A picture across the content, rounded, with its caption under it.
    private static Section Image(Context c, NewsletterImage image)
    {
        var (w, h) = c.Images.Size(image.Image);
        var pictureHeight = c.Content * Math.Clamp(h / w, ImageMinShape, ImageMaxShape);
        c.Images.Need(image.Image, c.Content, pictureHeight, cover: true);
        var caption = image.Caption is { } line ? Lay(c, [line], CaptionSize * c.Unit, c.Content, 0, 1.5, balance: true) : null;
        var height = pictureHeight + (caption is null ? 0 : (CaptionGap * c.Unit) + caption.Height);

        return new Section(height, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-image");
            OpenLink(xml, image.Link);
            c.Images.Draw(xml, image.Image, new RectD(c.Padding, top, c.Content, pictureHeight), cover: true, c.Radius(ImageRadius));
            CloseLink(xml, image.Link);
            if (caption is not null)
            {
                WriteText(xml, c, caption, c.Width / 2, top + pictureHeight + (CaptionGap * c.Unit), "middle", "newsletter-caption", muted: true);
            }

            xml.WriteEndElement();
        });
    }

    // A pill (or with square corners, a box), wide enough for its text and at least half the content.
    private static Section Button(Context c, NewsletterButton button)
    {
        var height = ButtonHeight * c.Unit;
        var padding = ButtonTextPadding * c.Unit;
        var text = OneLine(c, button.Text, ButtonTextSize * c.Unit, c.Content - (2 * padding));
        var width = Math.Min(c.Content, Math.Max(ButtonMinWidth * c.Content, text.Width + (2 * padding)));

        return new Section(height, (xml, top) =>
        {
            OpenLink(xml, button.Link);
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-button");
            NewsletterImages.Rect(xml, new RectD((c.Width - width) / 2, top, width, height), c.Square ? 0 : height / 2, close: false);
            xml.WriteAttributeString("fill", c.ButtonColor);
            xml.WriteEndElement();
            WriteText(xml, c, text, c.Width / 2, top + ((height - text.Height) / 2), "middle", "newsletter-button-text", bold: true, fill: c.LabelColor);
            xml.WriteEndElement();
            CloseLink(xml, button.Link);
        });
    }

    // A small label over a box with the value set large in it.
    private static Section Stats(Context c, NewsletterStats stats)
    {
        var label = stats.Label is { } line ? OneLine(c, line, StatsLabelSize * c.Unit, c.Content) : null;
        var labelHeight = label is null ? 0 : label.Height + (StatsLabelGap * c.Unit);
        var boxHeight = StatsHeight * c.Unit;
        var padding = StatsTextPadding * c.Unit;
        var value = OneLine(c, stats.Value, StatsValueSize * c.Unit, c.Content - (2 * padding));
        var boxWidth = Math.Min(c.Content, Math.Max(StatsMinWidth * c.Content, value.Width + (2 * padding)));

        return new Section(labelHeight + boxHeight, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-stats");
            if (label is not null)
            {
                WriteText(xml, c, label, c.Width / 2, top, "middle", "newsletter-stats-label", muted: true);
            }

            var boxTop = top + labelHeight;
            NewsletterImages.Rect(xml, new RectD((c.Width - boxWidth) / 2, boxTop, boxWidth, boxHeight), c.Radius(StatsRadius), close: false);
            xml.WriteAttributeString("fill", c.PanelColor);
            xml.WriteEndElement();
            OpenLink(xml, stats.Link);
            WriteText(xml, c, value, c.Width / 2, boxTop + ((boxHeight - value.Height) / 2), "middle", "newsletter-stats-value", underline: stats.Link is not null);
            CloseLink(xml, stats.Link);
            xml.WriteEndElement();
        });
    }

    // Text beside a sticker on the left: the picture, a postage stamp of it, or a postmark traced from it.
    private static Section Callout(Context c, NewsletterCallout callout)
    {
        var size = StickerSize * c.Unit;
        var gap = StickerGap * c.Unit;
        var sticker = callout.Sticker is { } s ? Sticker(c, s, size) : null;
        var textX = sticker is null ? c.Padding : c.Padding + size + gap;
        var text = Lay(c, callout.Text, BodySize * c.Unit, c.Width - c.Padding - textX, ParagraphGap * c.Unit);
        var stickerHeight = sticker?.Height ?? 0;
        var height = Math.Max(stickerHeight, text.Height);

        return new Section(height, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-callout");
            sticker?.Write(xml, top + ((height - stickerHeight) / 2));
            WriteText(xml, c, text, textX, top + ((height - text.Height) / 2), "start", "newsletter-callout-text");
            xml.WriteEndElement();
        });
    }

    // The sticker, whole inside a square on the left; a stamp or postmark is tilted, stuck on.
    private static Section Sticker(Context c, NewsletterSticker sticker, double size)
    {
        if (sticker is { Stamp: null, Postmark: false })
        {
            var (w, h) = c.Images.Size(sticker.Image);
            var scale = Math.Min(size / w, size / h);
            c.Images.Need(sticker.Image, size, size, cover: false);
            return new Section(h * scale, (xml, top) =>
                c.Images.Draw(xml, sticker.Image, new RectD(c.Padding + ((size - (w * scale)) / 2), top, w * scale, h * scale), cover: false));
        }

        string svg, className;
        if (sticker.Stamp is { } stamp)
        {
            svg = PostageStampWriter.Write(c.Images.Sized(sticker.Image, size, size, cover: false), stamp.ToOptions());
            className = "newsletter-sticker-stamp";
        }
        else
        {
            var options = PostmarkPiece.DefaultOptions with { FillColor = c.TextColor };
            var image = c.Images[sticker.Image];
            var drawing = LineArtTracer.Trace(ImageLoader.Load(new MemoryStream(image.Data)), options, c.CancellationToken);
            svg = SvgConverter.Render(drawing, options);
            className = "newsletter-sticker-postmark";
        }

        var (width, height) = NestedSvg.Size(svg);
        var fit = Math.Min(size / width, size / height);
        var (fw, fh) = (width * fit, height * fit);
        return new Section(fh, (xml, top) =>
        {
            var x = c.Padding + ((size - fw) / 2);
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("transform", $"rotate({Number(StickerTilt)} {Number(x + (fw / 2))} {Number(top + (fh / 2))})");
            NestedSvg.Nest(xml, className, svg, (x, top, fw, fh));
            xml.WriteEndElement();
        });
    }

    // A travel map across the content, drawn by the map's own writer and nested, rounded.
    private static Section Map(Context c, NewsletterMap map)
    {
        // Drawn at its size in a 640 px column and scaled with the column, like everything else.
        var width = (int)Math.Round(c.Content / c.Unit);
        var height = (int)Math.Round(width * MapShape);
        var svg = TravelMapWriter.Write(map.Stops, new TravelMapOptions
        {
            Width = width,
            Height = height,
            BackgroundColor = c.PanelColor,
            ForegroundColor = c.TextColor,
            WaypointColor = c.AccentColor,
            Scale = MapTextScale,
            Route = map.Route,
            Breaks = map.Breaks,
            Layout = map.Layout ?? TravelMapLayout.Names,
        });
        var drawnHeight = height * c.Unit;

        return new Section(drawnHeight, (xml, top) =>
        {
            var box = new RectD(c.Padding, top, c.Content, drawnHeight);
            var clip = c.Images.NextClipId();
            xml.WriteStartElement("clipPath", Namespace);
            xml.WriteAttributeString("id", clip);
            NewsletterImages.Rect(xml, box, c.Radius(MapRadius));
            xml.WriteEndElement();
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-map");
            xml.WriteAttributeString("clip-path", $"url(#{clip})");
            NestedSvg.Nest(xml, "newsletter-map-drawing", svg, (box.X, box.Y, box.Width, box.Height));
            xml.WriteEndElement();
        });
    }

    // The footer band: the links in rows, divided by bars, then the small print.
    private static Section Footer(Context c, NewsletterFooter footer)
    {
        var linkSize = FooterLinkSize * c.Unit;
        var separatorWidth = c.Metrics.Width(Separator, linkSize);
        var rows = new List<List<(NewsletterLink Link, double Width)>>();
        foreach (var link in footer.Links)
        {
            var width = Math.Min(c.Metrics.Width(link.Text, linkSize), c.Content);
            var row = rows.Count > 0 ? rows[^1] : null;
            if (row is null || RowWidth(row) + separatorWidth + width > c.Content)
            {
                rows.Add([(link, width)]);
            }
            else
            {
                row.Add((link, width));
            }
        }

        double RowWidth(List<(NewsletterLink Link, double Width)> row) => row.Sum(l => l.Width) + ((row.Count - 1) * separatorWidth);

        var rowHeight = linkSize * 1.4;
        var rowGap = FooterRowGap * c.Unit;
        var linksHeight = rows.Count == 0 ? 0 : (rows.Count * rowHeight) + ((rows.Count - 1) * rowGap);
        var lines = footer.Lines.Count > 0 ? Lay(c, footer.Lines, FooterTextSize * c.Unit, c.Content, 0, FooterLineHeight, balance: true) : null;
        var between = rows.Count > 0 && lines is not null ? FooterLinesGap * c.Unit : 0;
        var height = (FooterTop * c.Unit) + linksHeight + between + (lines?.Height ?? 0) + (FooterBottom * c.Unit);

        return new Section(height, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "newsletter-footer");
            xml.WriteAttributeString("fill", c.BandTextColor);
            NewsletterImages.Rect(xml, new RectD(0, top, c.Width, height), 0, close: false);
            xml.WriteAttributeString("fill", c.BandColor);
            xml.WriteEndElement();

            // Each row is one centred line of text, so the viewer's font spaces the links and
            // bars evenly; the estimated widths only decide where rows break.
            var y = top + (FooterTop * c.Unit);
            foreach (var row in rows)
            {
                xml.WriteStartElement("text", Namespace);
                xml.WriteAttributeString("class", "newsletter-footer-links");
                xml.WriteAttributeString("x", Number(c.Width / 2));
                xml.WriteAttributeString("y", Number(y + ((rowHeight - linkSize) / 2) + (Ascent * linkSize)));
                xml.WriteAttributeString("font-size", Number(linkSize));
                xml.WriteAttributeString("text-anchor", "middle");
                for (var i = 0; i < row.Count; i++)
                {
                    if (i > 0)
                    {
                        xml.WriteStartElement("tspan", Namespace);
                        xml.WriteAttributeString("class", "newsletter-footer-bar");
                        xml.WriteAttributeString("fill-opacity", Number(MutedOpacity));
                        xml.WriteString(Separator);
                        xml.WriteEndElement();
                    }

                    var link = row[i].Link;
                    xml.WriteStartElement("a", Namespace);
                    xml.WriteAttributeString("href", link.Url);
                    xml.WriteStartElement("tspan", Namespace);
                    xml.WriteAttributeString("class", "newsletter-footer-link");
                    xml.WriteAttributeString("text-decoration", "underline");
                    xml.WriteString(link.Text);
                    xml.WriteEndElement();
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                y += rowHeight + rowGap;
            }

            if (lines is not null)
            {
                y += between - (rows.Count > 0 ? rowGap : 0);
                WriteText(xml, c, lines, c.Width / 2, y, "middle", "newsletter-footer-text", muted: true);
            }

            xml.WriteEndElement();
        });
    }

    /// <summary>
    /// Paragraphs wrapped to <paramref name="width"/>, a gap between paragraphs. Balanced, each
    /// paragraph is wrapped to the narrowest width that takes no more lines, so centred text
    /// doesn't leave one word alone on its last line.
    /// </summary>
    private static Text Lay(Context c, IEnumerable<string> paragraphs, double size, double width, double paragraphGap, double lineHeight = BodyLineHeight, bool balance = false)
    {
        var lines = new List<(IReadOnlyList<Run> Runs, double Top)>();
        var (top, widest) = (0.0, 0.0);
        var first = true;
        foreach (var paragraph in paragraphs)
        {
            top += first ? 0 : paragraphGap;
            first = false;
            foreach (var line in balance ? Balanced(c, paragraph, size, width) : AccentText.Wrap(paragraph, size, width, c.Metrics))
            {
                lines.Add((line, top));
                widest = Math.Max(widest, c.Metrics.Width(string.Concat(line.Select(r => r.Text)), size));
                top += size * lineHeight;
            }
        }

        return new Text(lines, size, widest, top) { LineHeight = size * lineHeight };
    }

    // Fewer lines never come from a narrower width, so the narrowest width keeping the count is
    // found by bisection between the width split evenly across the lines and the full width.
    private static List<IReadOnlyList<Run>> Balanced(Context c, string paragraph, double size, double width)
    {
        var lines = AccentText.Wrap(paragraph, size, width, c.Metrics);
        if (lines.Count < 2)
        {
            return lines;
        }

        var (low, high) = (c.Metrics.Width(AccentText.Plain(paragraph), size) / lines.Count, width);
        for (var i = 0; i < 12; i++)
        {
            var middle = (low + high) / 2;
            (low, high) = AccentText.Wrap(paragraph, size, middle, c.Metrics).Count > lines.Count ? (middle, high) : (low, middle);
        }

        return AccentText.Wrap(paragraph, size, high, c.Metrics);
    }

    /// <summary>One line, made smaller until it fits <paramref name="width"/>.</summary>
    private static Text OneLine(Context c, string text, double size, double width)
    {
        var natural = c.Metrics.Width(AccentText.Plain(text), size);
        var fitted = natural > width ? size * width / natural : size;
        return Lay(c, [text], fitted, double.PositiveInfinity, 0, 1.3);
    }

    private static void WriteText(XmlWriter xml, Context c, Text text, double x, double top, string anchor, string className, bool bold = false, bool muted = false, bool underline = false, string? fill = null)
    {
        xml.WriteStartElement("text", Namespace);
        xml.WriteAttributeString("class", className);
        if (fill is not null)
        {
            xml.WriteAttributeString("fill", fill);
        }

        xml.WriteAttributeString("font-size", Number(text.Size));
        if (anchor != "start")
        {
            xml.WriteAttributeString("text-anchor", anchor);
        }

        if (bold)
        {
            xml.WriteAttributeString("font-weight", "bold");
        }

        if (muted)
        {
            xml.WriteAttributeString("fill-opacity", Number(MutedOpacity));
        }

        if (underline)
        {
            xml.WriteAttributeString("text-decoration", "underline");
        }

        // Each line's glyphs centred in its line height.
        var offset = ((text.LineHeight - text.Size) / 2) + (Ascent * text.Size);
        foreach (var (runs, lineTop) in text.Lines)
        {
            xml.WriteStartElement("tspan", Namespace);
            xml.WriteAttributeString("x", Number(x));
            xml.WriteAttributeString("y", Number(top + lineTop + offset));
            if (runs is [{ Accent: false } only])
            {
                xml.WriteString(only.Text);
            }
            else
            {
                foreach (var run in runs)
                {
                    if (run.Accent)
                    {
                        xml.WriteStartElement("tspan", Namespace);
                        xml.WriteAttributeString("class", "newsletter-accent");
                        xml.WriteAttributeString("fill", c.AccentColor);
                        xml.WriteAttributeString("fill-opacity", "1");
                        xml.WriteString(run.Text);
                        xml.WriteEndElement();
                    }
                    else
                    {
                        xml.WriteString(run.Text);
                    }
                }
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void OpenLink(XmlWriter xml, string? url)
    {
        if (url is not null)
        {
            xml.WriteStartElement("a", Namespace);
            xml.WriteAttributeString("href", url);
        }
    }

    private static void CloseLink(XmlWriter xml, string? url)
    {
        if (url is not null)
        {
            xml.WriteEndElement();
        }
    }

    // White from clear at the top to light at the bottom, laid over each ribbon's bar.
    private static void WriteSheen(XmlWriter xml)
    {
        xml.WriteStartElement("linearGradient", Namespace);
        xml.WriteAttributeString("id", SheenId);
        xml.WriteAttributeString("x1", "0");
        xml.WriteAttributeString("y1", "0");
        xml.WriteAttributeString("x2", "0");
        xml.WriteAttributeString("y2", "1");
        foreach (var (offset, opacity) in new[] { ("0", "0"), ("1", "0.3") })
        {
            xml.WriteStartElement("stop", Namespace);
            xml.WriteAttributeString("offset", offset);
            xml.WriteAttributeString("stop-color", "#ffffff");
            xml.WriteAttributeString("stop-opacity", opacity);
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    /// <summary>
    /// Writes the backdrop's pattern, as the theme says, as <c>#newsletter-backdrop-pattern</c>;
    /// false when there's none.
    /// </summary>
    private static bool WritePattern(XmlWriter xml, Context c)
    {
        switch (c.Theme.Backdrop)
        {
            case NewsletterBackdrop.Diamonds:
                WriteDiamonds(xml, c);
                return true;
            case NewsletterBackdrop.Dots:
                WriteDots(xml, c);
                return true;
            case NewsletterBackdrop.Stars:
                WriteStars(xml, c);
                return true;
            default:
                return false;
        }
    }

    // A fine grid of dots in the page colour, like a halftone.
    private static void WriteDots(XmlWriter xml, Context c)
    {
        var pitch = DotPitch * c.Unit;
        StartPattern(xml, pitch);
        xml.WriteStartElement("circle", Namespace);
        xml.WriteAttributeString("cx", Number(pitch / 2));
        xml.WriteAttributeString("cy", Number(pitch / 2));
        xml.WriteAttributeString("r", Number(DotRadius * c.Unit));
        xml.WriteAttributeString("fill", c.PageColor);
        xml.WriteAttributeString("fill-opacity", "0.6");
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    /// <summary>
    /// A starfield in the text colour: points of light of different sizes and brightness, and a
    /// few four-pointed sparkles, at places from a fixed seed so every render is the same.
    /// </summary>
    private static void WriteStars(XmlWriter xml, Context c)
    {
        var tile = StarTile * c.Unit;
        var random = new Random(7);
        StartPattern(xml, tile);
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("fill", c.TextColor);
        for (var i = 0; i < StarCount; i++)
        {
            xml.WriteStartElement("circle", Namespace);
            xml.WriteAttributeString("cx", Number(random.NextDouble() * tile));
            xml.WriteAttributeString("cy", Number(random.NextDouble() * tile));
            xml.WriteAttributeString("r", Number((0.4 + random.NextDouble()) * c.Unit));
            xml.WriteAttributeString("fill-opacity", Number(Math.Round(0.25 + (random.NextDouble() * 0.65), 2)));
            xml.WriteEndElement();
        }

        // A sparkle: four points, each a narrow kite out from the centre.
        for (var i = 0; i < SparkleCount; i++)
        {
            var (x, y, r) = (random.NextDouble() * tile, random.NextDouble() * tile, (3 + (random.NextDouble() * 3)) * c.Unit);
            var w = r * 0.22;
            xml.WriteStartElement("path", Namespace);
            xml.WriteAttributeString("d",
                $"M{Number(x)} {Number(y - r)}L{Number(x + w)} {Number(y - w)}L{Number(x + r)} {Number(y)}L{Number(x + w)} {Number(y + w)}" +
                $"L{Number(x)} {Number(y + r)}L{Number(x - w)} {Number(y + w)}L{Number(x - r)} {Number(y)}L{Number(x - w)} {Number(y - w)}Z");
            xml.WriteAttributeString("fill-opacity", "0.85");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    // Opens the backdrop's pattern element, a square tile in canvas units.
    private static void StartPattern(XmlWriter xml, double tile)
    {
        xml.WriteStartElement("pattern", Namespace);
        xml.WriteAttributeString("id", PatternId);
        xml.WriteAttributeString("patternUnits", "userSpaceOnUse");
        xml.WriteAttributeString("width", Number(tile));
        xml.WriteAttributeString("height", Number(tile));
    }

    /// <summary>
    /// Diamonds of small squares in the page colour, fading from the middle out and dithered at
    /// their edges, on a diagonal lattice (one in the middle of each tile and one on its corners).
    /// </summary>
    private static void WriteDiamonds(XmlWriter xml, Context c)
    {
        var (pitch, square, tile) = (PatternPitch * c.Unit, PatternSquare * c.Unit, PatternTile * c.Unit);

        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("id", DiamondId);
        xml.WriteAttributeString("fill", c.PageColor);
        for (var ring = 0; ring <= PatternRadius; ring++)
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("fill-opacity", Number(0.55 * (1 - (ring / (PatternRadius + 1.0)))));
            for (var i = -ring; i <= ring; i++)
            {
                foreach (var j in Math.Abs(i) == ring ? new[] { 0 } : new[] { -(ring - Math.Abs(i)), ring - Math.Abs(i) })
                {
                    // The outer rings are a checkerboard, so the diamond's edge breaks up into pixels.
                    if (ring > 2 && (i + j) % 2 != 0)
                    {
                        continue;
                    }

                    NewsletterImages.Rect(xml, new RectD((i * pitch) - (square / 2), (j * pitch) - (square / 2), square, square), 0);
                }
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();

        StartPattern(xml, tile);
        foreach (var (x, y) in new[] { (0.5, 0.5), (0.0, 0.0), (1.0, 0.0), (0.0, 1.0), (1.0, 1.0) })
        {
            xml.WriteStartElement("use", Namespace);
            xml.WriteAttributeString("href", $"#{DiamondId}");
            xml.WriteAttributeString("x", Number(x * tile));
            xml.WriteAttributeString("y", Number(y * tile));
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }
}
