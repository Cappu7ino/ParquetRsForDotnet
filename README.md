# ParquetRsForDotnet

`ParquetRsForDotnet` is a .NET 8 library for reading and writing parquet data through Rust `arrow-rs` and `parquet-rs` with explicit Arrow-native APIs.

The library focuses on two Arrow-native models:

- define an explicit `ParquetSchema` and append write batches through `ParquetFileWriter`
- open a parquet file through `ParquetFileReader`, then read columns from explicit row-group readers

## Repository Layout

- `src/` - managed library project and source code
- `tests/` - managed test project and end-to-end verification tests
- `native/` - Rust `cdylib` that performs Arrow import and parquet encoding

## Architecture

At a high level, the library splits responsibility across managed and native layers:

- managed (`src/`)
  - validates the public schema and incoming batches
  - materializes CLR arrays into Arrow arrays when needed
  - exports Arrow schema and record batches through Arrow C Data
  - imports Arrow schema and arrays from native reads
  - exposes a seekable source callback table over a managed `Stream`
  - exposes a sink callback table over a managed `Stream`
- native Rust (`native/`)
  - imports Arrow schema and record batches via Arrow FFI for writes
  - exports Arrow schema and arrays via Arrow FFI for reads
  - writes parquet using `parquet::arrow::ArrowWriter`
  - reads parquet using `parquet-rs` Arrow readers with row-group/column projection
  - streams parquet bytes through managed sink/source callbacks

Data flow:

```text
Write:
CLR arrays / IArrowArray
    -> managed batch validation/materialization
    -> Arrow C Data export
    -> Rust Arrow FFI import
    -> parquet-rs writer
    -> managed Stream sink

Read:
managed Stream source callbacks
    -> parquet-rs reader
    -> Arrow C Data export
    -> managed Arrow import
    -> IArrowArray
```

## Public API

Primary public API surface:

```csharp
public sealed class ParquetFileWriter : IDisposable
{
    public ParquetFileWriter(Stream output, ParquetSchema schema, ParquetWriteOptions? options = null);

    public void WriteBatch(params Array[] columns);
    public void WriteBatch(IReadOnlyList<Array> columns);

    public void WriteBatch(params IArrowArray[] columns);
    public void WriteBatch(IReadOnlyList<IArrowArray> columns);

    public void Finish();
}

public sealed class ParquetFileReader : IDisposable
{
    public ParquetFileReader(Stream input);

    public ParquetSchema Schema { get; }
    public int RowGroupCount { get; }

    public ParquetRowGroupReader OpenRowGroupReader(int rowGroupIndex);
    public ParquetRowGroupReader OpenRowGroupReader(int rowGroupIndex, params string[] projectedColumnNames);
}

public sealed class ParquetRowGroupReader : IDisposable
{
    public int RowGroupIndex { get; }
    public long RowCount { get; }
    public int ColumnCount { get; }
    public int ProjectedColumnCount { get; }
    public IReadOnlyList<string>? ProjectedColumnNames { get; }

    public IArrowArray ReadColumn(int columnIndex);
    public IArrowArray ReadColumn(string columnName);

    public T[] ReadColumn<T>(int columnIndex);
    public T[] ReadColumn<T>(string columnName);
}
```

Supporting public schema types:

- `ParquetSchema`
- `ParquetColumn`
- `ParquetColumnType`
- `ParquetDecimalSettings`
- `ParquetTimestampSettings`
- `ParquetTimestampUnit`

### `ParquetWriteOptions`

```csharp
public sealed class ParquetWriteOptions
{
    public int? MaxRowGroupRows { get; init; }
    public long? MaxRowGroupBytes { get; init; }

    public ParquetCompression Compression { get; init; } = ParquetCompression.Zstd;
    public bool EnableDictionaryEncoding { get; init; } = true;
    public ParquetStatisticsLevel StatisticsLevel { get; init; } = ParquetStatisticsLevel.Chunk;
    public ArrowMaterializationMode ArrowMaterializationMode { get; init; } = ArrowMaterializationMode.Default;
    public int? NativeWriteBatchSize { get; init; }

    public string? CreatedBy { get; init; }
    public IReadOnlyDictionary<string, string>? FileMetadata { get; init; }
}
```

