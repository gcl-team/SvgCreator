# Stamp style

**Goal:** an optional output that looks like a rubber-stamp impression: rough edges and worn ink. It's built only from native SVG filter primitives, with no image processing.

```sh
svgcreator input.png --style stamp --ink-color "#c0392b"   # a red seal impression
```

Code: `WriteStampFilter` in `src/SvgCreator/Svg/SvgWriter.cs`; options `Style` and `StampSeed`

## Geometry versus rendering (why it works alongside `--ellipses`)

| | Decides | Lives in the file as |
|---|---|---|
| `--ellipses` | **Geometry:** what shapes are stored | `<circle cx="254" cy="258" r="244.5" …/>`, exact and clean |
| `--style stamp` | **Rendering:** how the browser draws them | `<filter>` in `<defs>` + `filter="url(#…)"` on the ink group |

A real stamp works the same way: the carved design is precise, and the roughness comes from ink and paper when it's pressed. The filter roughens paths and native circles alike, so the impression is consistent, while the clean geometry underneath stays intact. **Delete the one `filter` attribute and you get the crisp drawing back.**

Roughening the geometry itself would have been worse on every count:
- the ring would no longer be detected as an ellipse;
- the file would fill up with jagged detail;
- the effect couldn't be undone.

## The filter, step by step

```xml
<filter id="svgcreator-stamp-1" filterUnits="userSpaceOnUse" x="0" y="0" width="W" height="H"
        color-interpolation-filters="sRGB">
  <!-- 1. rough edges -->
  <feTurbulence type="fractalNoise" baseFrequency="0.8" numOctaves="2" seed="1" result="edgeNoise"/>
  <feDisplacementMap in="SourceGraphic" in2="edgeNoise" scale="2.5"
                     xChannelSelector="R" yChannelSelector="G" result="rough"/>
  <!-- 2. worn ink: coarse blotches -->
  <feTurbulence type="fractalNoise" baseFrequency="0.06" numOctaves="3" seed="2" result="wearNoise"/>
  <feColorMatrix in="wearNoise" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  -14 0 0 0 9.8" result="wear"/>
  <!-- 3. worn ink: fine pinholes -->
  <feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="1" seed="3" result="speckNoise"/>
  <feColorMatrix in="speckNoise" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  -20 0 0 0 15.6" result="speck"/>
  <!-- 4. combine -->
  <feComposite in="wear" in2="speck" operator="in" result="mask"/>
  <feComposite in="rough" in2="mask" operator="in"/>
</filter>
```

| Primitive | What it does here |
|---|---|
| `feTurbulence` (fractal noise) | Generates smooth random noise (Perlin noise). `baseFrequency` sets the grain size: high is fine, low is blotchy. `numOctaves` layers finer detail on top. `seed` picks a different noise pattern. |
| `feDisplacementMap` | Moves each pixel of the ink by an offset read from the noise's R and G channels, by up to `scale` px. The edges wobble like ink pressed onto paper. |
| `feColorMatrix` (to alpha) | Turns noise into a mask: **alpha = Bias − Gain·R**. Fractal noise averages R ≈ 0.5, so ink stays fully opaque where R < (Bias − 1)/Gain and disappears where R > Bias/Gain. That's blotches above R ≈ 0.63–0.70 and pinholes above R ≈ 0.73–0.78. |
| `feComposite operator="in"` | Keeps the input only where the mask is opaque (the alphas multiply). The first combines the two wear masks; the second applies them to the roughened ink. |

`color-interpolation-filters="sRGB"` makes the noise-to-alpha thresholds behave the same in every browser. Without it, some browsers work in linear RGB, which shifts the noise values.

## Tuning notes

- **The first attempt was too worn.** Thresholds at R ≈ 0.55–0.62 broke thin face lines and made every stroke grainy. Raising them to the values above keeps the drawing readable while still looking worn.
- **Scale by line thickness, not image size.** The original plan scaled the texture with the canvas, ×4 for a 2032 px image. That gave 10 px edge displacement on lines only 2–4 px wide, so they frayed. In Lines mode, stroke width comes from `LineSigma` (pixels, at any resolution), so the texture scales with `LineSigma` instead: frequencies are divided by it, displacement is multiplied by it.
- **The seed is deterministic.** The same `StampSeed` always gives the same file, so tests are stable and AOT output matches JIT output. Change it (`--stamp-seed 2`) for a differently worn impression.
- **Unique filter ids.** The id includes the seed (`svgcreator-stamp-1`). Element ids are global when several SVGs are placed inline in one HTML page, so a generic id could make one SVG pick up another's filter.
