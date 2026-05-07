# AGENTS

This repository is optimized for agent-assisted work on an Arrow-native parquet library built from:

- managed .NET code under `src/`
- native Rust code under `native/`
- integration tests under `tests/`
- BenchmarkDotNet benchmarks under `benchmarks/`

Use this file as the first-stop guide for coding agents.

## Current product direction

- Public write API is centered on `ParquetFileWriter`
- Public read API is centered on `ParquetFileReader` and `ParquetRowGroupReader`
- Read v1 is Arrow-native only
- Write path is explicit-schema and batch-oriented
- Read path is explicit row-group then column-oriented
- Managed/native interop uses Arrow C Data

## Repository map

- `src/Write/` - public write API and write-side internals
- `src/Read/` - public read API and read-side internals
- `src/Schema/` - public schema types
- `src/Options/` - public option types
- `src/Interop/` - managed/native boundary types and P/Invoke bridge
- `src/Errors/` - public/native-facing error types
- `native/` - Rust parquet implementation
- `tests/` - xUnit end-to-end tests
- `benchmarks/` - performance comparisons, mainly against ParquetSharp
- `docs/read-write-architecture.md` - contributor architecture reference

## Coding style

Follow the .NET runtime coding style referenced by the user:

- source: `https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md`

Important rules to apply in this repo:

- use Allman braces
- use four spaces, no tabs
- use explicit visibility
- use `_camelCase` for private instance fields
- use `s_` for private static fields
- avoid `this.` unless required
- keep `using` directives at the top of the file, outside namespaces
- sort `System.*` usings before other usings
- prefer language keywords like `int`, `string`, `bool`
- use `nameof(...)` instead of string literals where appropriate
- avoid single-line `if (...) throw ...`
- use `var` only when the type is obvious from the right-hand side
- make private/internal types `sealed` or `static` unless inheritance is required
- use conventional comments to improve readability when intent is not obvious:
  - C#: prefer XML doc comments for public APIs and brief `//` comments only for non-obvious implementation details
  - Rust: prefer `///` for public items and short `//` comments for invariants, ownership, callback, or FFI-sensitive logic

When editing existing files, preserve local style only if it clearly predates the repo-wide conventions. For new code, prefer the runtime style above.

## Working rules for agents

- Keep public APIs small and explicit
- Prefer Arrow-native representations over convenience abstractions
- Do not reintroduce reader-based or row-based writer abstractions that were intentionally removed
- Keep managed/native ownership rules obvious
- Avoid whole-file buffering on the read path
- Prefer projection/pushdown rather than over-reading parquet data
- Keep benchmarks apples-to-apples when comparing with ParquetSharp
- Keep `net8.0` and `netstandard2.0` builds compiling when changing public or interop code

## Validation commands

Use these as the default validation set after code changes:

```powershell
dotnet test tests/ParquetRsForDotnet.Tests.csproj --no-restore
```

The test project targets both `net8.0` and `net472`; the `net472` target exercises the `netstandard2.0` library asset from a .NET Framework consumer.

```powershell
dotnet test tests/ParquetRsForDotnet.Tests.csproj -f net472 --no-restore
```

```powershell
cargo test
```

```powershell
dotnet build benchmarks/ParquetRsForDotnet.Benchmarks.csproj
```

Run `cargo test` from `native/`.

```powershell
dotnet pack src/ParquetRsForDotnet.csproj -c Release -p:CrossBuildLinux=true
```

## Read-path testing guidance

- Prefer `ParquetSharp` to generate external parquet source files for reader tests
- Use `ParquetFileWriter` for internal round-trip tests only when that is the intent
- Keep reader tests Arrow-native in v1
- Test row-group and column semantics explicitly

## Write-path testing guidance

- Verify written output with `Parquet.Net`
- Cover both CLR-array input and `IArrowArray` input where relevant
- Keep performance-oriented tests out of normal test execution when possible

## Benchmark guidance

- `BatchWriterBenchmarks` is the main true-parity write benchmark suite
- `ParquetReaderBenchmarks` is the main read benchmark suite
- If changing benchmark shape, document parity assumptions in code comments
- Do not commit generated benchmark artifacts

## Files worth reading before major changes

- `README.md`
- `docs/read-write-architecture.md`
- `src/Interop/NativeParquetBridge.cs`
- `native/src/lib.rs`
- `ParquetRsForDotnet.snk`

## When in doubt

- optimize for explicitness, Arrow-native flow, and low memory footprint
- keep the managed/native surface narrow
- mirror ParquetSharp-style ergonomics only where they fit the current architecture
