# Postage stamp layout

**Goal:** turn a picture into a postage stamp: the image on white paper with a perforated edge, the country name in large type, the fee in another corner, and a small caption.

```sh
svgcreator input.png --postage-stamp SINGAPORE --fee '$1.20' --caption 'Lion City, 2026' --ink-color '#7a1f1f'

# move items around; Postmark cancels the stamp
svgcreator input.png --postage-stamp MALAYSIA --fee 50c --fee-corner BottomRight --style postmark
```

Code: `src/SvgCreator/OutputLayout.cs` (public options), `src/SvgCreator/Svg/PostageStampWriter.cs` (layout)

## A layout, not a style

A style (Clean, Stamp, Postmark) changes how the ink of a drawing looks. A stamp is different: its canvas is the paper, not the image, and the picture sits in a window inside it. So it's a separate option, `SvgConversionOptions.Layout`, and the two combine:

| `--style` | On a postage stamp |
|---|---|
| Clean | Crisp printing |
| Stamp | The [stamp filter](Stamp-Style.md) wears the frame and the text. The picture is never filtered |
| Postmark | Worn printing, plus cancellation waves over the stamp's right side. The paper's own edge is the frame, so no rectangle is drawn, and the canvas widens for the waves' overhang |

`PostageStampWriter` hands its content to the same `SvgWriter.WriteDocument` the line art uses, so canvas widening, the filter definition and decorations work the same way for both (see [SVG Output](SVG-Output.md#styles-are-layers)).

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

Items in the same row (top or bottom) share a baseline, so the large country name and a smaller fee still line up. A row with nothing in it takes no space.

## Perforations

The paper is one path: a rectangle with semicircular bites along every edge. Each side gets a whole number of holes (`round(length / pitch)`, at least 2) spaced evenly, so the corners always match. The outline runs clockwise, and with SVG's y axis pointing down, **sweep flag 0** turns each arc *into* the paper. With sweep 1 you get bumps instead of bites.

The paper is white with a light grey edge, because on a white page white paper would otherwise be invisible.

## Lesson: text width without a font

Native `<text>` stays small and editable, but the library never knows which font the viewer will pick, so it can't measure text. Long names would run into each other ("SINGAPORE$1.20"). The writer estimates widths per character class instead. The values below were measured on bold Georgia, the widest of the fallbacks:

| Characters | Advance (em) |
|---|---|
| Capitals (and anything unknown) | 0.74 |
| Digits | 0.62 |
| Lowercase | 0.54 |
| Space, punctuation, `i l j` | 0.30 |

Regular weight is 8% narrower. If a row's estimate plus the gap between items is wider than the picture, the whole row shrinks evenly. A flat 0.62 em for every character underestimated bold capitals by about 15%, which is exactly the overlap the first render showed.

## Lesson: scale the wear to the finest ink

The stamp filter is tuned for line art strokes a few pixels thick. On a stamp the finest ink is the caption, whose strokes are about a tenth of an em. Scaling the texture to the picture size (or clamping the scale at 1) made the wear coarse next to that text, so captions lost whole letters ("F stiv l series"). The scale is now `unit × 0.05 × 0.1 / 4`: the caption's stroke width divided by the stroke width the filter was tuned for. The wear then looks the same relative to the text at any image size.

## Checking renders

Quick Look (`qlmanage -t`) crops thumbnails to a square, so it hides layout bugs at the edges. Headless Chrome renders the whole stamp as a browser will. A grey page makes the white paper visible:

```sh
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --screenshot=out.png \
  --window-size=900,1100 file:///path/to/page.html   # page.html: <img src="stamp.svg"> on a grey body
```
