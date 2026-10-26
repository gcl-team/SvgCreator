# Newsletter

**Goal:** a short, fun newsletter as one tall SVG, in the style of a game studio's update email: a black header band with a logo, a hero picture with a wavy edge, a few lines of intro with highlighted words, pixel-art section ribbons, rows of pictures beside text, red pill buttons, a "redeem code" box, and a footer of links and small print. It's for any kind of newsletter: a trip (with a [travel map](Travel-Map.md) in it), a small business's news, or new posts on a blog.

```sh
svgcreator newsletter trip/trip.json -o trip.svg                  # 640 px column on a 40 px patterned backdrop
svgcreator newsletter trip/trip.json -o print.svg --width 1280     # everything twice the size
svgcreator newsletter trip/trip.json -o plain.svg --margin 0       # the column alone, no backdrop
svgcreator newsletter trip/trip.json -o big.svg --image-density 0  # embed the pictures unchanged
svgcreator newsletter trip/trip.json -o space.svg --theme cosmos           # a built-in theme in place of the file's own
svgcreator newsletter trip/trip.json -o shop.svg --theme themes/shop.json  # or a theme file
```

The file is JSON. Picture paths, and the paths of stops files, are relative to the file, so a trip folder can move anywhere:

```
shanghai/
  trip.json          the newsletter
  stops-day1.json    one day's stops: "stops": "stops-day1.json" in a map block,
                     and svgcreator map stops-day1.json -o day1.svg reads it too
  logo.png
  photos/
```

