# Newsletter

**Goal:** a short, fun newsletter as one tall SVG, in the style of a game studio's update email: a black header band with a logo, a hero picture with a wavy edge, a few lines of intro with highlighted words, pixel-art section ribbons, rows of pictures beside text, red pill buttons, a "redeem code" box, and a footer of links and small print. It's for any kind of newsletter: a trip (with a [travel map](Travel-Map.md) in it), a small business's news, or new posts on a blog.

```sh
svgcreator newsletter trip/trip.json -o trip.svg                  # 640 px column on a 40 px patterned backdrop
svgcreator newsletter trip/trip.json -o print.svg --width 1280     # everything twice the size
svgcreator newsletter trip/trip.json -o plain.svg --margin 0       # the column alone, no backdrop
svgcreator newsletter trip/trip.json -o big.svg --image-density 0  # embed the pictures unchanged
svgcreator newsletter trip/trip.json -o teal.svg --page-color '#0f5257' --accent-color '#ffb347'
```

The file is JSON. Picture paths are relative to the file, so a folder holding `trip.json`, `photos/` and a logo can move anywhere:

```jsonc
{
  "header": { "title": "上海 · Shanghai", "logo": "logo.png" },
  "hero":   { "image": "photos/bund-night.jpg" },
  "intro":  ["嗨，旅行者！我们的[[上海三日游]]回顾来了。", "3 days, 12 stops and far too many [[xiaolongbao]]."],
  "blocks": [
    { "type": "button",  "text": "Full itinerary", "link": "https://example.com/shanghai" },
    { "type": "ribbon",  "text": "Day 1 · 外滩" },
    { "type": "map",     "route": "lines", "stops": [ { "sequence": 1, "name": "外滩 The Bund", "latitude": 31.24, "longitude": 121.49 } ] },
    { "type": "cards",   "items": [
        { "image": "photos/bund.jpg", "title": "外滩 The Bund", "text": "The skyline lights up at 7 pm." },
        { "image": "photos/yuyuan.jpg", "title": "豫园 Yu Garden", "text": ["Go early.", "It fills up by ten."], "link": "https://example.com/yuyuan" } ] },
    { "type": "image",   "image": "photos/xiaolongbao.jpg", "caption": "Worth the 40-minute queue" },
    { "type": "callout", "text": "Postcards sent from Yu Garden~", "sticker": { "image": "stamp.jpg", "stamp": { "country": "CHINA", "fee": "¥1.20" } } },
    { "type": "callout", "text": "Every stop got its own seal.", "sticker": { "image": "seal.png", "postmark": true } },
    { "type": "stats",   "label": "Trip in numbers", "value": "3 days · 12 stops · 41 km" },
    { "type": "text",    "align": "left", "text": "Longer notes read better from the left edge." }
  ],
  "footer": {
    "links": [ { "text": "首页", "url": "https://example.com" }, { "text": "Contact", "url": "mailto:hello@example.com" } ],
    "lines": ["Oct 2026 · Chun Lin & family"]
  }
}
```

```csharp
var svg = new NewsletterGenerator().Create(
    new Newsletter
    {
        Header = new("New on the blog"),
        Blocks = [new NewsletterCards([new NewsletterCard("aot.png", "Native AOT on Lambda") { Link = "https://example.com/aot" }])],
        Footer = new() { Lines = ["Thanks for reading"] },
    },
    new NewsletterOptions(),
    name => File.OpenRead(Path.Combine(folder, name)));
```

Code: `src/SvgCreator/Newsletter.cs`, `NewsletterOptions.cs`, `NewsletterGenerator.cs` (public API and validation), `src/SvgCreator/Composition/NewsletterComposer.cs` (layout and drawing), `Composition/NewsletterImages.cs` (embedding pictures), `Composition/AccentText.cs` (`[[accent]]` text), `src/SvgCreator.Cli/NewsletterJson.cs` (the file)

## A fixed frame around a list of blocks

The header, hero, intro and footer always sit in the same order, so every newsletter has the same frame. Between them is a list of blocks in any order:

| Block | Draws | From the email |
|---|---|---|
| `ribbon` | A heading on a bar in the accent colour, with stepped pixel ends and a pixel chevron off each | 兑换码, 更多内容 |
| `text` | Paragraphs, centred or (`"align": "left"`) from the left | The intro paragraph |
| `cards` | Rows of a 16:9 picture beside a title and text, the picture swapping sides each row, each row optionally a link | The three "more content" rows |
| `image` | A picture across the column with a caption | The hero, smaller |
| `button` | A pill, at least half the column wide, optionally a link | 版本情报, 了解更多 |
| `stats` | A small label over a box with the value set large, underlined if it's a link | The redeem code |
| `callout` | Text beside a sticker: the picture as it is, a [postage stamp](Postage-Stamp.md) of it, or a [postmark](Stamp-Style.md) traced from it | The mascot and the Messenger text |
| `map` | A [travel map](Travel-Map.md) of the stops, with its `route`, `breaks` and `layout` | (new) |

