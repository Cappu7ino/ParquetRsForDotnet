# Contributing

Thank you for contributing to `ParquetRsForDotnet`.

Before making non-trivial changes, read the repository guidance in [AGENTS.md](../AGENTS.md) and the deeper agent/contributor notes in [docs/agentic-coding-guide.md](../docs/agentic-coding-guide.md).

## Development Expectations

- Keep public APIs small, explicit, and Arrow-native.
- Preserve managed/native ownership boundaries across the Arrow C Data interop layer.
- Avoid whole-file buffering on the read path.
- Keep changes focused on the behavior being modified.
- Add or update tests when changing public behavior, interop behavior, packaging, or read/write semantics.

## Validation

Run the focused validation set that matches your change. The default checks are:

```powershell
dotnet test tests/ParquetRsForDotnet.Tests.csproj --no-restore
dotnet test tests/ParquetRsForDotnet.Tests.csproj -f net472 --no-restore
```

Run native tests from `native/`:

```powershell
cargo test
```

For package or benchmark changes, also run:

```powershell
dotnet build benchmarks/ParquetRsForDotnet.Benchmarks.csproj
dotnet pack src/ParquetRsForDotnet.csproj -c Release -p:CrossBuildLinux=true
```

The `net472` test leg should run on Windows because it validates the `netstandard2.0` asset from a .NET Framework consumer.
