# Licensing and dependencies

SvgCreator is **MIT**-licensed and must stay usable in closed-source and commercial apps. This ruled out several obvious dependencies.

## Potrace and its ports: GPL

- **Potrace**, the standard bitmap-to-vector tracer, is **GPL v2+**.
- **BitmapToVector** and **BitmapToVector.SkiaSharp** on NuGet are .NET ports of it, so they're **GPL-3.0-or-later**. They also haven't been updated since 2021.

**Copyleft:** distributing software that includes GPL code, or is derived from it, requires the whole combined work to be released under the GPL.
- **Porting is deriving.** Translating the C source into C# makes a derivative work.
- **Publishing a NuGet package is distributing.** Every consuming app would then need to be GPL-compatible.
- **Running on your own server isn't distributing**, unless the license is AGPL. So a private Lambda deployment alone would be fine. The NuGet package is the problem.

**What's allowed:** copyright protects *code*, not *ideas*. Implementing techniques from published papers (Schneider 1990, Suzuki–Abe 1985, Winnemöller 2012) without copying GPL source is a clean-room implementation. *This is a practical engineering guideline, not legal advice.*

## ImageSharp 4.x: build-time license key

ImageSharp uses the **Six Labors Split License** (free for open source or under $1M revenue, otherwise commercial). From **4.x** it also adds an MSBuild check, `SixLabors_ValidateLicense`:
- **Debug builds** only warn when there's no key.
- **Release builds fail**, and that includes `dotnet pack`, AOT publish and CI.

The check ships in the package's `build/` folder rather than `buildTransitive/`, so it doesn't spread to consumers of our package. It still blocks our own Release builds. ImageSharp 3.1.x has no key check, but it hasn't been released since October 2025.

**Decision:** switch to **SkiaSharp**, which is MIT and actively maintained. The trade-off is native binaries per platform, which is handled with the Linux NativeAssets package.

## Brand icons: Simple Icons (CC0)

The newsletter's footer icons (`Composition/NewsletterIcons.cs`, so far only GitHub's mark) are single SVG paths copied from [Simple Icons](https://simpleicons.org). Its drawings are **CC0**, public domain: they can be copied into MIT code without attribution or licence terms passing on. A licence covers the *drawing*, not the *brand*. The marks stay their owners' trademarks, and using one to link to your own page on that service is the use brand guidelines allow. Paths were copied, not a package referenced, so there's no dependency to keep up to date.

## Supply-chain habit

The version picked for each package is the newest one that's been published for **at least two weeks**, e.g. SkiaSharp 4.152.1 rather than a release a few days old.