Only the header (its title) and the footer (one link or line) are required. The hero and intro are optional, so a two-line "new posts this week" newsletter doesn't need a big picture. The title is always the document's `<title>`, which browser tabs and screen readers show, even when a logo takes its place in the band.

A fixed template (days, then stops with photos) was considered first, since the trip was the first use. A list of blocks won because the same command should make a blog's or a shop's newsletter, and the frame keeps every one of them looking finished.

## Pictures are named, not read by the library

The newsletter names its pictures (`"photos/bund.jpg"`), and the generator asks its caller to open each one: `Func<string, Stream>`, or an async version. The CLI resolves names against the file's folder. A Lambda could fetch them from S3 instead, so the library never touches the file system. Each name is opened once, however often it's used: the email above uses its hero again as the first card, and so can a newsletter.

Every name is checked before any picture is opened, as are links, colours and accent markers, so a typo fails fast. A picture that can't be read is named in the error (`photos/bund.jpg: Unsupported or corrupt image data.`), since a newsletter can hold twenty of them.

## The JSON file

- **Blocks are told apart by `"type"`**, using System.Text.Json's `[JsonPolymorphic]` attributes on `NewsletterBlock`. That works with the source generator, so the CLI stays AOT-safe. By default the `"type"` must be a block's first property; `AllowOutOfOrderMetadataProperties` lets it come anywhere, since people write these files by hand. A missing or unknown type gets a message in the file's terms (`every block needs a "type" (ribbon, text, …); missing at $.blocks[0]`), not System.Text.Json's one about abstract types and type discriminators.
- **Text can be a string or an array of paragraphs.** `NewsletterTextJsonConverter` reads either, so `"text": "One line."` needs no brackets.
- **Enums are strings** (`"align": "left"`, `"route": "lines"`), with `UseStringEnumConverter`.

**Lesson: source-generated JSON sets every init property, those missing from the file too.** A sticker's stamp first used `PostageStampOptions` directly. A stamp with only `"country"` then failed with "Value cannot be null (InkColor)", and every corner came out top left. To build a record with init-only properties, the generated code passes all of them in one object initializer, those missing from the JSON as null or 0. `TravelStop` meets the same thing (`init => field = value ?? []`). The fix is a small `NewsletterStamp` whose fields are nullable and fall back to the stamp's defaults in `ToOptions()`. Lists elsewhere in the newsletter use the `?? []` pattern, and its enums default to 0 on purpose: `Center`, `Arrows`, `Auto`.

## Sizes scale with the width

Every size is in pixels at a 640 px column times `width / 640`, so `--width 1280` draws the same newsletter twice the size. The height is whatever the content needs: each section (the header, the hero, the body with its blocks, the footer) is measured first, then written at its place.

