# SVG output

Code: `src/SvgCreator/Svg/SvgWriter.cs`

## Structure

```xml
<svg width="W" height="H" viewBox="0 0 W H" xmlns="http://www.w3.org/2000/svg">
  <g fill="#000" fill-rule="evenodd" stroke="none">
    <path d="M…Z M…Z" />   <!-- one path per component: outer border + holes -->
  </g>
</svg>
```

- **Shared attributes.** Fill, fill rule and stroke are set once on the `<g>` rather than on every path.
- **Safe XML.** The document is written with `XmlWriter`, which escapes attribute values. An injected `FillColor` such as `"><script>` can't break out of the attribute; there's a test for this.
- **No XML declaration.** It's omitted because the output is a .NET string (UTF-16), and a declaration would state the wrong encoding once the file is saved as UTF-8.

## Styles are layers

`SvgConverter` runs in two halves: `LineArtTracer` turns the image into a `TracedDrawing` (canvas size plus fitted outlines), and `SvgWriter` renders it. How it looks is an `SvgStyle`, built from two kinds of layer:

| Layer | Interface | What it does | Example |
|---|---|---|---|
| Ink filter (0 or 1) | `IInkFilter` | A `<filter>` in `<defs>`, applied to the ink group and to every decoration, so everything looks printed the same way | `StampFilter` |
| Decorations (0 or more) | `IDecoration` | Generated (not traced) artwork in its own `<g>` after the ink. It can ask for a wider canvas | `PostmarkCancellation` |

An `OutputStyle` is just a named combination of layers (`SvgStyle.For`): Stamp is the stamp filter alone, and Postmark is the stamp filter plus the cancellation decoration. A new look, such as a perforated postage-stamp edge, is a new `IDecoration` plus one line in `SvgStyle.For`, and `SvgWriter` doesn't change. Something that arranges several drawings, such as an envelope, belongs in its own composer that takes `TracedDrawing`s, not in `OutputStyle`.

**Golden tests.** `SvgGoldenTests` pins the exact SVG text of every style in `tests/SvgCreator.Tests/Golden/`, so a refactor can't change the output unnoticed. After an intentional output change, run `UPDATE_GOLDEN=1 dotnet test` and review the diff of those files.

## Lesson: curves made the file *bigger* at first

After [curve fitting](Curve-Fitting.md), the segment count dropped by 45%, but the files **roughly doubled** in size. A cubic `C` command has 6 numbers to an `L` command's 2, and they were printed with 2 decimals each.

| Version | image03 size |
|---|---|
| Polygon `L` commands, integer coordinates | 13.3 KB |
| Absolute `C` commands, 2 decimals | 28.7 KB |
| **Relative `c`/`l` commands, 0.1 px precision** | **17.5 KB** |

## Compact path encoding

1. **Relative commands.** Each subpath starts with an absolute `M`, then lowercase `c`/`l` with *deltas* from the previous point, which are small numbers with few digits.
2. **Tenths precision.** 0.1 px is invisible at any normal zoom.
3. **No rounding drift.** Coordinates are rounded to integer tenths *first*, and each delta is taken between rounded points. If you rounded the deltas themselves, the errors would pile up along a long path.
4. **Shortest number form.** `0.5` → `.5`, `-0.5` → `-.5`. A space is only inserted where a digit would otherwise run into the next number; a minus sign separates numbers by itself.
5. **Implicit closing line.** If the last segment is a straight line back to the start, it's left out, because `Z` draws it.
6. **Straight curves become lines.** A cubic whose control points lie within 0.1 px of the chord is written as `l`.

Example: `M2 1c1.3 4.7 10 5 10 .5c0-3.5-9.5-3.5-10-.5Z`