Option intent:

- `MaxRowGroupRows` / `MaxRowGroupBytes`
  - control parquet row-group boundaries and native row-group buffering
- `Compression`
  - selects the parquet compression codec
- `EnableDictionaryEncoding`
  - enables or disables default dictionary encoding
- `StatisticsLevel`
  - controls whether parquet statistics are omitted, written per row group, or written per page
- `ArrowMaterializationMode`
  - tuning knob for CLR array-backed batch materialization
- `NativeWriteBatchSize`
  - advanced knob that optionally overrides parquet-rs encoder chunk size; it does not split managed `WriteBatch(...)` calls, set data page size, or define row-group boundaries
  - defaults to `8_192` rows in this library when unset
- `CreatedBy`, `FileMetadata`
  - flow into native parquet writer properties

## Usage

### Appendable batch write

```csharp
using var destination = File.Create("batches.parquet");

var schema = new ParquetSchema(
[
    new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
    new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
    new ParquetColumn("amount", new ParquetDecimalSettings(18, 2), isNullable: true),
    new ParquetColumn("eventTime", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
]);

using var writer = new ParquetFileWriter(destination, schema, new ParquetWriteOptions
{
    Compression = ParquetCompression.Zstd,
    MaxRowGroupRows = 100_000,
    CreatedBy = "MyEngine",
});

writer.WriteBatch(
    new int?[] { 1, 2 },
    new string?[] { "first", "second" },
    new decimal?[] { 10.5m, 20.25m },
    new DateTime?[]
    {
        new DateTime(2024, 6, 1, 1, 2, 3, DateTimeKind.Utc),
        new DateTime(2024, 6, 1, 4, 5, 6, DateTimeKind.Utc),
    });

writer.WriteBatch(
    new int?[] { 3, null },
    new string?[] { "third", null },
    new decimal?[] { 30.75m, null },
    new DateTime?[]
    {
        new DateTime(2024, 6, 1, 7, 8, 9, DateTimeKind.Utc),
        null,
    });

writer.Finish();
```

Notes:

- column order must exactly match `ParquetSchema.Columns`
- each batch must provide all columns
- all columns in a batch must have the same row count
- `WriteBatch` accepts either CLR arrays or `IArrowArray` values

### Appendable Arrow-backed batch write

```csharp
using Apache.Arrow;
using Apache.Arrow.Memory;
using Apache.Arrow.Types;

var allocator = MemoryAllocator.Default.Value;

var schema = new ParquetSchema(
[
    new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
    new ParquetColumn("eventTime", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
]);

using var destination = File.Create("arrow-batches.parquet");
using var writer = new ParquetFileWriter(destination, schema);

using var ids = new Int32Array.Builder().Append(1).AppendNull().Append(3).Build(allocator);
using var timestamps = new TimestampArray.Builder(new TimestampType(TimeUnit.Millisecond, "UTC"))
    .Append(new DateTimeOffset(new DateTime(2024, 7, 1, 10, 0, 0, DateTimeKind.Utc)))
    .AppendNull()
    .Append(new DateTimeOffset(new DateTime(2024, 7, 1, 12, 0, 0, DateTimeKind.Utc)))
    .Build(allocator);

writer.WriteBatch(ids, timestamps);
```

### Arrow-native row-group column read

```csharp
using var input = File.OpenRead("arrow-batches.parquet");
using var reader = new ParquetFileReader(input);
using var rowGroup = reader.OpenRowGroupReader(0, "id", "eventTime");

var idColumn = (Int32Array)rowGroup.ReadColumn("id");
var eventTimeColumn = (TimestampArray)rowGroup.ReadColumn("eventTime");
```

