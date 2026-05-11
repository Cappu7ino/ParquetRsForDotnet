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

## `ParquetReadOptions`

- Responsibility: configure read behavior.
- Active options:
  - `BatchSize`: maximum row count per array yielded by batched column reads.
- Pitfalls:
  - `BatchSize` affects `ReadColumnBatches(...)` APIs, not the public shape of full-column `ReadColumn(...)` APIs.
  - values must be greater than zero.

## `ParquetRowGroupReader`

- Responsibility: read Arrow-native or CLR-materialized columns from one row group.
- Lifecycle: created by `ParquetFileReader.OpenRowGroupReader(...)`, used for column reads, disposed.
- Thread safety: not documented as thread-safe.
- Active operations:
  - `ReadColumn(int)` / `ReadColumn(string)` returning Arrow arrays
  - `ReadColumn<T>(int)` / `ReadColumn<T>(string)` returning CLR arrays
  - `ReadColumnBatches(int)` / `ReadColumnBatches(string)` returning Arrow array batches
  - `ReadColumnBatches(int, long, long)` / `ReadColumnBatches(string, long, long)` returning Arrow array batches for a row range
  - `ReadColumnBatches<T>(int)` / `ReadColumnBatches<T>(string)` returning CLR array batches
  - `ReadColumnBatches<T>(int, long, long)` / `ReadColumnBatches<T>(string, long, long)` returning CLR array batches for a row range
- Intended usage: read only the columns needed from a specific row group.
- Intended row-range usage: positional windows, pagination-style access, retry/resume, sampling, and deterministic row-group chunking when row offsets are already known.
- Pitfalls:
  - `ReadColumn<T>` validates exact CLR type
  - `ReadColumn(...)` and `ReadColumn<T>(...)` return the entire selected row-group column
  - use `ReadColumnBatches(...)`, `ReadColumnBatches(..., rowOffset, rowCount)`, or CLR equivalents for lower peak memory
  - `BatchSize` chunks returned arrays; row-range overloads additionally limit which input rows are read
  - row-range batched reads are scoped to the opened row group and validate that the range is within `RowCount`
  - row-range reads are positional selection, not predicate pushdown
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
  - `DataPageRowCountLimit`
  - `DataPageSizeLimitBytes`
  - `DictionaryPageSizeLimitBytes`
  - `MaxNativeWriterMemoryBytes`
  - `Compression`
  - `EnableDictionaryEncoding`
  - `StatisticsLevel`
  - `ArrowMaterializationMode`
  - `NativeWriteBatchSize`
  - `CreatedBy`
  - `FileMetadata`
- Pitfalls:
  - `NativeWriteBatchSize` is an advanced parquet-rs encoder chunking knob, not a managed batch splitter, page size, or row-group boundary
  - `MaxNativeWriterMemoryBytes` is an estimated native writer threshold checked after each managed batch, not a hard process memory limit
  - smaller page and row-group settings may increase metadata overhead and reduce compression efficiency
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

## Batched Reads

- `ReadColumnBatches(int|string)` returns an enumerable of Arrow arrays for one projected row-group column.
- `ReadColumnBatches<T>(int|string)` returns an enumerable of CLR arrays for one projected row-group column.
- Each yielded Arrow array should be disposed after processing.
- CLR batch arrays are managed arrays and do not need disposal.

## Error API

- `NativeParquetException`: managed exception for stable native failures.
- `NativeErrorCode`: stable error categories such as `SchemaMismatch`, `SinkWriteFailed`, `ParquetEncodeFailed`, and `SourceReadFailed`.
- Recommended pattern: catch `NativeParquetException` around write/read boundaries where caller input, stream failures, or native encoding can fail.

## Compatibility Notes

- `net8.0` date columns use `DateOnly` / `DateOnly?` for CLR materialization.
- `netstandard2.0` date columns use `DateTime` / `DateTime?` because `DateOnly` is unavailable.
- The `net472` test target validates the `netstandard2.0` asset.
