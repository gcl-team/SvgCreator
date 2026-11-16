using System.Globalization;
using System.Text;
using System.Xml;
using SvgCreator.Imaging;
using SvgCreator.Mapping;
using SvgCreator.Svg;
using static SvgCreator.Composition.NewsletterComposer;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Composition;

/// <summary>
/// Lays a newsletter out as a scrapbook page: the same content as <see cref="NewsletterComposer"/>'s
/// column, stuck onto a spiral-bound sheet on a striped backdrop. Each block is a piece, tilted a
/// little and held on with washi tape, a pin, a clip or a paperclip: the hero and pictures as
/// prints and polaroids, text on lined notes, ribbons cut out word by word like a ransom note, the
/// footer on a torn strip. The pieces are stacked top to bottom, as the column's sections are, so
/// the page is as tall as its content; then doodles are scattered in the gaps between them.
/// </summary>
/// <remarks>
/// Every tilt and nudge comes from a hash of the title (<see cref="Wobble"/>), so the same
/// newsletter is the same page every time. Sizes are in pixels at a 640 px column, as the column's are.
/// </remarks>
internal static class ScrapbookComposer
{
    // Handwriting: faces macOS and Windows ship, then their handwritten CJK faces, so Chinese is
    // handwritten too. None of them is measured; see HandWide.
    internal const string HandFamily = "'Bradley Hand', 'Segoe Print', 'Chalkboard SE', 'Comic Sans MS', 'HanziPen SC', 'Hannotate SC', 'KaiTi', 'STKaiti', cursive";

    private const double HandWide = 1.12;        // handwriting's width for the sans width it's measured as
    private const string Shine = "#ffffff";      // light catching tape, pins and clips, and the backdrop's pale stripes

    private const double TopSpace = 40;
    private const double BottomSpace = 48;
    private const double PieceGap = 22;
    private const double SheetTilt = -0.6;       // degrees
    private const double UnderTilt = 1.4;        // the sheet under it, peeking out
    private const double GridPitch = 22;
    private const double StripeWidth = 26;       // the backdrop's stripes
    private const double RingPitch = 30;         // the spiral binding
    private const double RingX = 15;
    private const double RingHole = 4;
    private const double RingReach = 12;         // how far the wire loops past the sheet's edge

    private const double LogoWidth = 300;
    private const double LogoHeight = 76;
    private const double LabelPadding = 12;      // round the logo, on its label in the band colour
    private const double LabelTilt = 4;          // the label's tilt, against the title's
    private const double TitleSize = 34;
    private const double TitleGap = 6;
    private const double SquiggleSpace = 14;     // under the title, for its wavy underline
    private const double HeaderTilt = -2.5;

    private const double HeroShare = 0.94;       // of the content's width
    private const double HeroBorder = 10;
    private const double HeroTilt = 2.2;
    private const double FastenerSpace = 14;     // above a piece, for the tape or pin over its top

    private const double TapeLength = 78;
    private const double TapeWidth = 22;
    private const double TapeTooth = 2.5;        // how deep its torn ends are cut
    private const double PinRadius = 7.5;
    private const double ClipWidth = 32;

    private const double NoteShare = 0.8;        // of the content's width, at most
    private const double NoteSize = 23;
    private const double NoteLineHeight = 1.55;
    private const double NotePadding = 22;
    private const double NoteMargin = 34;        // the margin rule, in from the left
    private const double NoteTilt = -3;

    private const double WordSize = 34;
    private const double WordPadding = 9;
    private const double WordGap = 7;
    private const double WordTilt = 9;           // each cut-out word, either way, at most
    private const double WordCut = 1.6;          // how far a scrap's corners stray from square

    private const double PolaroidShare = 0.54;
    private const double PolaroidBorder = 10;
    private const double CaptionSize = 19;
    private const double CaptionSpace = 22;      // round the caption in the polaroid's strip
    private const double CardTilt = 4.5;
    private const double CardRowGap = 4;
    private const double CardNoteSize = 19;
    private const double CardNotePadding = 16;
    private const double CardNoteOverlap = 12;   // how far a note lies over its polaroid
    private const double TornTooth = 7;          // the width of a torn edge's teeth
    private const double TornDepth = 3.5;

    private const double SnapshotShare = 0.86;
    private const double SnapshotBorder = 12;

    private const double FooterShare = 0.9;
    private const double FooterPadding = 18;
    private const double FooterLinkSize = 17;
    private const double FooterTextSize = 15;
    private const double FooterGap = 10;
    private const double SocialSize = 34;

    private const double DoodleSpacing = 55;     // one doodle per this much of the page's height
    private const double DoodleMin = 10;          // a doodle's radius
    private const double DoodleMax = 19;
    private const double DoodleRoom = 5;         // between a doodle and the pieces
    private const int DoodleTries = 800;
    private const int DoodleKinds = 8;

    // Between footer links: a middle dot with an en space each side.
    private const string Separator = " · ";

    private const string StripesId = "scrapbook-stripes";
    private const string GridId = "scrapbook-grid";
    private const string TapeId = "scrapbook-tape";

    /// <summary>
    /// A piece stuck on the page: the gap above it, its height, how to write it with its top at a
    /// given y, and the boxes doodles keep out of, measured from its top.
    /// </summary>
    private sealed record Piece(double Before, double Height, Action<XmlWriter, double> Write, IReadOnlyList<RectD> Keep);

    /// <summary>
    /// The page's tilts and nudges: each one from -1 to 1, from a hash of the title and a count,
    /// so the same newsletter is laid out the same way every time.
    /// </summary>
    private sealed class Wobble(string seed)
    {
        private readonly uint seed = Fnv(seed);
        private uint count;

        public double Next()
        {
            unchecked
            {
                var h = seed ^ (++count * 0x9E3779B9u);
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h / (double)uint.MaxValue * 2) - 1;
            }
        }

