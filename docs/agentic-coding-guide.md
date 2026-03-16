# Agentic Coding Guide

This guide is for AI coding agents working in this repository. It complements `AGENTS.md` by giving more implementation-specific context.

## Design priorities

Primary priorities for this codebase:

1. low memory footprint
2. strong performance
3. explicit Arrow-native APIs
4. simple and maintainable managed/native boundaries

Prefer designs that keep data in Arrow form and avoid redundant conversions.

## Public API model

### Write

- `ParquetFileWriter`
- explicit `ParquetSchema`
- `WriteBatch(...)` from:
  - CLR arrays
  - `IArrowArray`

### Read

- `ParquetFileReader`
- `OpenRowGroupReader(...)`
- `ParquetRowGroupReader.ReadColumn(...)`
- Arrow-native only in v1

## Managed/native split

### Managed side

Managed code should generally own:

- public API shape
- validation
- CLR-array materialization on writes
- Arrow import/export wrappers
- `Stream` lifetime
- callback context lifetime
- mapping between Arrow schema and public schema types

### Native side

Rust should generally own:

- parquet encoding/decoding
- row-group and column projection
- metadata loading and reuse
- callback-backed source/sink adaptation
- Arrow FFI import/export

## Common patterns

### Good patterns

- explicit native handle lifecycle
- row-group-oriented reads
- projected column reads
- schema-first writes
- immediate disposal of imported/exported temporary Arrow FFI wrappers

### Patterns to avoid

- whole-file buffering for read unless explicitly justified
- implicit schema inference from convenience shapes
- row-based APIs layered on top of Arrow-native APIs
- duplicate abstractions for the same pipeline stage

## Coding style reference

Apply the .NET runtime coding style the user referenced:

- `https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md`

High-value style rules for this repo:

- Allman braces
- explicit visibility
- `_camelCase` private fields
- `s_` static fields
- no `this.` unless needed
- no single-line `if (...) throw ...`
- prefer `nameof(...)`
- prefer explicit, readable code over clever compact code

## Comment conventions

Use comments to clarify intent, ownership, invariants, and interop-sensitive behavior, not to restate obvious code.

### C# comments

- use `///` XML doc comments for public APIs and important internal helpers when their role is not obvious
- use short `//` comments for non-obvious implementation details, especially around:
  - Arrow import/export ownership
  - native handle lifetime
  - row-group/column projection assumptions
  - callback bridge behavior
- avoid noisy comments that simply narrate each line of code

### Rust comments

- use `///` for public FFI entrypoints and public structs where the ABI or ownership contract matters
- use short `//` comments for:
  - safety assumptions in `unsafe` code
  - ownership/lifetime expectations across FFI
  - projection/read-path invariants
  - callback-backed I/O behavior
- keep comments close to the code they justify

## How to approach new work

### New write work

- start from `src/Write/`
- check `src/Interop/NativeParquetBridge.cs`
- update Rust writer lifecycle in `native/src/lib.rs`
- add tests in `tests/ParquetWriterTests.cs`
- update benchmarks if write performance semantics change

### New read work

- start from `src/Read/`
- preserve seekable source callback design
- prefer row-group scoped readers
- use parquet-rs projection for column reads
- add tests in `tests/ParquetReaderTests.cs`

### Schema work

- keep public schema types in `src/Schema/`
- keep write-side schema mapping and read-side schema import symmetric where practical
- be careful with decimal, timestamp, and fixed-size binary/guid disambiguation

## Validation checklist

After meaningful code changes, run:

```powershell
dotnet test tests/ParquetRsForDotnet.Tests.csproj --no-restore
```

```powershell
cargo test
```

```powershell
dotnet build benchmarks/ParquetRsForDotnet.Benchmarks.csproj
```

## Benchmark expectations

- use the same parquet bytes when comparing readers
- use the same logical schema and row-group structure when comparing writers
- call out whether a benchmark measures:
  - CLR materialization
  - Arrow-native operation
  - parquet encode/decode only
  - managed allocation only or native memory too

## Good first files to inspect

- `src/Write/ParquetFileWriter.cs`
- `src/Read/ParquetFileReader.cs`
- `src/Read/ParquetRowGroupReader.cs`
- `src/Interop/NativeParquetBridge.cs`
- `native/src/lib.rs`
- `tests/ParquetWriterTests.cs`
- `tests/ParquetReaderTests.cs`
- `benchmarks/BatchWriterBenchmarks.cs`
- `benchmarks/ParquetReaderBenchmarks.cs`

## Suggested agent behavior

- make focused changes
- keep APIs explicit
- update tests with code changes
- update docs when public behavior changes
- prefer deleting obsolete code over keeping parallel legacy paths
