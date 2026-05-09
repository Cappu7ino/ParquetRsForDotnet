# Public API Semantic Inventory

This file describes stable public-contract behavior for agents and SDK integrators. It avoids internal implementation details unless they affect correct consumption.

## Package

- Package ID: `ParquetRsForDotnet`
- Namespace: `ParquetRsForDotnet`
- Target frameworks: `net8.0`, `netstandard2.0`
- Primary dependency exposed in usage: `Apache.Arrow`
- Native runtime assets: `win-x64`, `linux-x64` when cross-built

## `ParquetFileWriter`

- Responsibility: create a new parquet file from explicit column batches.
- Lifecycle: construct with output stream and schema, call `WriteBatch(...)` zero or more times, call `Finish()`, then dispose.
- Thread safety: not documented as thread-safe; use one writer from one coordinated execution path.
- Stream ownership: caller owns the destination stream; the writer writes to it but does not define application-level stream lifetime.
- Intended usage: column-oriented batch writes from CLR arrays or Arrow arrays.
- Pitfalls:
  - columns must match schema order exactly
  - all batch columns must have equal length
  - no writes after `Finish()`
  - disposal may observe finish errors if `Finish()` was not called explicitly
- Related APIs: `ParquetSchema`, `ParquetColumn`, `ParquetWriteOptions`, `IArrowArray`.

## `ParquetFileReader`

- Responsibility: open parquet metadata and create row-group readers over a seekable stream.
- Lifecycle: construct with seekable input stream, inspect `Schema` / `RowGroupCount`, open row-group readers, dispose.
- Thread safety: not documented as thread-safe.
- Stream ownership: caller owns the input stream; it must remain valid while the reader is alive.
- Intended usage: explicit row-group access and column projection.
- Pitfalls:
  - input stream must be seekable
  - row-group index must be in range
  - dispose row-group readers before disposing the file reader
- Related APIs: `ParquetRowGroupReader`, `ParquetSchema`.

## `ParquetRowGroupReader`

- Responsibility: read Arrow-native or CLR-materialized columns from one row group.
- Lifecycle: created by `ParquetFileReader.OpenRowGroupReader(...)`, used for column reads, disposed.
- Thread safety: not documented as thread-safe.
- Intended usage: read only the columns needed from a specific row group.
- Pitfalls:
  - `ReadColumn<T>` validates exact CLR type
  - decimal CLR reads use `SqlDecimal`, not `decimal`
  - date CLR reads differ by target framework
- Related APIs: `ParquetFileReader`, `Apache.Arrow.IArrowArray`.

## `ParquetSchema`

- Responsibility: ordered collection of public parquet columns.
- Lifecycle: immutable after construction.
- Intended usage: required for every write and returned from every reader.
- Pitfalls:
  - write column order is schema order
  - schema inference is not provided
- Related APIs: `ParquetColumn`.

## `ParquetColumn`

- Responsibility: describe one top-level column.
- Constructors:
  - logical non-decimal/non-timestamp type constructor
  - decimal settings constructor
  - timestamp settings constructor
- Pitfalls:
  - `Decimal128` requires `ParquetDecimalSettings`
  - `Timestamp` requires `ParquetTimestampSettings`
  - nested/list/map columns are not part of the current public schema model

## `ParquetWriteOptions`

- Responsibility: configure native parquet writer behavior and CLR-to-Arrow materialization.
- Active options:
  - `MaxRowGroupRows`
  - `MaxRowGroupBytes`
  - `Compression`
  - `EnableDictionaryEncoding`
  - `StatisticsLevel`
  - `ArrowMaterializationMode`
  - `NativeWriteBatchSize`
  - `CreatedBy`
  - `FileMetadata`
- Pitfalls:
  - `NativeWriteBatchSize` is an advanced native encoder knob, not a managed batch splitter
  - callers own public `WriteBatch(...)` sizes

## Type Mapping

| Column kind | Schema API | Write CLR input | CLR read output |
| --- | --- | --- | --- |
| Primitive numeric | `ParquetColumnType.Int32`, etc. | matching arrays | matching arrays |
| String | `ParquetColumnType.String` | `string?[]` | `string[]` with possible nulls |
| Binary | `ParquetColumnType.Binary` | `byte[][]` | `byte[][]` |
| GUID | `ParquetColumnType.Guid` | `Guid[]` | `Guid[]` |
| Date | `Date32` / `Date64` | `DateOnly` on net8.0, `DateTime` on netstandard2.0 | `DateOnly` on net8.0, `DateTime` on netstandard2.0 |
| Timestamp | `ParquetTimestampSettings` | `DateTime` | `DateTime` |
| Decimal | `ParquetDecimalSettings` | `decimal` | `SqlDecimal` |

## Error API

- `NativeParquetException`: managed exception for stable native failures.
- `NativeErrorCode`: stable error categories such as `SchemaMismatch`, `SinkWriteFailed`, `ParquetEncodeFailed`, and `SourceReadFailed`.
- Recommended pattern: catch `NativeParquetException` around write/read boundaries where caller input, stream failures, or native encoding can fail.

## Compatibility Notes

- `net8.0` date columns use `DateOnly` / `DateOnly?` for CLR materialization.
- `netstandard2.0` date columns use `DateTime` / `DateTime?` because `DateOnly` is unavailable.
- The `net472` test target validates the `netstandard2.0` asset.