        // string.GetHashCode changes from run to run, so FNV-1a.
        private static uint Fnv(string text)
        {
            unchecked
            {
                var h = 2166136261u;
                foreach (var ch in text)
                {
                    h = (h ^ ch) * 16777619u;
                }

                return h;
            }
        }
    }

    public static string Compose(Newsletter newsletter, NewsletterOptions options, NewsletterTheme theme, IReadOnlyDictionary<string, EncodedImage> images, CancellationToken cancellationToken)
    {
        var c = new Context(options, theme, options.Width / ReferenceWidth, new NewsletterImages(images, options.ImageDensity), cancellationToken);
        var w = new Wobble(newsletter.Header.Title);

        var pieces = new List<Piece> { Header(c, w, newsletter.Header) };
        if (newsletter.Hero is { } hero)
        {
            pieces.Add(Hero(c, w, hero));
        }

        if (newsletter.Intro.Count > 0)
        {
            pieces.Add(Note(c, w, newsletter.Intro, NewsletterAlignment.Center, "scrapbook-intro"));
        }

        foreach (var block in newsletter.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pieces.AddRange(block switch
            {
                NewsletterRibbon ribbon => [Ransom(c, w, ribbon)],
                NewsletterText text => [Note(c, w, text.Text, text.Align, "scrapbook-text")],
                NewsletterCards cards => Polaroids(c, w, cards),
                NewsletterImage image => [Snapshot(c, w, image)],
                NewsletterButton button => [Stuck(c, w, Button(c, button), "scrapbook-button", backing: false)],
                NewsletterStats stats => [Stuck(c, w, Stats(c, stats), "scrapbook-stats", backing: false)],
                NewsletterCallout callout => [Stuck(c, w, Callout(c, callout), "scrapbook-callout", backing: true)],
                NewsletterMap map => [Stuck(c, w, Map(c, map), "scrapbook-map", backing: true)],
                _ => throw new ArgumentException($"Unknown newsletter block {block.GetType().Name}.", nameof(newsletter)),
            });
        }

        pieces.Add(Footer(c, w, newsletter.Footer));

        // Stacked top to bottom, as the column's sections are; the doodles go where they aren't.
        var u = c.Unit;
        var tops = new double[pieces.Count];
        var keep = new List<RectD>();
        var y = TopSpace * u;
        for (var i = 0; i < pieces.Count; i++)
        {
            y += i == 0 ? 0 : pieces[i].Before * u;
            tops[i] = y;
            keep.AddRange(pieces[i].Keep.Select(k => k with { Y = k.Y + tops[i] }));
            y += pieces[i].Height;
        }

        var pageHeight = (int)Math.Ceiling(y + (BottomSpace * u));
        var doodles = Doodles(c, w, keep, pageHeight);

        var margin = options.Margin;
        var (width, height) = (options.Width + (2 * margin), pageHeight + (2 * margin));
        return SvgWriter.WriteDocument(width, height, c.TextColor, SvgStyle.Clean, (xml, _) =>
        {
            xml.WriteElementString("title", Namespace, AccentText.Plain(newsletter.Header.Title));

            xml.WriteStartElement("defs", Namespace);
            NewsletterImages.WriteTint(xml, theme);
            WritePatterns(xml, c);
            c.Images.WriteDefinitions(xml);
            xml.WriteEndElement();

            if (margin > 0)
            {
                xml.WriteStartElement("g", Namespace);
                xml.WriteAttributeString("class", "scrapbook-backdrop");
                NewsletterImages.Rect(xml, new RectD(0, 0, width, height), 0, close: false);
                xml.WriteAttributeString("fill", c.BackdropColor);
                xml.WriteEndElement();
                NewsletterImages.Rect(xml, new RectD(0, 0, width, height), 0, close: false);
                xml.WriteAttributeString("fill", $"url(#{StripesId})");
                xml.WriteEndElement();
                xml.WriteEndElement();
            }

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "scrapbook-page");
            if (margin > 0)
            {
                xml.WriteAttributeString("transform", $"translate({Integer(margin)} {Integer(margin)})");
            }

            xml.WriteAttributeString("font-family", c.FontFamily);
            xml.WriteAttributeString("fill", c.TextColor);
            WriteSheets(xml, c, pageHeight);
            WriteDoodles(xml, c, doodles);
            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i].Write(xml, tops[i]);
            }

            xml.WriteEndElement();
        });
    }

    // The logo with the title handwritten under it and a wavy line under that, tilted together.
    private static Piece Header(Context c, Wobble w, NewsletterHeader header)
    {
        var u = c.Unit;
        var logo = header.Logo;
        var title = logo is null || !header.HideTitle ? HandLines(c, [header.Title], TitleSize * u, c.Content * 0.86, 1.15, balance: true) : null;

        // The logo on a label in the band colour, as it sits on the column's band, so a logo
        // made for that band stands out here too.
        var (logoBox, label) = (default(RectD), default(RectD));
        var titleTop = 0.0;
        if (logo is not null)
        {
            var (lw, lh) = c.Images.Size(logo);
            var padding = LabelPadding * u;
            var fit = Math.Min((Math.Min(LogoWidth * u, c.Content) - (2 * padding)) / lw, LogoHeight * u / lh);
            logoBox = new RectD((c.Width - (lw * fit)) / 2, padding, lw * fit, lh * fit);
            label = logoBox.Inflate(padding) with { X = logoBox.X - (1.5 * padding), Width = logoBox.Width + (3 * padding) };
            c.Images.Need(logo, logoBox.Width, logoBox.Height, cover: false);
            titleTop = label.Bottom + (TitleGap * u);
        }

        var titleWidth = title is null ? 0 : Math.Min(title.Width * HandWide, c.Content);
        var titleBox = title is null ? default : new RectD((c.Width - titleWidth) / 2, titleTop, titleWidth, title.Height + (SquiggleSpace * u));
        var tilt = HeaderTilt + (w.Next() * 0.8);
        var boxes = new[] { Turned(label, tilt + LabelTilt), Turned(titleBox, tilt) }.Where(b => b.Width > 0).ToList();
        var (height, shift) = Extent(boxes, logo is null ? 0 : FastenerSpace * u);

        return new Piece(0, height, (xml, top) =>
        {
            var t = top + shift;
            var whole = logo is null ? titleBox : title is null ? label : new RectD(c.Padding, 0, c.Content, titleBox.Bottom);
            StartTilt(xml, "scrapbook-header", tilt, whole.Center.X, t + whole.Center.Y);
            if (logo is not null)
            {
                var l = label with { Y = t + label.Y };
                StartTilt(xml, "scrapbook-logo", LabelTilt, l.Center.X, l.Center.Y);
                Shadow(xml, c, l);
                NewsletterImages.Rect(xml, l, 8 * u, close: false);
                xml.WriteAttributeString("fill", c.BandColor);
                xml.WriteEndElement();
                c.Images.Draw(xml, logo, logoBox with { Y = t + logoBox.Y }, cover: false);
                Tape(xml, c, l.X + (10 * u), l.Y + (8 * u), TapeLength * u * 0.7, -40, c.AccentColor);
                xml.WriteEndElement();
            }

            if (title is not null)
            {
                HandText(xml, c, title, c.Width / 2, t + titleTop, "middle", "scrapbook-title", bold: true);
                var line = Math.Min(titleWidth * 0.8, c.Content * 0.7);
                Squiggle(xml, (c.Width - line) / 2, t + titleTop + title.Height + (4 * u), line, 2.6 * u, c.AccentColor, 2.5 * u);
            }

            xml.WriteEndElement();
        }, Down(boxes, shift));
    }

    // The hero as a print, its border in the page colour, tilted, taped at its top corners.
    private static Piece Hero(Context c, Wobble w, NewsletterHero hero)
    {
        var u = c.Unit;
        var (border, frameWidth) = (HeroBorder * u, c.Content * HeroShare);
        var pictureWidth = frameWidth - (2 * border);
        var (iw, ih) = c.Images.Size(hero.Image);
        var pictureHeight = pictureWidth * Math.Clamp(ih / iw, 0.4, 0.8);
        c.Images.Need(hero.Image, pictureWidth, pictureHeight, cover: true, photo: true);

        var frame = new RectD(((c.Width - frameWidth) / 2) + (w.Next() * 6 * u), 0, frameWidth, pictureHeight + (2 * border));
        var tilt = HeroTilt + (w.Next() * 0.6);
        var turned = Turned(frame, tilt);
        var (height, shift) = Extent([turned], FastenerSpace * u);

        return new Piece(PieceGap, height, (xml, top) =>
        {
            var f = frame with { Y = top + shift };
            StartTilt(xml, "scrapbook-hero", tilt, f.Center.X, f.Center.Y);
            Print(xml, c, f, border, border, hero.Image, hero.Tint);
            Tape(xml, c, f.X + (22 * u), f.Y + (4 * u), TapeLength * u, -38, c.AccentColor);
            Tape(xml, c, f.Right - (22 * u), f.Y + (4 * u), TapeLength * u, 38, c.ButtonColor);
            xml.WriteEndElement();
        }, Down([turned], shift));
    }

    // Paragraphs handwritten on lined paper with a margin rule, a paperclip over its top.
    private static Piece Note(Context c, Wobble w, IReadOnlyList<string> paragraphs, NewsletterAlignment align, string className)
    {
        var u = c.Unit;
        var (padding, margin) = (NotePadding * u, NoteMargin * u);
        var most = c.Content * NoteShare;
        var text = HandLines(c, paragraphs, NoteSize * u, most - margin - padding, NoteLineHeight, balance: align == NewsletterAlignment.Center);
        var width = Math.Min(most, Math.Max(c.Content * 0.5, (text.Width * HandWide) + margin + padding));
        var note = new RectD(((c.Width - width) / 2) + (w.Next() * c.Content * 0.06), 0, width, text.Height + (2 * padding));
        var tilt = NoteTilt + (w.Next() * 1.2);
        var turned = Turned(note, tilt);
        var (height, shift) = Extent([turned], FastenerSpace * u);

        return new Piece(PieceGap, height, (xml, top) =>
        {
            var n = note with { Y = top + shift };
            StartTilt(xml, className, tilt, n.Center.X, n.Center.Y);
            Shadow(xml, c, n);
            NewsletterImages.Rect(xml, n, 2 * u, close: false);
            xml.WriteAttributeString("fill", c.PanelColor);
            xml.WriteEndElement();

            // A rule under each line, at the text's baselines, and the margin's rule down the left.
            var textTop = n.Y + padding;
            var baseline = ((text.LineHeight - text.Size) / 2) + (Ascent * text.Size) + (0.25 * text.Size);
            var rules = new StringBuilder();
            for (var y = textTop + baseline; y < n.Bottom - (4 * u); y += text.LineHeight)
            {
                rules.Append(CultureInfo.InvariantCulture, $"M{Number(n.X + (6 * u))} {Number(y)}H{Number(n.Right - (6 * u))}");
            }

            rules.Append(CultureInfo.InvariantCulture, $"M{Number(n.X + (margin * 0.7))} {Number(n.Y)}V{Number(n.Bottom)}");
            Stroke(xml, rules.ToString(), c.AccentColor, 1 * u, 0.4);

            var x = align == NewsletterAlignment.Left ? n.X + margin : n.X + ((margin + width - padding) / 2);
            HandText(xml, c, text, x, textTop, align == NewsletterAlignment.Left ? "start" : "middle", $"{className}-lines");
            Paperclip(xml, c, n.Right - (40 * u), n.Y - (12 * u), 6);
            xml.WriteEndElement();
        }, Down([turned], shift));
    }

    // A heading cut out word by word, each word on its own scrap in its own face and colours,
    // tilted its own way, like a ransom note.
    private static Piece Ransom(Context c, Wobble w, NewsletterRibbon ribbon)
    {
        var u = c.Unit;
        var (plain, accent) = AccentText.Parse(ribbon.Text);
        var words = new List<(string Text, bool Accent)>();
        var start = 0;
        foreach (var part in plain.Split(' '))
        {
            if (part.Length > 0)
            {
                words.Add((part, accent.AsSpan(start, part.Length).Contains(true)));
            }

            start += part.Length + 1;
        }

        (string Paper, string Ink)[] papers = [(c.AccentColor, c.LabelColor), (c.PageColor, c.TextColor), (c.ButtonColor, c.LabelColor), (c.TextColor, c.PageColor)];
        (string Family, FontMetrics Metrics, double Wide)[] faces = [(HandFamily, FontMetrics.Sans, HandWide), (SerifFamily, FontMetrics.Serif, 1), (SansFamily, FontMetrics.Sans, 1)];
        var (padding, gap, height) = (WordPadding * u, WordGap * u, WordSize * u * 1.45);

        // Each scrap's size, a word too wide for the content set smaller; then rows, filled greedily.
        var scraps = words.Select((word, i) =>
        {
            var face = faces[i % faces.Length];
            var size = WordSize * u;
            var textWidth = face.Metrics.Width(word.Text, size) * face.Wide * 1.06; // bold
            if (textWidth + (2 * padding) > c.Content)
            {
                size *= (c.Content - (2 * padding)) / textWidth;
                textWidth = c.Content - (2 * padding);
            }

            var paper = word.Accent ? (c.HighlightColor, c.PageColor) : papers[i % papers.Length];
            double[] cut = [.. Enumerable.Range(0, 8).Select(_ => w.Next() * WordCut * u)];
            return (Word: word.Text, Face: face.Family, Size: size, Width: textWidth + (2 * padding), Paper: paper, Tilt: w.Next() * WordTilt, Drop: w.Next() * 3 * u, Cut: cut);
        }).ToList();

        var rows = new List<List<int>>();
        for (var i = 0; i < scraps.Count; i++)
        {
            if (rows.Count == 0 || rows[^1].Sum(j => scraps[j].Width + gap) + scraps[i].Width > c.Content * 0.9)
            {
                rows.Add([]);
            }

            rows[^1].Add(i);
        }

        // Where each scrap goes, rows centred.
        var boxes = new RectD[scraps.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            var x = (c.Width - (rows[r].Sum(j => scraps[j].Width) + (gap * (rows[r].Count - 1)))) / 2;
            foreach (var j in rows[r])
            {
                boxes[j] = new RectD(x, (r * (height + gap)) + scraps[j].Drop, scraps[j].Width, height);
                x += scraps[j].Width + gap;
            }
        }

        var turned = boxes.Select((b, j) => Turned(b, scraps[j].Tilt)).ToList();
        var (pieceHeight, shift) = Extent(turned, 0);

        return new Piece(PieceGap, pieceHeight, (xml, top) =>
        {
            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("class", "scrapbook-ribbon");
            for (var j = 0; j < scraps.Count; j++)
            {
                var s = scraps[j];
                var b = boxes[j] with { Y = boxes[j].Y + top + shift };
                StartTilt(xml, "scrapbook-word", s.Tilt, b.Center.X, b.Center.Y);
                var outline = $"M{P(b.X + s.Cut[0], b.Y + s.Cut[1])}L{P(b.Right + s.Cut[2], b.Y + s.Cut[3])}L{P(b.Right + s.Cut[4], b.Bottom + s.Cut[5])}L{P(b.X + s.Cut[6], b.Bottom + s.Cut[7])}Z";
                Fill(xml, outline, c.TextColor, 0.18, $"translate({Number(1.5 * u)} {Number(2.5 * u)})");
                Fill(xml, outline, s.Paper.Item1);

                xml.WriteStartElement("text", Namespace);
                xml.WriteAttributeString("x", Number(b.Center.X));
                xml.WriteAttributeString("y", Number(b.Y + ((height - s.Size) / 2) + (Ascent * s.Size)));
                xml.WriteAttributeString("font-family", s.Face);
                xml.WriteAttributeString("font-size", Number(s.Size));
                xml.WriteAttributeString("font-weight", "bold");
                xml.WriteAttributeString("text-anchor", "middle");
                xml.WriteAttributeString("fill", s.Paper.Item2);
                xml.WriteString(s.Word);
                xml.WriteEndElement();
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }, Down(turned, shift));
    }

    // A polaroid per card, its title handwritten in the strip under the picture, and its text on
    // a torn note lying over its side, the sides swapping each row as the column's cards do. Each
    // card is its own piece, so the rows can sit closer than other pieces.
    private static List<Piece> Polaroids(Context c, Wobble w, NewsletterCards cards)
    {
        var u = c.Unit;
        var border = PolaroidBorder * u;
        var width = c.Content * PolaroidShare;
        var pictureWidth = width - (2 * border);
        var pictureHeight = pictureWidth * 9 / 16;
        var pieces = new List<Piece>();
        for (var i = 0; i < cards.Items.Count; i++)
        {
            var card = cards.Items[i];
            c.Images.Need(card.Image, pictureWidth, pictureHeight, cover: true, photo: true);
            var caption = HandLines(c, [card.Title], CaptionSize * u, pictureWidth, 1.2, balance: true);
            var strip = caption.Height + (CaptionSpace * u);
            var left = i % 2 == 0;
            var polaroid = new RectD(left ? c.Padding + (4 * u) : c.Width - c.Padding - width - (4 * u), 0, width, border + pictureHeight + strip);
            var sign = left ? -1 : 1;
            var tilt = sign * (CardTilt + (w.Next() * 1.2));
            var boxes = new List<RectD> { Turned(polaroid, tilt) };

            Text? body = null;
            var (note, noteTilt, torn) = (default(RectD), 0.0, Array.Empty<double>());
            if (card.Text.Count > 0)
            {
                var padding = CardNotePadding * u;
                var noteX = left ? polaroid.Right - (CardNoteOverlap * u) : c.Padding;
                var noteWidth = left ? c.Width - c.Padding - noteX : polaroid.X + (CardNoteOverlap * u) - c.Padding;
                body = HandLines(c, card.Text, CardNoteSize * u, noteWidth - (2 * padding), 1.45, ParagraphGap * u);
                var noteHeight = body.Height + (2 * padding) + (TornDepth * u);
                note = new RectD(noteX, ((polaroid.Height - noteHeight) / 2) + (w.Next() * 12 * u), noteWidth, noteHeight);
                noteTilt = -sign * (1.6 + (w.Next() * 0.8));
                torn = [.. Enumerable.Range(0, (int)Math.Ceiling(noteWidth / (TornTooth * u)) + 1).Select(_ => (0.5 + (w.Next() * 0.5)) * TornDepth * u)];
                boxes.Add(Turned(note, noteTilt));
            }

            var (height, shift) = Extent(boxes, FastenerSpace * u);
            var fastener = i % 3;
            pieces.Add(new Piece(i == 0 ? PieceGap : CardRowGap, height, (xml, top) =>
            {
                var p = polaroid with { Y = top + shift };
                OpenLink(xml, card.Link);
                xml.WriteStartElement("g", Namespace);
                xml.WriteAttributeString("class", "scrapbook-card");
                StartTilt(xml, "scrapbook-polaroid", tilt, p.Center.X, p.Center.Y);
                Print(xml, c, p, border, strip, card.Image, card.Tint);
                HandText(xml, c, caption, p.Center.X, p.Bottom - strip + ((strip - caption.Height) / 2), "middle", "scrapbook-caption", bold: true);
                switch (fastener)
                {
                    case 0:
                        Pin(xml, c, p.Center.X, p.Y + (6 * u), c.AccentColor);
                        break;
                    case 1:
                        Tape(xml, c, p.Center.X, p.Y, TapeLength * u * 0.8, sign * 4, c.ButtonColor);
                        break;
                    default:
                        BinderClip(xml, c, p.Center.X, p.Y, c.AccentColor);
                        break;
                }

                xml.WriteEndElement();

                if (body is not null)
                {
                    var n = note with { Y = note.Y + top + shift };
                    StartTilt(xml, "scrapbook-card-note", noteTilt, n.Center.X, n.Center.Y);
                    var outline = Torn(n, torn, TornTooth * u);
                    Fill(xml, outline, c.TextColor, 0.12, $"translate({Number(2 * u)} {Number(3 * u)})");
                    Fill(xml, outline, i % 2 == 0 ? c.PageColor : c.PanelColor);
                    HandText(xml, c, body, n.X + (CardNotePadding * u), n.Y + (CardNotePadding * u), "start", "scrapbook-card-text");
                    Tape(xml, c, n.Center.X, n.Y, TapeLength * u * 0.6, -sign * 3, c.AccentColor);
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                CloseLink(xml, card.Link);
            }, Down(boxes, shift)));
        }

        return pieces;
    }

    // A picture as a print with its caption handwritten under it, taped at the top.
    private static Piece Snapshot(Context c, Wobble w, NewsletterImage image)
    {
        var u = c.Unit;
        var (border, width) = (SnapshotBorder * u, c.Content * SnapshotShare);
        var pictureWidth = width - (2 * border);
        var (iw, ih) = c.Images.Size(image.Image);
        var pictureHeight = pictureWidth * Math.Clamp(ih / iw, 0.3, 1.25);
        c.Images.Need(image.Image, pictureWidth, pictureHeight, cover: true, photo: true);
        var caption = image.Caption is { } line ? HandLines(c, [line], CaptionSize * u, pictureWidth, 1.2, balance: true) : null;
        var strip = caption is null ? border : caption.Height + (CaptionSpace * u);
        var frame = new RectD(((c.Width - width) / 2) + (w.Next() * 10 * u), 0, width, border + pictureHeight + strip);
        var tilt = w.Next() * 3;
        var turned = Turned(frame, tilt);
        var (height, shift) = Extent([turned], FastenerSpace * u);

        return new Piece(PieceGap, height, (xml, top) =>
        {
            var f = frame with { Y = top + shift };
            OpenLink(xml, image.Link);
            StartTilt(xml, "scrapbook-image", tilt, f.Center.X, f.Center.Y);
            Print(xml, c, f, border, strip, image.Image, image.Tint);
            if (caption is not null)
            {
                HandText(xml, c, caption, f.Center.X, f.Bottom - strip + ((strip - caption.Height) / 2), "middle", "scrapbook-caption");
            }

            Tape(xml, c, f.Center.X, f.Y, TapeLength * u, -3, c.AccentColor);
            xml.WriteEndElement();
            CloseLink(xml, image.Link);
        }, Down([turned], shift));
    }

    // A block the scrapbook draws as the column does, stuck on at a slant: on its own (buttons,
    // stats) or on a card in the page colour taped to the page (callouts, maps).
    private static Piece Stuck(Context c, Wobble w, Section section, string className, bool backing)
    {
        var u = c.Unit;
        var padding = backing ? 12 * u : 0;
        var box = new RectD(c.Padding - padding, 0, c.Content + (2 * padding), section.Height + (2 * padding));
        var tilt = w.Next() * (backing ? 1.5 : 3);
        var turned = Turned(box, tilt);
        var (height, shift) = Extent([turned], backing ? FastenerSpace * u : 0);

        return new Piece(PieceGap, height, (xml, top) =>
        {
            var b = box with { Y = top + shift };
            StartTilt(xml, className, tilt, b.Center.X, b.Center.Y);
            if (backing)
            {
                Shadow(xml, c, b);
                NewsletterImages.Rect(xml, b, 2 * u, close: false);
                xml.WriteAttributeString("fill", c.PageColor);
                xml.WriteEndElement();
            }

            section.Write(xml, b.Y + padding);
            if (backing)
            {
                Tape(xml, c, b.X + (30 * u), b.Y + (2 * u), TapeLength * u * 0.8, -30, c.ButtonColor);
                Tape(xml, c, b.Right - (30 * u), b.Bottom - (2 * u), TapeLength * u * 0.8, -30, c.AccentColor);
            }

            xml.WriteEndElement();
        }, Down([turned], shift));
    }

    // A torn strip at the bottom: the links handwritten and underlined, the icons, then the lines.
    private static Piece Footer(Context c, Wobble w, NewsletterFooter footer)
    {
        var u = c.Unit;
        var (padding, gap) = (FooterPadding * u, FooterGap * u);
        var width = c.Content * FooterShare;
        var room = width - (2 * padding);

        // The links on one line, smaller if they need to be.
        var linkSize = FooterLinkSize * u;
        var linksWidth = footer.Links.Count == 0 ? 0 : HandWide * c.Metrics.Width(string.Join(Separator, footer.Links.Select(l => l.Text)), linkSize);
        linkSize *= linksWidth > room ? room / linksWidth : 1;
        var linksHeight = footer.Links.Count == 0 ? 0 : linkSize * 1.4;
        var socialHeight = footer.Social.Count == 0 ? 0 : SocialSize * u;
        var lines = footer.Lines.Count > 0 ? HandLines(c, footer.Lines, FooterTextSize * u, room, 1.5, balance: true) : null;
        var parts = new[] { linksHeight, socialHeight, lines?.Height ?? 0 }.Where(h => h > 0).ToList();
        var stripHeight = parts.Sum() + (gap * (parts.Count - 1)) + (2 * padding);

        var strip = new RectD((c.Width - width) / 2, 0, width, stripHeight);
        var tilt = -1.2 + (w.Next() * 0.6);
        var torn = Enumerable.Range(0, (2 * ((int)Math.Ceiling(width / (TornTooth * u)) + 1))).Select(_ => (0.5 + (w.Next() * 0.5)) * TornDepth * u).ToArray();
        var turned = Turned(strip, tilt);
        var (height, shift) = Extent([turned], FastenerSpace * u);

        return new Piece(PieceGap + 8, height, (xml, top) =>
        {
            var s = strip with { Y = top + shift };
            StartTilt(xml, "scrapbook-footer", tilt, s.Center.X, s.Center.Y);
            var outline = Torn(s, torn, TornTooth * u, both: true);
            Fill(xml, outline, c.TextColor, 0.12, $"translate({Number(2 * u)} {Number(3 * u)})");
            Fill(xml, outline, c.PanelColor);
            Tape(xml, c, s.X + (8 * u), s.Y + (6 * u), TapeLength * u * 0.7, -35, c.ButtonColor);
            Tape(xml, c, s.Right - (8 * u), s.Y + (6 * u), TapeLength * u * 0.7, 35, c.AccentColor);

            xml.WriteStartElement("g", Namespace);
            xml.WriteAttributeString("font-family", HandFamily);
            var y = s.Y + padding;
            if (footer.Links.Count > 0)
            {
                xml.WriteStartElement("text", Namespace);
                xml.WriteAttributeString("class", "scrapbook-footer-links");
                xml.WriteAttributeString("x", Number(c.Width / 2));
                xml.WriteAttributeString("y", Number(y + ((linksHeight - linkSize) / 2) + (Ascent * linkSize)));
                xml.WriteAttributeString("font-size", Number(linkSize));
                xml.WriteAttributeString("font-weight", "bold");
                xml.WriteAttributeString("text-anchor", "middle");
                for (var i = 0; i < footer.Links.Count; i++)
                {
                    if (i > 0)
                    {
                        xml.WriteElementString("tspan", Namespace, Separator);
                    }

                    xml.WriteStartElement("a", Namespace);
                    xml.WriteAttributeString("href", footer.Links[i].Url);
                    xml.WriteStartElement("tspan", Namespace);
                    xml.WriteAttributeString("text-decoration", "underline");
                    xml.WriteString(footer.Links[i].Text);
                    xml.WriteEndElement();
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                y += linksHeight + gap;
            }

            if (footer.Social.Count > 0)
            {
                WriteSocial(xml, c, footer.Social, y, socialHeight);
                y += socialHeight + gap;
            }

            if (lines is not null)
            {
                WriteText(xml, c, lines, c.Width / 2, y, "middle", "scrapbook-footer-text", muted: true);
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
        }, Down([turned], shift));
    }

    // The sheet under the page, turned the other way, then the page: a grid, and a spiral
    // binding down its left edge, its wire looping out over the edge from each punched hole.
    private static void WriteSheets(XmlWriter xml, Context c, double height)
    {
        var u = c.Unit;
        var sheet = new RectD(0, 0, c.Width, height);
        StartTilt(xml, "scrapbook-sheet-under", UnderTilt, sheet.Center.X, sheet.Center.Y);
        Shadow(xml, c, sheet);
        NewsletterImages.Rect(xml, sheet, 0, close: false);
        xml.WriteAttributeString("fill", c.PanelColor);
        xml.WriteEndElement();
        xml.WriteEndElement();

        StartTilt(xml, "scrapbook-sheet", SheetTilt, sheet.Center.X, sheet.Center.Y);
        Shadow(xml, c, sheet);
        NewsletterImages.Rect(xml, sheet, 0, close: false);
        xml.WriteAttributeString("fill", c.PageColor);
        xml.WriteEndElement();
        NewsletterImages.Rect(xml, sheet, 0, close: false);
        xml.WriteAttributeString("fill", $"url(#{GridId})");
        xml.WriteEndElement();

        var (x, reach, pitch) = (RingX * u, RingReach * u, RingPitch * u);
        var wire = new StringBuilder();
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "scrapbook-holes");
        xml.WriteAttributeString("fill", c.TextColor);
        xml.WriteAttributeString("fill-opacity", "0.2");
        for (var y = pitch * 0.75; y < height - (pitch / 2); y += pitch)
        {
            Circle(xml, x, y, RingHole * u);
            wire.Append(CultureInfo.InvariantCulture, $"M{P(x, y)}C{P(x, y - (10 * u))} {P(-reach, y - (10 * u))} {P(-reach, y - (2 * u))}");
        }

        xml.WriteEndElement();
        Stroke(xml, wire.ToString(), c.TextColor, 2.6 * u, 0.55);
        xml.WriteEndElement();
    }

    // Stripes on the backdrop, the sheet's grid, and the stripes over washi tape.
    private static void WritePatterns(XmlWriter xml, Context c)
    {
        var u = c.Unit;
        var stripe = StripeWidth * u;
        StartPattern(xml, StripesId, 2 * stripe);
        NewsletterImages.Rect(xml, new RectD(0, 0, stripe, 2 * stripe), 0, close: false);
        xml.WriteAttributeString("fill", Shine);
        xml.WriteAttributeString("fill-opacity", "0.45");
        xml.WriteEndElement();
        xml.WriteEndElement();

        var pitch = GridPitch * u;
        StartPattern(xml, GridId, pitch);
        Stroke(xml, $"M{Number(pitch)} 0V{Number(pitch)}M0 {Number(pitch)}H{Number(pitch)}", c.AccentColor, 1 * u, 0.16);
        xml.WriteEndElement();

        var tile = 8 * u;
        StartPattern(xml, TapeId, tile);
        Stroke(xml, $"M0 {Number(tile)}L{Number(tile)} 0M{Number(-tile / 4)} {Number(tile / 4)}L{Number(tile / 4)} {Number(-tile / 4)}M{Number(tile * 0.75)} {Number(tile * 1.25)}L{Number(tile * 1.25)} {Number(tile * 0.75)}", Shine, 2 * u, 0.4);
        xml.WriteEndElement();
    }

    private static void StartPattern(XmlWriter xml, string id, double tile)
    {
        xml.WriteStartElement("pattern", Namespace);
        xml.WriteAttributeString("id", id);
        xml.WriteAttributeString("patternUnits", "userSpaceOnUse");
        xml.WriteAttributeString("width", Number(tile));
        xml.WriteAttributeString("height", Number(tile));
    }

    /// <summary>
    /// Doodles in the gaps: tries spots across the page, keeping those clear of every piece and
    /// of the other doodles, up to one per <see cref="DoodleSpacing"/> of the page's height.
    /// </summary>
    private static List<(int Kind, double X, double Y, double R, string Color, double Tilt)> Doodles(Context c, Wobble w, List<RectD> keep, double height)
    {
        var u = c.Unit;
        var doodles = new List<(int Kind, double X, double Y, double R, string Color, double Tilt)>();
        var target = (int)(height / (DoodleSpacing * u));
        string[] colors = [c.AccentColor, c.ButtonColor, c.HighlightColor];
        var (left, right) = ((RingX + RingHole + 8) * u, c.Width - (8 * u));
        for (var i = 0; i < DoodleTries && doodles.Count < target; i++)
        {
            var r = (DoodleMin + ((DoodleMax - DoodleMin) * (w.Next() + 1) / 2)) * u;
            var x = left + r + ((right - left - (2 * r)) * (w.Next() + 1) / 2);
            var y = (12 * u) + r + ((height - (24 * u) - (2 * r)) * (w.Next() + 1) / 2);
            var tilt = w.Next() * 20;
            var spot = new RectD(x - r, y - r, 2 * r, 2 * r).Inflate(DoodleRoom * u);
            if (keep.Any(k => k.OverlapArea(spot) > 0) || doodles.Any(d => double.Hypot(d.X - x, d.Y - y) < 2.5 * (d.R + r)))
            {
                continue;
            }

            doodles.Add((doodles.Count % DoodleKinds, x, y, r, colors[doodles.Count % colors.Length], tilt));
        }

        return doodles;
    }

    // Stars, hearts, sparkles, notes, paws, flowers, swirls and dots, each in its colour.
    private static void WriteDoodles(XmlWriter xml, Context c, List<(int Kind, double X, double Y, double R, string Color, double Tilt)> doodles)
    {
        var u = c.Unit;
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "scrapbook-doodles");
        foreach (var (kind, x, y, r, color, tilt) in doodles)
        {
            StartTilt(xml, "scrapbook-doodle", tilt, x, y);
            switch (kind)
            {
                case 0:
                    xml.WriteStartElement("path", Namespace);
                    xml.WriteAttributeString("d", Star(x, y, r));
                    xml.WriteAttributeString("fill", "none");
                    xml.WriteAttributeString("stroke", color);
                    xml.WriteAttributeString("stroke-width", Number(2 * u));
                    xml.WriteAttributeString("stroke-linejoin", "round");
                    xml.WriteEndElement();
                    break;
                case 1:
                    Fill(xml, $"M{P(x, y + (0.8 * r))}C{P(x - (1.2 * r), y - (0.1 * r))} {P(x - (0.6 * r), y - r)} {P(x, y - (0.35 * r))}C{P(x + (0.6 * r), y - r)} {P(x + (1.2 * r), y - (0.1 * r))} {P(x, y + (0.8 * r))}Z", color);
                    break;
                case 2:
                    Fill(xml, Sparkle(x, y, r, 0.22), color);
                    break;
                case 3:
                    // A quaver: a tilted head, a stem and a flag.
                    xml.WriteStartElement("ellipse", Namespace);
                    xml.WriteAttributeString("cx", Number(x - (0.3 * r)));
                    xml.WriteAttributeString("cy", Number(y + (0.6 * r)));
                    xml.WriteAttributeString("rx", Number(0.38 * r));
                    xml.WriteAttributeString("ry", Number(0.28 * r));
                    xml.WriteAttributeString("transform", $"rotate(-20 {Number(x - (0.3 * r))} {Number(y + (0.6 * r))})");
                    xml.WriteAttributeString("fill", color);
                    xml.WriteEndElement();
                    Stroke(xml, $"M{P(x + (0.05 * r), y + (0.55 * r))}V{Number(y - (0.9 * r))}Q{P(x + (0.2 * r), y - (0.3 * r))} {P(x + (0.65 * r), y - (0.2 * r))}", color, 1.8 * u, 1);
                    break;
                case 4:
                    // A paw: a pad and four toes.
                    xml.WriteStartElement("g", Namespace);
                    xml.WriteAttributeString("fill", color);
                    xml.WriteStartElement("ellipse", Namespace);
                    xml.WriteAttributeString("cx", Number(x));
                    xml.WriteAttributeString("cy", Number(y + (0.3 * r)));
                    xml.WriteAttributeString("rx", Number(0.45 * r));
                    xml.WriteAttributeString("ry", Number(0.38 * r));
                    xml.WriteEndElement();
                    foreach (var (dx, dy) in new[] { (-0.55, -0.15), (-0.22, -0.52), (0.22, -0.52), (0.55, -0.15) })
                    {
                        Circle(xml, x + (dx * r), y + (dy * r), 0.18 * r);
                    }

                    xml.WriteEndElement();
                    break;
                case 5:
                    // A flower: five petals round a centre in the page colour.
                    xml.WriteStartElement("g", Namespace);
                    xml.WriteAttributeString("fill", color);
                    for (var k = 0; k < 5; k++)
                    {
                        var angle = (k * 2 * Math.PI / 5) - (Math.PI / 2);
                        Circle(xml, x + (0.5 * r * Math.Cos(angle)), y + (0.5 * r * Math.Sin(angle)), 0.38 * r);
                    }

                    xml.WriteEndElement();
                    Circle(xml, x, y, 0.28 * r, c.PageColor);
                    break;
                case 6:
                    // A swirl: two turns out from the centre.
                    var swirl = new StringBuilder($"M{P(x, y)}");
                    for (var k = 1; k <= 32; k++)
                    {
                        var angle = k * 4 * Math.PI / 32;
                        swirl.Append(CultureInfo.InvariantCulture, $"L{P(x + (r * k / 32 * Math.Cos(angle)), y + (r * k / 32 * Math.Sin(angle)))}");
                    }

                    Stroke(xml, swirl.ToString(), color, 1.8 * u, 1);
                    break;
                default:
                    // Three dots, getting smaller.
                    xml.WriteStartElement("g", Namespace);
                    xml.WriteAttributeString("fill", color);
                    Circle(xml, x - (0.6 * r), y + (0.3 * r), 0.26 * r);
                    Circle(xml, x, y - (0.1 * r), 0.2 * r);
                    Circle(xml, x + (0.5 * r), y - (0.45 * r), 0.14 * r);
                    xml.WriteEndElement();
                    break;
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    /// <summary>
    /// A print: a soft shadow, a border in the page colour (white, on a light page), and the photo inside it, with
    /// <paramref name="bottom"/> of border under the photo (a polaroid's deeper strip).
    /// </summary>
    private static void Print(XmlWriter xml, Context c, RectD frame, double border, double bottom, string image, bool? tint)
    {
        Shadow(xml, c, frame);
        NewsletterImages.Rect(xml, frame, 1.5 * c.Unit, close: false);
        xml.WriteAttributeString("fill", c.PageColor);
        xml.WriteAttributeString("stroke", c.TextColor);
        xml.WriteAttributeString("stroke-opacity", "0.08");
        xml.WriteAttributeString("stroke-width", Number(c.Unit));
        xml.WriteEndElement();
        Photo(xml, c, image, new RectD(frame.X + border, frame.Y + border, frame.Width - (2 * border), frame.Height - border - bottom), 0, tint);
    }

    // A shadow under paper: the same box a little down and right, faint.
    private static void Shadow(XmlWriter xml, Context c, RectD box)
    {
        NewsletterImages.Rect(xml, new RectD(box.X + (2 * c.Unit), box.Y + (4 * c.Unit), box.Width, box.Height), 2 * c.Unit, close: false);
        xml.WriteAttributeString("fill", c.TextColor);
        xml.WriteAttributeString("fill-opacity", "0.12");
        xml.WriteEndElement();
    }

    /// <summary>
    /// A strip of washi tape centred on (<paramref name="x"/>, <paramref name="y"/>), turned
    /// <paramref name="degrees"/>: see-through colour with white stripes over it, its ends torn in teeth.
    /// </summary>
    private static void Tape(XmlWriter xml, Context c, double x, double y, double length, double degrees, string color)
    {
        var (l, h, d) = (length / 2, TapeWidth * c.Unit / 2, TapeTooth * c.Unit);
        const int teeth = 4;
        var outline = new StringBuilder($"M{P(-l, -h)}H{Number(l)}");
        for (var k = 1; k <= teeth; k++)
        {
            outline.Append(CultureInfo.InvariantCulture, $"L{P(l - (k % 2 == 1 ? d : 0), -h + (k * 2 * h / teeth))}");
        }

        outline.Append(CultureInfo.InvariantCulture, $"H{Number(-l)}");
        for (var k = 1; k <= teeth; k++)
        {
            outline.Append(CultureInfo.InvariantCulture, $"L{P(-l + (k % 2 == 1 ? d : 0), h - (k * 2 * h / teeth))}");
        }

        outline.Append('Z');
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "scrapbook-tape");
        xml.WriteAttributeString("transform", $"translate({Number(x)} {Number(y)}) rotate({Number(degrees)})");
        Fill(xml, outline.ToString(), color, 0.7);
        Fill(xml, outline.ToString(), $"url(#{TapeId})");
        xml.WriteEndElement();
    }

    // A push pin seen from above: a round head with a shine, its shadow falling down and right.
    private static void Pin(XmlWriter xml, Context c, double x, double y, string color)
    {
        var (u, r) = (c.Unit, PinRadius * c.Unit);
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "scrapbook-pin");
        xml.WriteStartElement("ellipse", Namespace);
        xml.WriteAttributeString("cx", Number(x + (3 * u)));
        xml.WriteAttributeString("cy", Number(y + (5 * u)));
        xml.WriteAttributeString("rx", Number(r * 0.8));
        xml.WriteAttributeString("ry", Number(r * 0.45));
        xml.WriteAttributeString("fill", c.TextColor);
        xml.WriteAttributeString("fill-opacity", "0.2");
        xml.WriteEndElement();
        Circle(xml, x, y, r, color);
        xml.WriteStartElement("circle", Namespace);
        xml.WriteAttributeString("cx", Number(x - (r * 0.32)));
        xml.WriteAttributeString("cy", Number(y - (r * 0.32)));
        xml.WriteAttributeString("r", Number(r * 0.32));
        xml.WriteAttributeString("fill", Shine);
        xml.WriteAttributeString("fill-opacity", "0.75");
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    // A binder clip biting the edge at (x, y): its body over the edge and its two wire handles up.
    private static void BinderClip(XmlWriter xml, Context c, double x, double y, string color)
    {
        var (u, w) = (c.Unit, ClipWidth * c.Unit);
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "scrapbook-clip");
        Stroke(xml, $"M{P(x - (w * 0.3), y - (6 * u))}L{P(x - (w * 0.2), y - (22 * u))}L{P(x + (w * 0.2), y - (22 * u))}L{P(x + (w * 0.3), y - (6 * u))}", c.TextColor, 2 * u, 0.55);
        Fill(xml, $"M{P(x - (w / 2), y + (12 * u))}L{P(x + (w / 2), y + (12 * u))}L{P(x + (w * 0.36), y - (7 * u))}L{P(x - (w * 0.36), y - (7 * u))}Z", color);
        Stroke(xml, $"M{P(x - (w * 0.3), y - (3 * u))}H{Number(x + (w * 0.3))}", Shine, 1.5 * u, 0.5);
        xml.WriteEndElement();
    }

    // A paperclip, its top at (x, y), turned: three bends of wire.
    private static void Paperclip(XmlWriter xml, Context c, double x, double y, double degrees)
    {
        var u = c.Unit;
        string N(double v) => Number(v * u);
        xml.WriteStartElement("path", Namespace);
        xml.WriteAttributeString("class", "scrapbook-paperclip");
        xml.WriteAttributeString("d", $"M{N(3.5)} {N(34)}V{N(11.5)}A{N(3.5)} {N(3.5)} 0 0 1 {N(10.5)} {N(11.5)}V{N(42)}A{N(6)} {N(6)} 0 0 1 {N(-1.5)} {N(42)}V{N(7.5)}A{N(7.5)} {N(7.5)} 0 0 1 {N(13.5)} {N(7.5)}V{N(34)}");
        xml.WriteAttributeString("transform", $"translate({Number(x)} {Number(y)}) rotate({Number(degrees)})");
        xml.WriteAttributeString("fill", "none");
        xml.WriteAttributeString("stroke", c.HighlightColor);
        xml.WriteAttributeString("stroke-width", Number(2.4 * u));
        xml.WriteAttributeString("stroke-linecap", "round");
        xml.WriteEndElement();
    }

    // A wavy hand-drawn line from (x, y), length wide.
    private static void Squiggle(XmlWriter xml, double x, double y, double length, double depth, string color, double width)
    {
        var half = depth * 3;
        var d = new StringBuilder($"M{P(x, y)}q{Number(half / 2)} {Number(-depth)} {Number(half)} 0");
        for (var done = half; done + half <= length; done += half)
        {
            d.Append(CultureInfo.InvariantCulture, $"t{Number(half)} 0");
        }

        Stroke(xml, d.ToString(), color, width, 1);
    }

    /// <summary>
    /// A box's outline with its bottom edge (and with <paramref name="both"/>, its top edge too)
    /// torn: teeth <paramref name="tooth"/> wide, each as deep as the next of <paramref name="depths"/>.
    /// </summary>
    private static string Torn(RectD box, double[] depths, double tooth, bool both = false)
    {
        var d = new StringBuilder($"M{P(box.X, box.Y)}");
        var k = 0;
        if (both)
        {
            for (var x = box.X + tooth; x < box.Right; x += tooth)
            {
                d.Append(CultureInfo.InvariantCulture, $"L{P(x, box.Y + (k++ % 2 == 0 ? depths[k % depths.Length] : 0))}");
            }
        }

        d.Append(CultureInfo.InvariantCulture, $"L{P(box.Right, box.Y)}L{P(box.Right, box.Bottom)}");
        for (var x = box.Right - tooth; x > box.X; x -= tooth)
        {
            d.Append(CultureInfo.InvariantCulture, $"L{P(x, box.Bottom - (k++ % 2 == 0 ? depths[k % depths.Length] : 0))}");
        }

        d.Append(CultureInfo.InvariantCulture, $"L{P(box.X, box.Bottom)}Z");
        return d.ToString();
    }

    // Opens a group turned about (cx, cy); the caller closes it.
    private static void StartTilt(XmlWriter xml, string className, double degrees, double cx, double cy)
    {
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", className);
        xml.WriteAttributeString("transform", $"rotate({Number(degrees)} {Number(cx)} {Number(cy)})");
    }

    private static void Fill(XmlWriter xml, string d, string color, double opacity = 1, string? transform = null)
    {
        xml.WriteStartElement("path", Namespace);
        xml.WriteAttributeString("d", d);
        xml.WriteAttributeString("fill", color);
        if (opacity < 1)
        {
            xml.WriteAttributeString("fill-opacity", Number(opacity));
        }

        if (transform is not null)
        {
            xml.WriteAttributeString("transform", transform);
        }

        xml.WriteEndElement();
    }

    private static void Stroke(XmlWriter xml, string d, string color, double width, double opacity)
    {
        xml.WriteStartElement("path", Namespace);
        xml.WriteAttributeString("d", d);
        xml.WriteAttributeString("fill", "none");
        xml.WriteAttributeString("stroke", color);
        xml.WriteAttributeString("stroke-width", Number(width));
        xml.WriteAttributeString("stroke-linecap", "round");
        if (opacity < 1)
        {
            xml.WriteAttributeString("stroke-opacity", Number(opacity));
        }

        xml.WriteEndElement();
    }

    /// <summary>Paragraphs to be handwritten, wrapped narrower than <paramref name="width"/> by <see cref="HandWide"/>.</summary>
    private static Text HandLines(Context c, IEnumerable<string> paragraphs, double size, double width, double lineHeight, double paragraphGap = 0, bool balance = false) =>
        Lay(c, paragraphs, size, width / HandWide, paragraphGap, lineHeight, balance);

    private static void HandText(XmlWriter xml, Context c, Text text, double x, double top, string anchor, string className, bool bold = false)
    {
        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("font-family", HandFamily);
        WriteText(xml, c, text, x, top, anchor, className, bold);
        xml.WriteEndElement();
    }

    /// <summary>The box round <paramref name="box"/> turned <paramref name="degrees"/> about its centre.</summary>
    private static RectD Turned(RectD box, double degrees)
    {
        var angle = degrees * Math.PI / 180;
        var (sin, cos) = (Math.Abs(Math.Sin(angle)), Math.Abs(Math.Cos(angle)));
        var (w, h) = ((box.Width * cos) + (box.Height * sin), (box.Width * sin) + (box.Height * cos));
        return new RectD(box.X + ((box.Width - w) / 2), box.Y + ((box.Height - h) / 2), w, h);
    }

    /// <summary>
    /// A piece's height from its turned boxes, with <paramref name="above"/> of room over them for
    /// what fastens it, and how far down to move them so the topmost starts at the piece's top.
    /// </summary>
    private static (double Height, double Shift) Extent(IReadOnlyList<RectD> boxes, double above)
    {
        var top = boxes.Min(b => b.Y) - above;
        return (boxes.Max(b => b.Bottom) - top, -top);
    }

    private static List<RectD> Down(IEnumerable<RectD> boxes, double by) => [.. boxes.Select(b => b with { Y = b.Y + by })];

    private static string P(double x, double y) => $"{Number(x)} {Number(y)}";
}
