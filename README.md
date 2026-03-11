# ParquetRsForDotnet

`ParquetRsForDotnet` is a .NET 8 library for writing parquet data through Rust `arrow-rs` and `parquet-rs` with an explicit batch-oriented API.

The library focuses on one write model:

- define an explicit `ParquetSchema`
- create a `ParquetFileWriter`
- append one or more batches in strict schema order
- write parquet bytes directly into a destination `Stream`

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
  - exposes a sink callback table over a managed `Stream`
- native Rust (`native/`)
  - imports Arrow schema and record batches via Arrow FFI
  - writes parquet using `parquet::arrow::ArrowWriter`
  - streams parquet bytes directly into the managed sink callbacks

Data flow:

```text
CLR arrays / IArrowArray
    -> managed batch validation/materialization
    -> Arrow C Data export
    -> Rust Arrow FFI import
    -> parquet-rs writer
    -> managed Stream sink
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
    public int TargetBatchRows { get; init; } = 4096;
    public long? TargetBatchBytes { get; init; }

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
  - affect native parquet row-group buffering
- `Compression`
  - selects the parquet compression codec
- `EnableDictionaryEncoding`
  - enables or disables default dictionary encoding
- `StatisticsLevel`
  - controls whether parquet statistics are omitted, written per row group, or written per page
- `ArrowMaterializationMode`
  - tuning knob for CLR array-backed batch materialization
- `NativeWriteBatchSize`
  - optionally overrides parquet-rs internal column write batch size
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
- schema/type validation
- native sink behavior
- Rust-side option and writer tests

## Contributor Notes

For implementation details, ownership rules, and extension points, see `docs/write-architecture.md`.

## Notes

- `Cargo.lock` is kept in source control for reproducible native builds
- Rust build artifacts live under `native/target/` and are ignored
- managed `bin/` and `obj/` folders are ignored

## TSA Bug Filing

TSA bug filing is configured through `tsaoptions.json`. Official builds are expected to keep TSA bug filing enabled. [Learn more](https://aka.ms/OBTSA)