```jsonc
{
  "header": { "title": "上海 · Shanghai", "logo": "logo.png" },
  "hero":   { "image": "photos/bund-night.jpg" },
  "intro":  ["嗨，旅行者！我们的[[上海三日游]]回顾来了。", "3 days, 12 stops and far too many [[xiaolongbao]]."],
  "blocks": [
    { "type": "button",  "text": "Full itinerary", "link": "https://example.com/shanghai" },
    { "type": "ribbon",  "text": "Day 1 · 外滩" },
    { "type": "map",     "route": "lines", "stops": "stops-day1.json" },
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

Code: `src/SvgCreator/Newsletter.cs`, `NewsletterOptions.cs`, `NewsletterTheme.cs`, `NewsletterGenerator.cs` (public API and validation), `src/SvgCreator/Composition/NewsletterComposer.cs` (layout and drawing), `Composition/NewsletterImages.cs` (embedding pictures), `Composition/AccentText.cs` (`[[accent]]` text), `src/SvgCreator.Cli/NewsletterJson.cs` (the file)

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
| `map` | A [travel map](Travel-Map.md) of the stops, with its `route`, `breaks` and `layout` (default `names`: the places' names alone). `"stops"` is the list itself, or the path of a stops file as the `map` command reads | (new) |

Only the header (its title) and the footer (one link or line) are required. The hero and intro are optional, so a two-line "new posts this week" newsletter doesn't need a big picture. The title is always the document's `<title>`, which browser tabs and screen readers show.

The header band shows the title alone (84 px tall, 28 px type) or, with a `"logo"`, the logo with the title stacked under it: the logo fitted into 300 × 44 px, the title at 22 px, and the band growing to fit the pair with 16 px above and below. A logo that already says the name can stand alone with `"hideTitle": true`, which keeps the 84 px band and a 52 px logo. It's `hideTitle`, not `showTitle`, because a source-generated JSON reader would turn a missing `showTitle` into false and hide everyone's titles.

A fixed template (days, then stops with photos) was considered first, since the trip was the first use. A list of blocks won because the same command should make a blog's or a shop's newsletter, and the frame keeps every one of them looking finished.

## Pictures are named, not read by the library

The newsletter names its pictures (`"photos/bund.jpg"`), and the generator asks its caller to open each one: `Func<string, Stream>`, or an async version. The CLI resolves names against the file's folder. A Lambda could fetch them from S3 instead, so the library never touches the file system. Each name is opened once, however often it's used: the email above uses its hero again as the first card, and so can a newsletter.

Every name is checked before any picture is opened, as are links, colours and accent markers, so a typo fails fast. A picture that can't be read is named in the error (`photos/bund.jpg: Unsupported or corrupt image data.`), since a newsletter can hold twenty of them.

## The JSON file

- **Blocks are told apart by `"type"`**, using System.Text.Json's `[JsonPolymorphic]` attributes on `NewsletterBlock`. That works with the source generator, so the CLI stays AOT-safe. By default the `"type"` must be a block's first property; `AllowOutOfOrderMetadataProperties` lets it come anywhere, since people write these files by hand. A missing or unknown type gets a message in the file's terms (`every block needs a "type" (ribbon, text, …); missing at $.blocks[0]`), not System.Text.Json's one about abstract types and type discriminators.
- **Text can be a string or an array of paragraphs.** `NewsletterTextJsonConverter` reads either, so `"text": "One line."` needs no brackets.
- **Enums are strings** (`"align": "left"`, `"route": "lines"`), with `UseStringEnumConverter`.
- **A map's stops can be a file.** `"stops": "stops-day1.json"` names the same file the `map` command reads, so each day's stops live in one place. The CLI reads the newsletter as a `JsonNode` tree first, replaces each map block's `"stops"` path with the array from that file, then deserializes the tree. The library still sees a plain list of stops, and, as with pictures, never opens a file itself. A missing or broken stops file is named in the error, with the block it belongs to. The sample trip came out byte-identical either way.

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
- **The map** is drawn by the travel map's writer at its size in a 640 px column, with its text at 0.6 of the map's own default size, which is body-text size. It shows only the places' names unless the block asks for another `layout`.
 It's in the panel colour, its text in the text colour, its waypoints in the accent colour, and it's nested and clipped to a rounded rectangle.
- **Clip paths are written before the group that uses them.** `XmlWriter` can't add an attribute to an element once it has a child, so writing the `<clipPath>` inside the group and then setting `clip-path` on the group threw.

**Lesson: a map in a newsletter is an overview.** The first version used the map's own default, `auto`. That was fine for the three-stop sample, but on the real Shanghai days (11 to 23 stops each, with metro directions) it filled a 576 px map with cramped labels, or moved them to a side panel at about 6 px. The newsletter's cards already tell the story, so its maps now default to the travel map's new `names` layout. `Layout` is nullable rather than defaulting to `Names`, because a source-generated JSON reader would set a missing enum to 0, which is `Auto`.

Every part has a class for restyling: `newsletter-backdrop`, `newsletter-column`, `newsletter-page`, `newsletter-header`, `newsletter-title`, `newsletter-hero`, `newsletter-hero-edge`, `newsletter-intro`, `newsletter-ribbon`, `newsletter-ribbon-text`, `newsletter-text`, `newsletter-cards`, `newsletter-card`, `newsletter-card-title`, `newsletter-card-text`, `newsletter-image`, `newsletter-caption`, `newsletter-button`, `newsletter-button-text`, `newsletter-stats`, `newsletter-stats-label`, `newsletter-stats-value`, `newsletter-callout`, `newsletter-callout-text`, `newsletter-sticker-stamp`, `newsletter-sticker-postmark`, `newsletter-map`, `newsletter-map-drawing`, `newsletter-picture`, `newsletter-accent`, `newsletter-footer`, `newsletter-footer-links`, `newsletter-footer-link`, `newsletter-footer-bar` and `newsletter-footer-text`.

## Themes

A theme sets how the newsletter looks: nine colours, the typeface and four shapes. The layout (the frame, the blocks and their spacing) is the same in every theme, so every theme fits every newsletter.

| | `starlight` (default) | `postcard` | `minimal` | `cosmos` |
|---|---|---|---|---|
| For | Games, fun trips | Trips, with stamps and postmarks | A blog, a small business | A blog on outer space and planets |
| Page / text | Purple / white | Cream / navy | White / near black | Deep navy / pale blue |
| Accent / button | Orange / red | Red / navy | Blue / blue | Cyan / violet |
| `font` | `sans` | `serif` | `sans` | `sans` |
| `ribbon` | `pixel` | `plain` | `tab` | `tab` |
| `heroEdge` | `wave` | `straight` | `straight` | `arc` |
| `backdrop` | `diamonds` | `none` | `none` | `stars` |
| `corners` | `round` | `round` | `square` | `round` |

The newsletter file picks a theme in one of three ways:

```jsonc
"theme": "postcard"                                                     // a built-in theme
"theme": { "base": "postcard", "accentColor": "#e4572e", "heroEdge": "wave" }  // with changes
"theme": "themes/shop.json"                                             // a theme file, relative to the newsletter
```

A theme file holds the same object, so a shop keeps its colours in one place and every newsletter points at it. `--theme` (a name or a file) takes the place of the file's theme, which makes it quick to try the same newsletter in each theme. The library has the same order: `NewsletterOptions.Theme`, else `Newsletter.Theme`, else `starlight`.

| Setting | Choices | Notes |
|---|---|---|
| `backdropColor` | CSS colour | Around the column |
| `pageColor` | | The column, and the backdrop's diamonds and dots |
| `bandColor`, `bandTextColor` | | The header and footer bands, and their text |
| `textColor` | | Text on the page, a map's markers and route, a postmark sticker's ink, the stars |
| `accentColor` | | Ribbons, the hero's edge, `[[accented]]` text, map waypoints |
| `labelColor` | | Text on the accent and button colours: headings on ribbon bars, button labels |
| `buttonColor`, `panelColor` | | Buttons; stats boxes and map backgrounds |
| `font` | `sans`, `serif` | |
| `ribbon` | `pixel`, `plain`, `tab` | Pixel-art bar, plain bar, or a heading underlined in the accent |
| `heroEdge` | `wave`, `straight`, `arc` | `arc`: the page rises into the hero like a planet's horizon, with a glow |
| `backdrop` | `diamonds`, `dots`, `stars`, `none` | |
| `corners` | `round`, `square` | `square` also makes buttons boxes, not pills |
| `photoTint` | `none`, `gradient`, `duotone` | How photos are coloured; `none` in every built-in theme (see below) |
| `tintStartColor`, `tintEndColor` | Hex colour | The tint's dark and light colours; each built-in theme has its own pair |
| `tintStrength` | 0 to 1 | How strongly the tint is laid over the photo; 0.8 in the built-in themes |

- **Shapes are short lists of choices, not CSS.** Every combination has been drawn and checked, and a theme can't break the layout.
- **Fonts are the measured stacks only.** Line breaks come from per-character widths measured for each stack (`FontMetrics.Sans` and `Serif`). An unmeasured font would wrap wrongly, and text would overrun or break early. The serif stack is `Georgia, 'Times New Roman', 'Songti SC', 'STSong', 'SimSun', 'Noto Serif CJK SC', serif`. The maps keep their sans text in every theme.
- **The colour flags went.** `--page-color`, `--accent-color` and the rest were replaced by `--theme`, which keeps the command short. A one-off colour goes in the file's theme instead.
- **Settings are nullable and filled from the base.** A source-generated JSON reader sets every property the file leaves out to null or 0. With defaults, `{ "accentColor": "#e4572e" }` would have made every other colour null and every shape the first choice. Null means "from the base" instead, and `Resolve()` fills it in.

### Photo tints

`"theme": { "base": "cosmos", "photoTint": "gradient" }` colours every photo (the hero, cards and image blocks, not logos or stickers) to match the theme, as the placeholder photos are coloured:

| `photoTint` | What each photo becomes | SVG |
|---|---|---|
| `gradient` | Its own light and shade, in the colours of a diagonal gradient from the start colour (top left) to the end colour (bottom right) | The gradient laid over the photo with `mix-blend-mode: color`, in a group with `isolation: isolate` |
| `duotone` | The two colours alone: the start colour for its darks, the end colour for its lights | A filter: `feColorMatrix` turns it grey by luminance (0.2126 R + 0.7152 G + 0.0722 B), then `feComponentTransfer` maps grey 0 → start and 1 → end on each channel |

A photo that should keep its own colours, such as food, opts out with `"tint": false` on its hero, card or image block.

Neither tint touches the photo's pixels: the viewer applies both, so the embedded photos are the same, and turning a tint off costs nothing. The maths is a straight line between two colours, `A + (B − A) × t`. For the gradient, `t` is how far across the diagonal a point is; for the duotone, it's the pixel's grey.

- **Why `color` and not a plain overlay.** All four were tried on a real airport photo, mostly blue sky. A see-through overlay washed it out and flattened it. `soft-light` barely changed it, since its sky was already blue. A `color` blend keeps the photo's brightness and takes the gradient's hue and saturation, which is what makes it look like the placeholders. `tintStrength` is the gradient's opacity.
- **Isolation matters.** Without `isolation: isolate` on the photo's group, the blend would mix the gradient with the page behind the photo too, wherever the photo has rounded corners or transparency.
- **Duotone filters run in sRGB.** `color-interpolation-filters="sRGB"` keeps the end colours exactly as given; the default, linear RGB, would shift them. Below full strength, the duotone is a second copy of the photo over the first, at `tintStrength` opacity.
- **Tint colours are hex,** because the duotone needs each colour's red, green and blue as numbers. A CSS name like `navy` is refused with a message saying so.
- **Viewers.** Every current browser supports both. A viewer that ignores `mix-blend-mode` would show the gradient as a plain see-through layer at `tintStrength`, so a viewer that matters (an editor, say) should be checked before relying on it.

**Lesson: one text colour isn't enough.** The first themes had a single text colour, as `starlight` did. `postcard`'s navy text then vanished on its navy footer band, and white button labels were unreadable on `cosmos`'s cyan. Text sits on three kinds of background, so a theme has three text colours: `textColor` on the page, `bandTextColor` on the bands, and `labelColor` on accent bars and buttons.

The stars are placed from a fixed seed, so every render is the same, as the golden files require. They're dots of different sizes and brightness, plus four sparkles (eight-point paths drawn as four narrow kites) in each 200 px tile. The arc's glow is a wide, faint stroke under the line, so it needs no SVG filter.
