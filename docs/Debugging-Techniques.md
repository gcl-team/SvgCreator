# Debugging techniques

Most of the real fixes in this project came from **measuring** rather than reasoning. These are the techniques that paid off.

## 1. Probe actual pixel values

**Question:** why did the bangs outline disappear?

The obvious guess was "the tracer can't detect it". Converting the image to BMP with `sips` and reading pixels with a small Python script showed the line was dusty rose with luminance **125–158**, just above the threshold of 128. That one measurement redirected the work from the tracer to line extraction.

## 2. Dump intermediate filter responses

**Question:** why do hair strands have gaps?

A temporary diagnostic test printed the DoG response on a grid around the gap. It showed **7–11 against a threshold of 12**: the line was there, just weak. That pointed to hysteresis (use evidence that already exists) rather than geometric gap-closing (invent new geometry).

Pattern: a throwaway `ZzDebug` test class with `ITestOutputHelper` can call `internal` stages directly (via `InternalsVisibleTo`). Run it with:

```sh
tests/SvgCreator.Tests/bin/Debug/net10.0/SvgCreator.Tests -class SvgCreator.Tests.ZzDebug -showLiveOutput
```

Delete it afterwards.

## 3. Overlay diffs

To see what a change *adds*, render both SVGs into one file: the new output in **red** underneath, the old output in **black** on top. Only additions stay visible in red. This exposed:
- colour edges doubling outlines (red bands beside black lines), and
- hysteresis thickening every line (red halos).

Neither problem shows up in file sizes or segment counts.

## 4. Visual checks at native resolution

`qlmanage -t -s 1024 -o out file.svg` renders an SVG to PNG on macOS, and `sips -c H W --cropOffset Y X` crops it. Comparing the same crop of the original and the output is what confirmed fixes like the bangs and the hair gaps.

## 5. Measure the claim, not the proxy

- "Curves make the file smaller" was **wrong**: segment count fell, but bytes doubled. Always check the number you actually care about.
- A test fitting points sampled from one cubic "failed" at tolerance 0.5, but that was chord-length parameterization being imperfect. The test's expectation was the thing that was wrong.
- A fading-line test failed because the line faded *all the way* to the background colour. A fully faded line is a genuine tip, not a gap.

## 6. Make sure you're running the code you think you're running

A Debug-versus-AOT output difference was first blamed on floating-point rounding. The real cause was a **stale CLI build**: `dotnet test` rebuilt the library and the tests, but the CLI's `bin/` still held the old `SvgCreator.dll`. A colour-contrast sweep run against it had produced misleading conclusions.

**Habit:** run `dotnet build` on the project you're about to execute (or use `dotnet run`, which builds) before comparing outputs. After rebuilding, Debug, Release and AOT outputs were byte-identical.