Notes:

- CLR read materialization is also available through `ReadColumn<T>(...)`
- decimal CLR reads materialize as `SqlDecimal` / `SqlDecimal?`
- input streams must be seekable
- reads are explicit by row group, then by column
- optional row-group projection is selected by column names; integer reads still use original schema ordinals, not projected positions

## Build And Test

Managed builds automatically invoke `cargo build` for the native writer when the Rust toolchain is available at `%USERPROFILE%\.cargo\bin`.

Build and test the full repository:

```powershell
dotnet build ParquetRsForDotnet.slnx
dotnet test ParquetRsForDotnet.slnx
```

Run Rust tests directly:

```powershell
cd native
cargo test
```

## NuGet Packaging

The project produces a strong-name signed NuGet package with Source Link support, `net8.0` and `netstandard2.0` assemblies, and bundled native libraries.

`netstandard2.0` consumers use `DateTime` / `DateTime?` arrays for `Date32` and `Date64` columns because `DateOnly` is not available on .NET Standard 2.0. `net8.0` consumers continue to use `DateOnly` / `DateOnly?` for date columns.

The test project also targets `net472` to verify that .NET Framework consumers can run through the `netstandard2.0` asset.

### Windows-only pack

Build and pack with the Windows native library only:

```powershell
dotnet pack src/ParquetRsForDotnet.csproj -c Release
```

This produces `ParquetRsForDotnet.0.1.1.nupkg` and `ParquetRsForDotnet.0.1.1.snupkg` under `src/bin/Release/`.

### Cross-build for Linux (opt-in)

To include the Linux native library in the package, install the cross-compilation toolchain once:

```powershell
winget install zig.zig
cargo install cargo-zigbuild
rustup target add x86_64-unknown-linux-gnu
```

Then pack with both RIDs:

```powershell
dotnet pack src/ParquetRsForDotnet.csproj -c Release -p:CrossBuildLinux=true
```

The resulting `.nupkg` will contain native libraries under `runtimes/win-x64/native/` and `runtimes/linux-x64/native/`.

For package content verification and native asset troubleshooting, see `docs/how-to/package-and-native-assets.md`.

## Benchmarks

The repository includes a BenchmarkDotNet project under `benchmarks/` focused on true-parity multi-batch writer comparisons against ParquetSharp.

Run it with:

```powershell
dotnet run -c Release --project benchmarks/ParquetRsForDotnet.Benchmarks.csproj
```

Current benchmark coverage focuses on two representative data patterns:

- mixed nullable shape with `int`, `string`, `decimal`, and `DateTime`
- no-decimal nullable shape with `int`, `string`, `DateTime`, and `int`

For both patterns, the benchmark keeps parity by using:

- the same cached input data on both sides
- multiple batches per file
- one row group per batch
- the same compression and logical types

## Testing Strategy

The tests intentionally verify parquet output with `Parquet.Net` rather than relying only on magic-byte checks.

Current coverage includes:

- CLR-array batch writes
- Arrow-array batch writes
- multi-batch writes
- temporal and decimal writes
- Arrow-native row-group column reads
- schema/type validation
- native sink behavior
- native/source reader behavior
- Rust-side option and writer tests

## Contributor Notes

For implementation details, ownership rules, and extension points, see `docs/read-write-architecture.md`.

For AI-assisted consuming-repo integration, start with `docs/ai/bootstrap.md` and `api/public-api.md`. These stable public-contract artifacts are also packed into the NuGet package.

## Notes

- `Cargo.lock` is kept in source control for reproducible native builds
- Rust build artifacts live under `native/target/` and are ignored
- managed `bin/` and `obj/` folders are ignored

## TSA Bug Filing

TSA bug filing is configured through `tsaoptions.json`. Official builds are expected to keep TSA bug filing enabled. [Learn more](https://aka.ms/OBTSA)