That's the opposite of the [travel map's](Travel-Map.md#sizes-dont-grow-with-the-canvas) rule, and on purpose. A map needs more room when it's crowded, so a bigger canvas must mean smaller-looking text. A newsletter's room grows downwards with its content, so its width only sets how big it's drawn, as zooming an email does.

## Embedding pictures

Each picture is embedded once, as an `<image>` in `<defs>`, and drawn with `<use>` wherever it appears. A picture that fills a box (hero, cards, image blocks) is scaled to cover it and clipped to a rounded rectangle; a logo or sticker fits inside its box whole.

Phone photos are far bigger than a 278 px card needs, so before embedding, each picture is scaled down (SkiaSharp, Mitchell cubic, premultiplied so transparent edges don't fringe) to `--image-density` (default 2) pixels per pixel of the **largest** box it's drawn in, then saved as JPEG at quality 85, or PNG if any pixel is transparent. The `<image>` keeps the picture's original size whatever its pixels, so scaling down moves nothing. A picture is kept as it is when it's already small enough, when it's a GIF (which may be animated), or when the smaller version would take more bytes, as a small, well-compressed PNG can.

Measured on the sample trip (four photos of 2000 to 4000 px, a stamp picture, a logo and a seal, about 640 KB of files): `--image-density 0` gives an 864 KB SVG (base64 adds a third), 2 gives 159 KB, and 1 gives 85 KB. Real phone photos of 3 to 5 MB each save far more.

## Text

All text is native `<text>`: small, selectable and editable. The library can't measure it, because the viewer picks the font, so lines are broken from estimated widths (`Svg/TextWrap.cs` and `Svg/FontMetrics.cs`, shared with the travel map). The estimates err wide, so lines come out somewhat shorter than the column. The font stack is `Helvetica, Arial, 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', 'Noto Sans CJK SC', sans-serif`: Latin in the faces the widths were measured for, Chinese from whichever CJK font the viewer has.

**Lesson: CJK is 1 em, not "unknown".** FontMetrics gave every character outside its tables the widest Latin advance, 1.28 em in the sans stack, to stay an upper bound. For Chinese that's 28% too wide, so Chinese paragraphs wrapped at about 78% of the column. Every CJK font sets Han, kana, Hangul and full-width punctuation on a 1 em square, regular or bold, so CJK is now measured at exactly 1 em. That also changed the travel map's and the postage stamp's estimates, for the better.

- **Accented text.** `[[like this]]` is printed in the accent colour, as a `<tspan>`. Wrapping works on the plain text, then each line is matched back to it character by character, skipping the spaces wrapping drops or squeezes, to find where its accented runs fall. A marker left open, closed without opening, or nested is an error, not literal brackets.
- **Balanced lines.** Centred text (the intro, centred text blocks, captions, the footer's lines) is wrapped to the narrowest width that takes no more lines, found by bisection. Without that, the sample intro ended with "it~" alone on its last line.
- **Footer links as one line of text.** The links and the bars between them were first placed one by one from their estimated widths. Because the estimates err wide by different amounts, the bars sat unevenly ("首页 |  Blog   |  Contact"). Each row is now one centred `<text>` of `<a><tspan>` links and en-space bars, so the viewer's font spaces them evenly. The estimates still decide where rows break. En spaces are used because SVG collapses ordinary spaces.
- **One-line text** (ribbons, buttons, stats values, the title) shrinks to fit rather than wrapping.

## Links

Cards, image blocks, buttons, stats and footer links can link, as SVG `<a href>`. Only absolute `http`, `https` and `mailto` addresses are accepted. A `javascript:` link would run when the SVG is opened in a browser, and a relative one means nothing once the file is shared. Links work when the SVG is opened directly or inlined in a page. Inside an `<img>` tag a browser shows the picture but ignores its links, and email clients mostly don't show SVG at all, so to send it by email, export a PNG and link the whole picture.

## Drawing details

- **The hero's wave** is two cubic Béziers meeting smoothly (the control points either side of the join mirror each other), rising and falling 18 px. The hero is clipped to the shape above it, and the same curve is stroked in the accent colour. The picture's own shape sets the height, kept between 5:2 and 5:4.
- **The ribbon** is the bar, two steps in at each end, and a "<" or ">" of three squares off each end at 80% opacity, with a white sheen gradient and a thin highlight line along the bottom.
- **The backdrop** is a `<pattern>` of diamonds of small squares in the page colour, fading from the middle out and checkered in their outer rings so their edges break up into pixels. One diamond is drawn once in `<defs>` and placed five times in each tile (its middle and its corners), giving a diagonal lattice.
- **Stickers.** A stamp is made by the [postage stamp](Postage-Stamp.md) writer, with its picture first scaled down to the sticker's size. A postmark is traced as [Postmark](Stamp-Style.md) style. Both are nested as inner `<svg>`s, as the [mail piece](Mail-Piece.md) nests its pieces (`Composition/NestedSvg.cs` is now shared), and tilted 6° as if stuck on. The postmark is inked in the text colour, because the postmark's default dark ink all but vanished on the purple page.
- **The map** is drawn by the travel map's writer at its size in a 640 px column, with its text at 0.6 of the map's own default size, which is body-text size. It's in the panel colour, its text in the text colour, its waypoints in the accent colour, and it's nested and clipped to a rounded rectangle.
- **Clip paths are written before the group that uses them.** `XmlWriter` can't add an attribute to an element once it has a child, so writing the `<clipPath>` inside the group and then setting `clip-path` on the group threw.

Every part has a class for restyling: `newsletter-backdrop`, `newsletter-column`, `newsletter-page`, `newsletter-header`, `newsletter-title`, `newsletter-hero`, `newsletter-hero-edge`, `newsletter-intro`, `newsletter-ribbon`, `newsletter-ribbon-text`, `newsletter-text`, `newsletter-cards`, `newsletter-card`, `newsletter-card-title`, `newsletter-card-text`, `newsletter-image`, `newsletter-caption`, `newsletter-button`, `newsletter-button-text`, `newsletter-stats`, `newsletter-stats-label`, `newsletter-stats-value`, `newsletter-callout`, `newsletter-callout-text`, `newsletter-sticker-stamp`, `newsletter-sticker-postmark`, `newsletter-map`, `newsletter-map-drawing`, `newsletter-picture`, `newsletter-accent`, `newsletter-footer`, `newsletter-footer-links`, `newsletter-footer-link`, `newsletter-footer-bar` and `newsletter-footer-text`.

## Colours

| Option | Default | Used for |
|---|---|---|
| `--backdrop-color` | `#231a45` deep indigo | Around the column |
| `--page-color` | `#5a4d7b` purple | The column, and the backdrop's diamonds |
| `--band-color` | `#0b0b0f` near black | Header and footer bands |
| `--text-color` | `#ffffff` | Text, the map's markers and route, postmark stickers |
| `--accent-color` | `#f0764a` orange | Ribbons, the hero's wave, `[[accented]]` text, map waypoints |
| `--button-color` | `#a3292b` red | Buttons |
| `--panel-color` | `#463a66` dark purple | Stats boxes, map backgrounds |

The defaults are taken from the email that inspired the layout. Each is any CSS colour, checked as the other generators check theirs.
