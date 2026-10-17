# Mail piece

**Goal:** put the pieces together: an [envelope](Envelope.md) with a [postage stamp](Postage-Stamp.md) in its top-right corner and a postmark over the stamp, as one SVG.

```sh
svgcreator mail -o mail.svg \
  --stamp-image picture.png --country SINGAPORE --fee '$1.20' --caption 'Lion City' \
  --postmark-image seal.png --postmark-mode Threshold

# every piece is optional: an envelope alone, or crumpled with a blue postmark
svgcreator mail -o plain.svg
svgcreator mail -o used.svg --envelope-style Crumpled --stamp-image picture.png --country JAPAN \
  --postmark-image seal.png --postmark-mode Threshold --postmark-color '#1d3f8f'
```

```csharp
var svg = new MailPieceGenerator().Create(
    new EnvelopeOptions(),
    new PostageStampPiece(File.OpenRead("picture.png"), new PostageStampOptions { Country = "SINGAPORE" }),
    new PostmarkPiece(File.OpenRead("seal.png")));
```

Code: `src/SvgCreator/MailPieceGenerator.cs`, `MailPieces.cs` (public API), `src/SvgCreator/Composition/MailPieceComposer.cs` (layout)

CLI flags are prefixed per piece (`--envelope-*`, `--stamp-*`, `--postmark-*`, plus `--width`, `--height`, `--paper-color`, `--country`, `--fee`, `--caption`), so the pieces' options can't collide. `--stamp-image` and `--country` go together.

## Pieces, not layers

Each piece is made by its own generator, unchanged, and nested into the envelope's canvas as an inner `<svg x y width height viewBox>`. An inner `<svg>` keeps its own coordinates, so no generator knows it's being composed. This is why the stamp and the envelope were kept standalone in the first place. Draw order is envelope, postage stamp, postmark.

The public types live at the root of the library, like every other generator. The layout code is internal, in `Composition/`, alongside the other internal stages (`Imaging/`, `Tracing/`, `Fitting/`, `Svg/`).

| Piece | Made by | Placed |
|---|---|---|
| Envelope | `EnvelopeWriter` | The whole canvas |
| Postage stamp | `PostageStampWriter` | Top-right corner, 0.06 inside the strap border, 0.36 of the shorter side tall (narrower if the picture is very wide) |
| Postmark | Traced like `SvgConverter`, default `OutputStyle.Postmark` in dark ink | Its ring (the circular frame the Postmark style finds) is scaled so its outer edge is 0.42 of the stamp's height and centred on the stamp's left edge, 0.62 down, so the waves cross onto the stamp. On a narrow stamp it moves left until it covers at most a third of the stamp's width (see below). Without a circle, the whole picture, 0.8 of the stamp's height, overlaps the stamp's left edge by a quarter; a picture too wide for that is made smaller until it fits on the envelope on both sides of that edge. A framed postmark too wide for the envelope is made smaller too (see below) |

**Lesson: size from the height, overlap from the width.** The ring's size follows the stamp's height, but how much of the stamp it hides depends on the stamp's width. Centred on the edge, the ring covers about 32% of a wide stamp but 56% of a portrait one, which hides most of the picture. A fixed shift left would fix portraits but push the waves off wide stamps: half the stamp's width, tried first, left them ending in the margin of a landscape stamp. So the ring moves left only as far as it must to cover at most a third of the stamp's width (`max(0, radius − width / 3)`). Stamps wider than about 1.26 × their height don't move. The largest shift, about 0.17 of the height on a narrow portrait stamp, still leaves the waves, which reach about 0.67 of the height past the centre, well across the stamp.

The radius here is the ring's **outer edge**. A thick ring is written as one circle stroked along its midline (the test seal: `r="50"` with a 6 px stroke, from radii 53 and 47), but the stroke reaches out to 53. Sizing and the cover rule use 53, because that's what you see and what covers the stamp. Sizing the midline would let the visible ring cover more than a third.

The frame may also be an **ellipse**, possibly rotated, since the circle detector accepts any ellipse. So the layout works from its bounding box: the *vertical* half-extent is sized to 0.42 of the stamp's height, and the *horizontal* half-extent at that size is what the cover rule limits. Treating every frame as a circle sized its minor radius instead, and let an 80 × 40 ellipse reach twice as far onto the stamp as the rule allows.

**Lesson: a wide frame must still fit on the envelope.** Sizing by height says nothing about width, and the cover rule pushes a wide frame further left. On a 550 × 550 envelope with no postage stamp, a 180 × 60 frame in a 400 × 200 drawing scaled to 1.386, and after the shift its ring ran from x = −119.5 to 379.5, so part of it was cut off. So the scale is also capped until the whole postmark canvas, ring and waves, is on the envelope, and the shift is worked out again at that scale. Both canvas edges move steadily as the scale grows, and in each case of the cover rule (ring moved or not) they are straight lines in the scale. So the cap has a closed form: the left edge must fit in both cases, the right edge in either. A first version required the right edge to fit in both cases, which narrowed frames that already fit.

Every option is validated before any image is read, so a bad option fails fast.

## Ids on a shared canvas

Ids are global across the whole document. Nesting only works because every id names everything its definition depends on: the envelope's ids carry its size and seed, and the stamp filter carries its seed, canvas size and the texture values it writes (`svgcreator-stamp-{seed}-{W}x{H}-{edge}-{displacement}-{wear}-{speck}`). That stamp-filter id used to be just `svgcreator-stamp-{seed}`. Two stamped pieces with the same seed but different sizes would then have shared an id with *different* definitions, and one would render with the other's filter. Now an id is shared only when the definitions are identical.

## Known limits

- **The stamp doesn't crumple with the envelope.** It's drawn over the creases, so it looks freshly stuck on a crumpled envelope.
- **The cancellation waves are the Postmark style's own length**: they cross about half the stamp, not all of it.
- Layout is fixed (no position flags or rotation yet), and there's no "PAR AVION" label or address.
