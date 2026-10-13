# Envelope

**Goal:** an airmail envelope as a separate piece of artwork: paper with the classic red and blue strap border, flat or crumpled. Later it can be combined with a postage stamp and a postmark on one canvas.

```sh
svgcreator envelope -o envelope.svg                                # 1100 × 550, flat
svgcreator envelope -o crumpled.svg --style crumpled --seed 7      # a different seed, a different crumple
svgcreator envelope -o kraft.svg --width 640 --height 400 --paper-color '#e8d9b5'
```

```csharp
var svg = new EnvelopeGenerator().Create(new EnvelopeOptions { Style = EnvelopeStyle.Crumpled, Seed = 7 });
```

Code: `src/SvgCreator/EnvelopeGenerator.cs`, `EnvelopeOptions.cs` (public API), `src/SvgCreator/Svg/EnvelopeWriter.cs` (drawing)

## A generator of its own

Like the [postage stamp](Postage-Stamp.md), the envelope is its own product, with its own options. It doesn't follow `OutputStyle`, and it takes no image: everything is drawn. The canvas is the envelope (`Width` × `Height` in pixels), and every part has a class (`envelope-paper`, `envelope-straps`, `envelope-straps-alternate`, `envelope-creases`), so a composer or an editor can find them.

Sizes are fractions of the shorter side and every value is written with up to four decimals, the same [precision rule](SVG-Output.md#compact-path-encoding) as the stamp.

## The strap border

| Part | Size (× shorter side) |
|---|---|
| Border depth | 0.07 |
| Each strap and each gap, along the edge | 0.07 |

Each side of the border is a trapezoid mitred at 45° at the corners. Along it, straps slanted at 45° repeat *strap, gap, alternate strap, gap*. Each side gets a whole number of repeats, so all four start and end the same way, as the stamp's perforations do. The sides run clockwise, so the slant turns consistently around the envelope.

Straps are cut to their side with Sutherland–Hodgman clipping, which is enough because both shapes are convex. The cut is computed rather than done with a `clipPath`, so the flat envelope has **no ids at all** and can be dropped into any canvas. Repeats are capped at 250 per side, because the strap width follows the shorter side and a 1 × 1,000,000 envelope would otherwise get millions.

## Crumpled

Twelve straight creases through seeded random points at random angles. Each crease shades one side of its fold, fading out over about 0.13 of the shorter side, and puts a thin white highlight just across the fold. Where creases overlap they form facets, which is what makes paper look crumpled rather than textured. On top of that, low-frequency noise displaces the whole envelope slightly for irregular edges, and fine noise adds paper grain.

The creases are one group (`envelope-creases`): delete it and the envelope is flat again, apart from the edges.

**Lesson: a lighting filter can't crumple paper.** The first plan was a pure render-time filter, `feTurbulence` into `feDiffuseLighting`, like the [stamp's worn ink](Stamp-Style.md). Twelve recipes later, combining fractal noise and turbulence at different scales, with folds plus creases, it still read as embossed wallpaper or tissue. Perlin noise is smooth, so its relief is rounded bumps. Paper can't stretch, so its creases are *straight*. Seeded straight creases look right in every renderer and cost about 5 KB.

**Determinism.** The creases come from a small SplitMix64 generator, not `System.Random`. .NET doesn't promise that a seeded `Random` gives the same sequence across versions, and the same seed must always give the same envelope (the golden tests depend on it).

## Ids, for composing

Ids are global once several SVGs share a page or a canvas. Each id names everything its definition depends on, so two envelopes only share an id when the definitions are identical, and a duplicate then renders correctly either way:

| Id | Depends on |
|---|---|
| `svgcreator-envelope-{W}x{H}-{seed}-edge` | Size (noise scale) and seed |
| `svgcreator-envelope-{W}x{H}-paper` (crease clip) | Size |
| `svgcreator-envelope-grain` | Nothing: always the same |
| `svgcreator-envelope-crease` (shadow gradient) | Nothing: each crease is a rotated rectangle, so they all share one gradient |
