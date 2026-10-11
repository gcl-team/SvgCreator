# Colour edges

**Goal:** find boundaries between colours of *similar brightness*, such as pink hair on pale skin, which luminance-based [line extraction](Line-Extraction.md) can't see.

Code: `LineExtractor.MarkColorEdges`, `RgbImage.ToLabChroma`

## CIE Lab and ΔE

RGB distance doesn't match how different two colours *look*. **CIE L\*a\*b\*** separates:

- **L\***: lightness
- **a\***: green ↔ red
- **b\***: blue ↔ yellow

Euclidean distance in Lab, **ΔE**, approximates perceived difference: ΔE ≈ 1–2 is just noticeable. Using only the **a\*b\*** plane measures hue and saturation difference *independent of brightness*, which is exactly what luminance misses.

Conversion: sRGB → linear (gamma lookup table) → XYZ (D65) → Lab. It costs about 0.17 s on a 3 MP image.

## Algorithm

Run the same center-surround comparison on the a\* and b\* planes. A pixel is a colour-edge candidate when `|center_ab − surround_ab| ≥ threshold`.

### Pitfall 1: a step only gives about 24% of its height

For a *line*, center-surround responds strongly. For a *step* between two flat regions, both blurs straddle the edge, and the difference peaks at only

```
max over d of  Φ(d/σ) − Φ(d/3σ)  ≈ 0.24   (about 1.6σ from the edge; independent of σ)
```

of the step height (Φ is the normal CDF). The first version used the raw difference as the threshold, so `ColorContrast = 10` effectively required ΔE ≈ 40. A test pink/grey pair at ΔE 28.5 wasn't detected. **Fix:** the option is now the ΔE between the two regions, and internally the threshold is `ColorContrast × 0.24`.

### Pitfall 2: doubled outlines

Most colour boundaries in artwork *already* have a drawn line. Adding a colour band beside it doubled the outline. Things tried, in order:

1. **Put the line on the more saturated side.** This failed: the blue background is more saturated than pink hair, so the colour line landed on the *opposite* side from the luminance line.
2. **Compare luminance and chroma response strength per pixel.** This failed: a drawn outline is a *line* in luminance but a *step* in chroma, so the two responses have different spatial shapes and the ratio isn't consistent across the band.
3. **Judge whole pieces (the version kept).** Group colour pixels into connected pieces and drop any piece where **≥ 50% of its pixels lie within `ceil(3σ)+1` px of a luminance line**. That piece is the same boundary seen twice. A genuine colour-only boundary touches luminance lines at most at its ends.

**Side rule (kept):** put the colour line on the **darker side**, where a luminance line would be, or on the **more saturated side** when brightness is equal (|ΔL| < 0.5). Both are where an artist would draw the line, and both keep it one-sided.

## Outcome and honest assessment

On the three sample illustrations, colour edges add **very little**: a short line near an eye and a few specks. These artworks outline nearly every region with a line that's darker than its surroundings, so luminance already catches them. The feature matters for **lineless or flat-colour styles, logos, and soft hair-to-skin transitions**. The default `ColorContrast = 20` is safe: it rarely adds clutter, and a dedicated test proves it finds an equal-luma pink/grey boundary that luminance alone can't.
