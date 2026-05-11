# AI Bootstrap for ParquetRsForDotnet

Use this file when integrating the `ParquetRsForDotnet` NuGet package from another repository.

## Package Purpose

- `ParquetRsForDotnet` is an Arrow-native parquet reader/writer for .NET.
- Managed .NET owns the public API, validation, CLR array materialization, and stream callbacks.
- Native Rust (`parquet-rs` / `arrow-rs`) owns parquet encoding, parquet decoding, row-group metadata, and column projection.
- The package targets `net8.0` and `netstandard2.0`; .NET Framework consumers use the `netstandard2.0` asset.

## Start With These APIs

- Write parquet: `ParquetFileWriter` + explicit `ParquetSchema` + `WriteBatch(...)`.
- Read parquet: `ParquetFileReader` + `OpenRowGroupReader(...)` + `ReadColumn(...)`.
- Reduce read peak memory: construct `ParquetFileReader` with `ParquetReadOptions.BatchSize` and use `ReadColumnBatches(...)`.
- Configure writes: `ParquetWriteOptions`.
- Handle native failures: catch `NativeParquetException` and inspect `ErrorCode`.

## Required Writer Pattern

```csharp
using var output = File.Create(path);
using var writer = new ParquetFileWriter(output, schema, options);

writer.WriteBatch(column0, column1, column2);
writer.Finish();
```

- Always define `ParquetSchema` explicitly.
- Pass one array per schema column.
- Pass columns in exactly the same order as `ParquetSchema.Columns`.
- Each column in one batch must have the same row count.
- Call `Finish()` explicitly so write failures are observed before disposal.

## Required Reader Pattern

```csharp
using var input = File.OpenRead(path);
using var reader = new ParquetFileReader(input);
using var rowGroup = reader.OpenRowGroupReader(0);

var ids = rowGroup.ReadColumn<int>("id");
```

- Input streams must be seekable.
- Read explicitly by row group, then by column.
- Use `ReadColumn(...)` for Arrow arrays and `ReadColumn<T>(...)` for CLR arrays.
- Use `ReadColumnBatches(...)` when a row-group column is too large to materialize in one array.
- Dispose row-group readers before disposing the file reader.

## Type Mapping Cheatsheet

| Public column type | Write CLR type | Read CLR type on net8.0 | Read CLR type on netstandard2.0 |
| --- | --- | --- | --- |
| `Boolean` | `bool` / `bool?` | `bool` / `bool?` | `bool` / `bool?` |
| `Int8`..`UInt64` | matching CLR integer | matching CLR integer | matching CLR integer |
| `Float32` / `Float64` | `float` / `double` | `float` / `double` | `float` / `double` |
| `String` | `string?[]` | `string[]` with possible nulls | `string[]` with possible nulls |
| `Binary` | `byte[][]` | `byte[][]` | `byte[][]` |
| `Guid` | `Guid[]` | `Guid[]` | `Guid[]` |
| `Date32` / `Date64` | `DateOnly` on net8.0, `DateTime` on netstandard2.0 | `DateOnly` / `DateOnly?` | `DateTime` / `DateTime?` |
| `Timestamp` | `DateTime` / `DateTime?` | `DateTime` / `DateTime?` | `DateTime` / `DateTime?` |
| `Decimal128` | `decimal` / `decimal?` | `SqlDecimal` / `SqlDecimal?` | `SqlDecimal` / `SqlDecimal?` |

## Write Options That Matter

- `MaxRowGroupRows`: maximum row count per parquet row group.
- `MaxRowGroupBytes`: approximate maximum byte size per parquet row group.
- `NativeWriteBatchSize`: advanced parquet-rs encoder write batch size; does not split managed `WriteBatch(...)` calls.

## Read Options That Matter

- `BatchSize`: maximum row count per array yielded by `ReadColumnBatches(...)`.
- `BatchSize` does not change `ReadColumn(...)`; full-column reads still return the entire row-group column.
- Leave unset to preserve current one-batch-per-row-group-column behavior.
- `Compression`: default is `Zstd`.
- `EnableDictionaryEncoding`: default is `true`.
- `StatisticsLevel`: default is chunk-level statistics.
- `ArrowMaterializationMode`: controls CLR-array-to-Arrow materialization strategy.

## Do Not Do This

- Do not infer schema from objects or dictionaries; the SDK does not provide schema inference.
- Do not write rows one at a time; batch column arrays instead.
- Do not pass columns out of schema order.
- Do not read from non-seekable streams.
- Do not assume writer/reader instances are thread-safe.
- Do not read decimal columns as `decimal`; use `SqlDecimal` for CLR reads.
- Do not use `DateOnly` from .NET Framework / `netstandard2.0` consumers.
- Do not assume `NativeWriteBatchSize` controls row-group boundaries.
- Do not assume `ParquetReadOptions.BatchSize` reduces memory unless using `ReadColumnBatches(...)`.

## Native Assets

- NuGet runtime assets are under `runtimes/<rid>/native/`.
- Supported packaged RIDs are `win-x64` and `linux-x64` when the package is built with Linux cross-build enabled.
- Native load failures surface as `NativeParquetException` or platform loader exceptions depending on where binding fails.

## Retrieval Keywords

`parquet`, `arrow`, `Arrow C Data`, `row group`, `column projection`, `ParquetFileWriter`, `ParquetFileReader`, `ReadColumn`, `WriteBatch`, `netstandard2.0`, `net472`, `DateOnly`, `DateTime`, `SqlDecimal`, `native runtime asset`.
