# SvgCreator

Turn raster images into line-art SVGs that contain only the ink outlines and no colouring. The main use case is extracting the line art from anime or cartoon character images.

```csharp
using SvgCreator;

var converter = new SvgConverter();
string svg = converter.ConvertFile("character.png", new SvgConversionOptions { Threshold = 100 });
File.WriteAllText("character.svg", svg);
```

## How it works

| Stage | Class | What it does |
|---|---|---|
| Decode | `ImageLoader` | Decodes with SkiaSharp, turns the pixels upright by their EXIF orientation, then converts to luminance composited over white (transparent = background) |
| Extract ink | `LineExtractor` (default), `Binarizer` | **Lines mode:** a pixel is ink when it is at least `LineContrast` levels darker than its blurred surroundings (center-surround Difference of Gaussians, the core of XDoG). Finds coloured lines, such as dark-pink hair lines, and boundaries between regions. Flat areas of any brightness and soft shading produce nothing. Hysteresis (as in Canny) lets a line that starts at `LineContrast` continue along its ridge down to `WeakLineContrast`, so lines that fade, such as hair strands thinning to a tip, stay closed. Boundaries between colours that differ by at least `ColorContrast` (CIE ΔE) but have similar brightness are found in the Lab a*/b* planes. They are skipped where a drawn line already marks the boundary, so lines aren't doubled. **Threshold mode:** pixels darker than `Threshold` become ink, which suits clean black-and-white line art |
| Label | `ComponentLabeler` | Two-pass connected-component labeling (8-connectivity, union-find). Drops specks smaller than `MinComponentArea` |
| Trace | `ContourTracer` | Follows pixel *edges*, not pixel centres, so 1-px lines keep their width. Outer borders run clockwise and holes counter-clockwise |
| Fit curves | `CurveBuilder`, `CornerDetector`, `BezierFitter` | Takes edge midpoints to remove the pixel staircase. Finds corners sharper than `CornerAngle` and snaps them to the pixel corner. Fits cubic Béziers between corners using Schneider's algorithm, within `CurveTolerance` |
| Recognize shapes | `EllipseFitter` | Outlines that are true ellipses (direct least-squares fit, Fitzgibbon/Halíř–Flusser) become native `<circle>`/`<ellipse>` elements, and rings become one stroked shape |
| Write | `SvgWriter`, `SvgStyle` | One `<path>` per component under `fill-rule="evenodd"`. Uses compact relative `c`/`l` commands at 0.1 px precision. Styles add an ink filter (stamp) and decorations (postmark) on top |

Every stage runs in O(pixels) time over flat arrays. The tracer is our own MIT implementation and contains no Potrace or GPL code.

Planned next: automatic `LineSigma` scaling for high-resolution images, and centerline (skeleton) output.

## Technical notes

[`docs/`](docs/Home.md) explains each technique, why it was chosen, and the pitfalls hit along the way: line extraction (DoG and hysteresis), colour edges, contour tracing, Schneider curve fitting, Native AOT and licensing. The pages are laid out so they can be published to the GitHub Wiki as they are.

## Repository layout

```
src/SvgCreator/          NuGet library (PackageId: SvgCreator)
src/SvgCreator.Cli/      dotnet global tool (command: svgcreator)
tests/SvgCreator.Tests/  xUnit v3 tests (Microsoft.Testing.Platform)
docs/                    Technical notes, wiki-ready (Home.md, _Sidebar.md)
```

Future hosts such as `src/SvgCreator.Api` or `src/SvgCreator.Web` go next to these projects. Shared settings live in `Directory.Build.props`, and all package versions live in `Directory.Packages.props` (central package management).

## CLI

```sh
dotnet run --project src/SvgCreator.Cli -- input.png -o output.svg --line-contrast 12 --weak-line-contrast 6 --line-sigma 1 --color-contrast 20 --min-area 4 --tolerance 1

# coloured ink: hex, CSS name or rgb()/hsl()
dotnet run --project src/SvgCreator.Cli -- input.png --ink-color "#c0392b"

# rubber-stamp / seal impression (native SVG filter: rough edges, worn ink)
dotnet run --project src/SvgCreator.Cli -- input.png --style stamp --ink-color "#c0392b"

# postal cancellation: stamp + wavy lines over the circular (or rectangular) frame
dotnet run --project src/SvgCreator.Cli -- input.png --style postmark --ink-color "#c0392b"

# postage stamp: the image embedded on perforated paper, with country, fee and caption
dotnet run --project src/SvgCreator.Cli -- stamp input.png --country SINGAPORE --fee '$1.20' --caption 'Lion City'

# airmail envelope: red and blue strap border, flat or crumpled
dotnet run --project src/SvgCreator.Cli -- envelope -o envelope.svg --style crumpled --seed 7

# all together: envelope, postage stamp and postmark
dotnet run --project src/SvgCreator.Cli -- mail -o mail.svg --stamp-image picture.png --country SINGAPORE --postmark-image seal.png --postmark-mode Threshold

# travel map: numbered stops from a JSON file, joined by arrows in travel order
dotnet run --project src/SvgCreator.Cli -- map stops.json -o trip.svg
dotnet run --project src/SvgCreator.Cli -- map stops.json -o trip.svg --layout panel --width 1600 --height 1000
dotnet run --project src/SvgCreator.Cli -- map stops.json -o trip.svg --route lines
# a far stop (an airport 40 km out) is pulled in with a dotted, labelled break; --breaks none keeps strict scale
# mark transit stops "waypoint": true in the stops file: unnumbered dots the route runs through

# newsletter: header, hero and intro, then ribbons, text, picture cards, buttons, stats, callouts
# and travel maps in any order, and a footer, from a JSON file (picture paths relative to it)
dotnet run --project src/SvgCreator.Cli -- newsletter trip/trip.json -o trip.svg
dotnet run --project src/SvgCreator.Cli -- newsletter trip/trip.json -o trip.svg --width 1280 --margin 0

# clean black-on-white line art
dotnet run --project src/SvgCreator.Cli -- input.png --mode Threshold --threshold 128

# or as a global tool
dotnet pack -c Release -o artifacts
dotnet tool install --global --add-source ./artifacts SvgCreator.Cli
svgcreator input.png
```

## Build and test

```sh
dotnet build
dotnet test
```

## Native AOT

The library sets `IsAotCompatible`, and warnings are treated as errors, so any trim or AOT analyzer warning breaks the build. The CLI doubles as the AOT smoke test:

```sh
dotnet publish src/SvgCreator.Cli -c Release -r osx-arm64 -p:PublishAot=true
```

AWS Lambda (`provided.al2023`) will need a Linux build. Native AOT can't cross-compile across operating systems, so publish for `linux-x64` or `linux-arm64` from a Linux container or CI. On Linux, reference `SkiaSharp.NativeAssets.Linux.NoDependencies`, as the CLI does.

## License

MIT. Dependencies: SkiaSharp (MIT).
