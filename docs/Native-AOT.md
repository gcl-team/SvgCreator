# Native AOT

**Why:** the library is intended to run on **AWS Lambda** with a Native AOT build on the `provided.al2023` runtime. That gives fast cold starts and no JIT.

## Guardrails

- The library sets **`<IsAotCompatible>true</IsAotCompatible>`**. That enables the trim, AOT and single-file analyzers.
- **`TreatWarningsAsErrors`** is on everywhere, so any trim or AOT warning (IL2xxx / IL3xxx) fails the build.
- **Banned APIs:** reflection, `dynamic`, `Activator.CreateInstance`, `XmlSerializer` and reflection-based `System.Text.Json`. Use `XmlWriter`/`StringBuilder`. JSON uses a source-generated `JsonSerializerContext` (`TravelStopsJson`, `NewsletterJson`). Polymorphic types (`[JsonPolymorphic]` and `[JsonDerivedType]` on the newsletter's blocks) and custom converters named in `[JsonConverter]` attributes work with it too: the generator writes code for each derived type and creates each converter directly.
- Generic APIs are fine, e.g. `Enum.IsDefined<T>`, and System.CommandLine 2.x's typed options.
- **Hot loops** use flat arrays and `Span<T>`, with no per-pixel allocation.

## The CLI doubles as the AOT smoke test

The CLI is packaged as a normal (framework-dependent) dotnet tool, but publishing it with AOT exercises the library's whole code path:

```sh
dotnet publish src/SvgCreator.Cli -c Release -r osx-arm64 -p:PublishAot=true
```

The checks are:
1. **Zero warnings.**
2. **Identical output:** the native binary's SVG must be **byte-identical** to the JIT build's on the sample images.

## Lambda notes (for later)

- **Native AOT can't cross-compile between operating systems.** Build `linux-x64` or `linux-arm64` inside a Linux container or in CI.
- **SkiaSharp needs its native library on Linux.** Reference **`SkiaSharp.NativeAssets.Linux.NoDependencies`**, which avoids needing fontconfig/freetype on the system. The CLI already does this.
