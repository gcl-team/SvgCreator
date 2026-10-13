# Postage stamp

**Goal:** turn a picture into a postage stamp: the image on white paper with a perforated edge, the country name in large type, the fee in another corner, and a small caption.

```sh
svgcreator stamp input.png --country SINGAPORE --fee '$1.20' --caption 'Lion City, 2026' --ink-color '#7a1f1f'

# move items around
svgcreator stamp input.png --country MALAYSIA --fee 50c --fee-corner BottomRight
```

```csharp
var svg = new PostageStampGenerator().CreateFromFile("input.png",
    new PostageStampOptions { Country = "SINGAPORE", Fee = "$1.20", Caption = "Lion City", InkColor = "#7a1f1f" });
```

Code: `src/SvgCreator/PostageStampGenerator.cs`, `PostageStampOptions.cs` (public API), `src/SvgCreator/Svg/PostageStampWriter.cs` (layout)

## Its own generator, always clean

A stamp is a different product from line art, so it isn't a mode of `SvgConverter`. It has its own `PostageStampGenerator` and `PostageStampOptions`. It never traces, so the line-extraction options would mean nothing. It also ignores the output styles (Clean, Stamp, Postmark): the stamp is always printed clean.

That's deliberate. A worn look or a postmark is separate artwork, and the plan is to compose several SVGs on one canvas later: a stamp, a postmark over it, an envelope around them. Keeping each piece single-purpose means the composer can combine them freely, and no generator has to know about the others.

An earlier draft put the stamp behind a `Layout` option on `SvgConversionOptions`, with styles applied on top. It worked, but it mixed two products in one options record, and the stamp ended up owning its own postmark.

The stamp still renders through `SvgWriter.WriteDocument`, with `SvgStyle.Clean`, like the line art (see [SVG Output](SVG-Output.md#styles-are-layers)).

## The picture is embedded, not traced

The original image goes in an `<image href="data:…;base64,…">` at its own pixel size, so it's never resampled and the line-extraction options don't apply. PNG, JPEG, WebP, GIF and BMP keep their original bytes. Any other format SkiaSharp decodes is re-encoded as PNG, because browsers can't show it.

The SVG is roughly 4/3 the size of the input file, the cost of base64.

## Sizes

Everything is a fraction of the picture's shorter side (`unit`), so the stamp looks the same at any resolution:

| Part | Size |
|---|---|
| Paper margin around the printed area | 0.08 |
| Perforation pitch / hole radius | 0.05 / 30% of the pitch |
| Country / fee / caption type | 0.12 / 0.11 / 0.05 |
| Picture frame stroke | 0.006 |

**Precision.** Every value the stamp writes is a fraction of the picture: positions, sizes, stroke widths, font sizes, and the perforated path. So all of them are written with up to four decimals, and the path uses absolute coordinates so rounding can't build up along the edge. The [tenths](SVG-Output.md#compact-path-encoding) used for traced line art broke the layout in four ways, each worse on small pictures:

- The paper edge (0.003) rounded to 0 for any picture under 16.7 px, which removed the very edge that keeps white paper visible on a white page. The frame (0.006) disappeared under 8.3 px.
- The paper is inset from the canvas edge by its stroke width, so the edge stroke isn't clipped. At tenths that inset (0.024 px on an 8 px picture) rounded to 0, and half of the top and left edge fell outside the `viewBox`. Writing the path at full precision costs about 2.5 KB on a 2000 px stamp, 0.1% of the file next to the embedded picture.
- The frame and the picture were each rounded separately, so the frame drifted off the picture's edge. Even the 60×40 test picture was 0.08 px off, a third of the frame's width.
- `FitRow` makes a row fill exactly the space available, so a font size moved by up to 0.05 could push the row past the picture again.

Items in the same row (top or bottom) share a baseline, so the large country name and a smaller fee still line up. A row with nothing in it takes no space.

## Perforations

The paper is one path: a rectangle with semicircular bites along every edge. Each side gets a whole number of holes (`round(length / pitch)`, at least 2) spaced evenly, so the corners always match. A side has at most 200 holes, which a 10:1 stamp reaches. Past that the holes spread out. Without the cap, the pitch (which follows the *shorter* side) would give a 1 × 1,000,000 px picture 20 million holes, so a small input could exhaust memory. The outline runs clockwise, and with SVG's y axis pointing down, **sweep flag 0** turns each arc *into* the paper. With sweep 1 you get bumps instead of bites.

The paper is white with a light grey edge, because on a white page white paper would otherwise be invisible.

## Lesson: text width without a font

Native `<text>` stays small and editable, but the library never knows which font the viewer will pick, so it can't measure text. Long names would run into each other ("SINGAPORE$1.20"). The writer estimates widths per character class instead. The values below were measured on bold Georgia, the widest of the fallbacks:

| Characters | Advance (em) |
|---|---|
| Capitals (and anything unknown) | 0.74 |
| Digits | 0.62 |
| Lowercase | 0.54 |
| Space, punctuation, `i l j` | 0.30 |

Regular weight is 8% narrower. If a row's estimate plus the gap between items is wider than the picture, the text shrinks evenly into the space left after the gap. A flat 0.62 em for every character underestimated bold capitals by about 15%, which is exactly the overlap the first render showed.

**Scale only what actually scales.** The first fix divided the available width by *text + gap*. But the items are anchored to the picture's edges, so the gap never shrinks. The row then came out `gap × (1 − scale)` too wide: 61.2 px in a 60 px row in the long-country test, which is enough for the labels to touch. The scale is now `(available − gap) / text`. `FitRow_ShrinksOnlyTheTextSoTheRowWithItsFixedGapFits` checks the fitted width before serialization. `Create_FittedRowStillFitsAfterFontSizesAreWritten` reads the four-decimal `font-size` values back from the SVG and checks that the row still measures the available width.

## Checking renders

Quick Look (`qlmanage -t`) crops thumbnails to a square, so it hides layout bugs at the edges. Headless Chrome renders the whole stamp as a browser will. A grey page makes the white paper visible:

```sh
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --screenshot=out.png \
  --window-size=900,1100 file:///path/to/page.html   # page.html: <img src="stamp.svg"> on a grey body
```
