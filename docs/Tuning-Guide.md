# Tuning guide

The defaults were tuned on 512–2000 px anime illustrations. Start with them, and change one option at a time.

| Symptom | Option (CLI flag) | Direction |
|---|---|---|
| Faint lines missing | `LineContrast` (`-c`) | Lower it, e.g. 8 |
| Too much speckle or texture | `LineContrast` (`-c`) / `MinComponentArea` (`--min-area`) | Raise to ~16 / raise to 10–20 |
| Lines broken where the original is continuous | `WeakLineContrast` (`--weak-line-contrast`) | Lower it, e.g. 4–5 |
| Thick strokes come out as two parallel edges (large images) | `LineSigma` (`--line-sigma`) | Raise to 1.5–2 |
| Fine texture picked up on high-resolution images | `LineSigma` (`--line-sigma`) | Raise it |
| Unoutlined hair/skin boundary missing | `ColorContrast` (`--color-contrast`) | Lower it, e.g. 12. Set 0 to disable colour edges |
| Curves too fussy | `CurveTolerance` (`--tolerance`) | Raise to 1.5–2 |
| Curves cut across details | `CurveTolerance` (`--tolerance`) | Lower to 0.5–0.75 |
| Sharp tips get rounded | `CornerAngle` (`--corner-angle`) | Raise it, e.g. 150, so more turns count as corners |
| A round shape should stay curves (or a circle wasn't recognized as you expected) | `DetectEllipses` (`--ellipses false`) | Turn off to keep everything as Bézier curves |
| Want a rubber-stamp / seal look | `Style` (`--style stamp`) | Native SVG filter adds rough edges and worn ink; pair with `--ink-color "#c0392b"` for a red seal |
| Want a postal cancellation look | `Style` (`--style postmark`) | Stamp + wavy cancellation lines over the circular frame, or a rectangular frame around the picture when there's no circle. Widens the canvas ~20–30% |
| Different stamp wear pattern | `StampSeed` (`--stamp-seed`) | Any other non-negative number |
| Want coloured ink | `FillColor` (`--ink-color`) | Hex (`#c0392b`, `#c0392b80` with alpha), a CSS name (`navy`), or `rgb()`/`hsl()`. Anything else is rejected |
| The input is already clean black-on-white line art | `Mode` (`--mode Threshold`), `Threshold` (`-t`) | Use Threshold mode |

## Defaults

| Option | Default | Unit |
|---|---|---|
| `Mode` | Lines | |
| `LineContrast` | 12 | luminance levels (0–255) |
| `WeakLineContrast` | 6 | luminance levels |
| `LineSigma` | 1 | px (Gaussian σ) |
| `ColorContrast` | 20 | CIE ΔE (a\*b\*) |
| `Threshold` (Threshold mode) | 128 | luminance |
| `MinComponentArea` | 4 | px |
| `CurveTolerance` | 1 | px |
| `CornerAngle` | 135 | degrees (180 = straight) |
| `DetectEllipses` | true | |
| `Style` | Clean | |
| `StampSeed` | 1 | |
| `FillColor` | `#000` | CSS colour |
