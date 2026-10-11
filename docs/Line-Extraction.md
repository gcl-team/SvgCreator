# Line extraction

**Goal:** decide which pixels are *ink*, meaning the drawn lines and region boundaries, so that flat colour areas come out empty.

Code: `src/SvgCreator/Imaging/LineExtractor.cs`, `GaussianBlur.cs`, `Binarizer.cs`

## Why a global threshold fails

The first version used `ink = luminance < 128`. On a coloured anime avatar it produced a **posterized silhouette**, not line art:

| Area | Luminance | Result |
|---|---|---|
| Blue background | ~60 | Ink: filled solid black |
| Face, pink hair | 200–250 | Not ink |
| **Bangs outline (dusty rose, e.g. RGB 175,111,124)** | **125–158** | **Mostly *not* ink, so the line vanished** |

Anime artists often draw hair lines in a darker shade of the hair colour, not black. At 512 px the lines are 1–2 px wide, and anti-aliasing lightens them further. Raising the threshold to 170 recovered the lines, but it also turned soft shading into black blobs. **No single absolute threshold separates "a pink line on light hair" from "a pink shadow on light hair."**

The key observation is that the bangs line is about **100 levels darker than the hair right next to it** (131 vs 232). That's strong *local* contrast even though the line isn't dark in *absolute* terms.

## Center-surround Difference of Gaussians (DoG)

Compare each pixel with its neighbourhood instead of with a fixed value:

```
center   = GaussianBlur(luminance, σ)        // the pixel, lightly denoised
surround = GaussianBlur(luminance, 3σ)       // its neighbourhood
ink      = surround − center ≥ LineContrast  // darker than its surroundings
```

| Situation | surround − center | Result |
|---|---|---|
| Flat area of *any* brightness (blue background, face) | ≈ 0 | Empty |
| Soft, gradual shading | ≈ 0 (a linear ramp cancels out) | Empty |
| Thin line darker than its surroundings | Large positive | Ink |
| Step edge between two regions | Positive on the darker side only | A thin one-sided band that outlines the boundary |

This is the core of **XDoG** (Winnemöller et al., *"XDoG: An eXtended difference-of-Gaussians compendium"*, 2012). We deliberately **dropped XDoG's sharpening term**, `(1+p)·G_σ − p·G_kσ`. In flat regions it evaluates to the original brightness, so dark flat areas would become ink again, which is exactly the problem we were trying to remove.

### Choosing the surround scale (3σ, not 1.6σ)

Classic DoG uses `k ≈ 1.6` to approximate a Laplacian of Gaussian. That works for 1-px edges, but **thick lines come out hollow**: in the middle of a wide dark stroke, both blurs see mostly dark pixels, so their difference is about 0, and only the stroke's two edges respond. A wider surround (`SurroundScale = 3`) keeps lines up to about `2·3·σ` px wide filled solid. On large images with thicker strokes, raise `LineSigma`.

### Tuning `LineContrast` (default 12)

Sweep on the 512 px sample:

| Value | Effect |
|---|---|
| 8 | Most complete, a little more noise |
| **12** | **Complete strands, low noise (chosen)** |
| 20 | Strands start breaking up |
| 30 | Too sparse |

### Implementation notes

- **Separable Gaussian blur.** A 2D blur is done as one horizontal pass then one vertical pass, costing O(pixels × radius) instead of O(pixels × radius²). The kernel radius is `ceil(3σ)`, and edges are clamped so image borders don't create false lines.
- **Fast and AOT-friendly.** Everything runs on flat `float[]` buffers.

## Hysteresis: keeping fading lines connected

**Symptom:** hair strand edges had gaps near their tips, even though the original drawing is closed there.

**Measurement:** along the gap, the DoG response stayed at **7–11**, just under the threshold of 12. The response fades because, as a strand thins to a tip, the blue pixels beside it have less bright pink nearby to contrast with. **The line is still in the image; the signal is just weak.**

**Fix (from the Canny edge detector):** use two thresholds.

- A pixel ≥ `LineContrast` (12) **starts** a line.
- A connected pixel ≥ `WeakLineContrast` (6) **continues** it. This is a flood fill from the strong pixels.
- Faint texture that doesn't touch any strong line is still ignored, so noise doesn't increase.

### Pitfall: hysteresis thickens every line

The first version also accepted weak pixels *beside* strong lines, so every line grew about 1 px wider. Canny avoids this by first thinning edges to their peak ("non-maximum suppression"). Our equivalent is a **ridge test**: a weak pixel may only extend a line if it's a local maximum of the response in at least **2 of the 4 directions** (E–W, N–S and both diagonals).

- A pixel on the **centre line (ridge)** of a faint line peaks *across* the line, in several directions.
- A pixel on the **flank** of a strong line only "peaks" along the line's own direction, which is 1 direction, so it's rejected.

**Result:** the number of separate line pieces roughly **halved** on every sample (e.g. 227 → 110), and file sizes changed by +4–8%. Strong lines stay exactly as wide as before (this is covered by a unit test).

## Threshold mode is still available

For clean black-on-white line art, `--mode Threshold` uses the simple global threshold. It's the right tool when the input already *is* line art.
