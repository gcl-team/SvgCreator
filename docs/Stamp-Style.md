# Stamp and postmark styles

**Goal:** an optional output that looks like a rubber-stamp impression: rough edges and worn ink. It's built only from native SVG filter primitives, with no image processing.

```sh
svgcreator input.png --style stamp --ink-color "#c0392b"   # a red seal impression
```

Code: `src/SvgCreator/Svg/StampFilter.cs`; options `Style` and `StampSeed`

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
<filter id="svgcreator-stamp-1-1-W×H" filterUnits="userSpaceOnUse" x="0" y="0" width="W" height="H"
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
- **Unique filter ids.** The id names everything the definition depends on: the seed, the texture scale and the canvas size (`svgcreator-stamp-{seed}-{scale}-{W}x{H}`). Element ids are global when several SVGs share an HTML page or a composed canvas. An id with just the seed let two stamped pieces with the same seed but different sizes share an id with *different* definitions, so one would render with the other's filter. Now two pieces only share an id when their filters are identical, and then either copy works.

## Postmark style

`--style postmark` is the stamp impression **plus a postal cancellation**: parallel wavy lines across the stamp.

```sh
svgcreator input.png --style postmark --ink-color "#c0392b"
```

Code: `src/SvgCreator/Svg/PostmarkCancellation.cs`

### The frame

- **A circle was recognized** ([Shape Recognition](Shape-Recognition.md)): the largest ellipse becomes the postmark's frame, if it's at least 25% of the image's shorter side. That rule stops an eye or an ornament from being picked. On image03, that's the ring at (254, 258) with r = 244.5.
- **No circle** (e.g. a character portrait): a native `<rect>` border, inset slightly from the canvas edges, turns the **whole picture into the stamp**. It frames the canvas rather than the drawing's bounding box, because portraits are often cropped at the image edge.

### The cancellation lines

- **6 parallel waves** spanning the middle 60% of the frame's height.
- **Like real cancellations, they overlap the stamp.** They start half a frame-radius *inside* the frame's right side and run 0.6 radius *past* it.
- **Everything scales with the frame's half-height:** spacing, wavelength and amplitude.
- **The waves are as thick as the ring.** When the frame is a band (an outline with a concentric hole, the same pair the writer turns into one stroked circle), the waves use the band's width, so they read as part of the same stamp. A frame without a band (a solid disc) and the rectangle fallback keep using 1.8% of the frame's half-height. Both have a 1.5 px floor, so the waves survive the worn-ink filter. The first version used that 1.8% for every frame, described as "about the ring's own stroke". Measured, a bold ring traced from a white-page screenshot was 2.5× thicker than the waves, and the filter's pinholes take proportionally more ink from a thin line, so the waves looked faint.
- **Each wave costs a few bytes per hump.** A quadratic Bézier whose control point is offset by 2A peaks at height A, and SVG's `t` command repeats a quadratic by mirroring the previous control point. So one `q` followed by `t`s draws a sine-like wave: `M377.4 110q22.6-35.5 45.2 0t45.2 0t45.2 0…`.
- **They're generated, not traced,** so they sit in their own `<g class="postmark-cancel">` (with the `<rect>` frame, if any), separate from the drawing. The same stamp filter applies to both groups, so the whole mark looks equally worn.

### Size: overlap rather than beside

The first version put the waves fully *beside* the frame. That widened image01 from 2032 to **3304 px** (+63%). macOS Quick Look then rendered it blank, probably because a filter over that large an area exceeded what it can handle. Overlapping the frame, as real postmarks do, limits growth to the overhang:

| Image | Canvas width |
|---|---|
| image01 (rect frame) | 2032 → 2461 (+21%) |
| image02 (rect frame) | 1796 → 2220 (+24%) |
| image03 (circle frame) | 512 → 658 (+28%) |

The canvas is only widened when the waves would otherwise run off the edge. A circle in the middle of a wide canvas needs no extra space.

### Previewing note

Quick Look crops wide SVGs to a square thumbnail, so the frame's edges and the waves' ends looked cut off in previews. The geometry was checked numerically instead: the frame is inside the original canvas, the waves start inside the frame, and they end within the widened canvas. A browser shows the full mark.

