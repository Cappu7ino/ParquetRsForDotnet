# Read/Write Architecture

This document is for contributors working on the parquet interop pipeline. The public README stays customer-facing; this note goes deeper into implementation details, ownership, and extension points.

## Scope

Current scope covers Arrow-native parquet read and write.

- public schema-driven batch writing
- explicit row-group/column reading
- managed CLR-array materialization into Arrow arrays for writes
- Arrow C Data export/import
- Rust parquet encoding and decoding
- managed sink/source callbacks

## Layer Responsibilities

### Managed layer (`src/`)

The managed layer owns:

- public write API
- public read API
- public schema definition and validation
- CLR-array materialization into Arrow arrays for writes
- Arrow schema and record-batch export through Arrow C Data for writes
- Arrow schema and array import through Arrow C Data for reads
- sink callback table over `Stream` for writes
- seekable source callback table over `Stream` for reads
- native option marshalling

Important internal types:

- `ParquetFileWriter`
- `ParquetFileReader`
- `ParquetRowGroupReader`
- `ArrowRecordBatchBuilder`
- `ClrArrayMaterializer`
- `ArrowSchemaExporter`
- `ManagedParquetSink`
- `ManagedParquetSource`
- `NativeParquetBridge`

### Native layer (`native/`)

The native Rust layer owns:

- Arrow schema import/export
- Arrow record-batch import for writes
- Arrow array export for reads
- `WriterProperties` construction
- parquet encoding with `ArrowWriter`
- parquet reading with `parquet-rs` Arrow readers
- row-group and column projection
- writing encoded bytes into sink callbacks
- reading parquet bytes through source callbacks
- native error projection

Important Rust types/functions:

- `ParquetWriteOptionsFFI`
- `ParquetOutputSink`
- `ParquetInputSource`
- `SinkWriter`
- `SourceReader`
- `SourceChunkReader`
- `parquet_file_writer_create`
- `parquet_file_writer_write_batch`
- `parquet_file_writer_finish`
- `parquet_file_writer_dispose`
- `parquet_file_reader_open`
- `parquet_file_reader_get_schema`
- `parquet_file_reader_get_row_group_count`
- `parquet_file_reader_open_row_group`
- `parquet_row_group_reader_get_row_count`
- `parquet_row_group_reader_read_column`
- `parquet_row_group_reader_dispose`
- `parquet_file_reader_dispose`

## End-to-End Flow

### Write

```text
CLR arrays / IArrowArray
  -> ParquetFileWriter
  -> ArrowRecordBatchBuilder
  -> Arrow C Data export
  -> Rust Arrow FFI import
  -> parquet::arrow::ArrowWriter
  -> ManagedParquetSink
  -> Stream
```

### Read

```text
Stream
  -> ManagedParquetSource
  -> callback-backed SourceChunkReader
  -> parquet-rs Arrow reader
  -> Arrow C Data export
  -> managed Arrow import
  -> ParquetRowGroupReader.ReadColumn(...)
```

## Ownership Rules

### Managed Arrow objects

Managed Arrow arrays and record batches are created per write batch and exported through Arrow C Data.

Key rule:

- the managed `RecordBatch` passed into native export is owned by the bridge for the duration of the native write call and disposed when the call completes

### Native Arrow read exports

Native read paths export Arrow arrays through Arrow C Data and managed code immediately imports them.

Key rule:

- managed code owns the imported Arrow object after a successful native read call

### Native sink callbacks

`ManagedParquetSink` allocates a `GCHandle` to itself and passes that handle as the sink context.

Key rules:

- the sink object must outlive the native writer handle
- the destination `Stream` is still caller-owned
- sink callbacks must never assume the stream is disposed by native code

### Native source callbacks

`ManagedParquetSource` allocates a `GCHandle` to itself and exposes random-access callbacks for parquet reads.

Key rules:

- the source object must outlive the native file reader handle
- input streams must be seekable
- source callbacks are expected to support footer and column-chunk reads at arbitrary offsets

### Native option marshalling

`NativeParquetBridge.NativeOptionScope` owns marshalled UTF-8 strings and metadata buffers for the duration of a native call.

Key rule:

- any pointer placed into `ParquetWriteOptionsNative` must remain valid until the native call returns

## Public Schema Mapping

The public schema path is centralized in `PublicSchemaMapper` for writes and `ParquetSchemaImporter` for reads.

### Defaults

- `Guid` -> `FixedSizeBinary(16)`
- `DateOnly` -> `Date32` or `Date64`
- `DateTime` -> configured `Timestamp`
- `decimal` -> configured `Decimal128(precision, scale)`

The write API requires callers to provide `ParquetSchema` explicitly. The read API projects native Arrow schema back into `ParquetSchema`.

## Materialization Strategy

`ArrowRecordBatchBuilder` handles two write-batch inputs:

- `IReadOnlyList<Array>`
- `IReadOnlyList<IArrowArray>`

For CLR arrays, `ClrArrayMaterializer` converts known managed array shapes into Arrow arrays.

Current handled shapes include:

- primitive arrays
- nullable primitive arrays
- `string?[]`
- `decimal[]` / `decimal?[]`
- `DateOnly[]` / `DateOnly?[]`
- `DateTime[]` / `DateTime?[]`
- `Guid[]`
- `byte[][]`

`ArrowMaterializationMode.LowLevelFixedWidth` is used to bypass builder-based construction for supported fixed-width shapes.

## Writer Lifecycle

`ParquetFileWriter` is stateful and appendable.

Lifecycle:

1. validate schema and create native writer with imported Arrow schema
2. for each `WriteBatch(...)`
   - validate column count/order/types/length
   - materialize CLR arrays if needed
   - export `RecordBatch` through Arrow C Data
   - import and write the batch in Rust
3. `Finish()` finalizes parquet output
4. `Dispose()` releases native resources and sink state

## Reader Lifecycle

`ParquetFileReader` opens a seekable parquet source and exposes explicit row-group readers.

Lifecycle:

1. create `ManagedParquetSource` over a seekable `Stream`
2. open native file reader and load parquet metadata once
3. import native Arrow schema and project it into `ParquetSchema`
4. open `ParquetRowGroupReader` for a chosen row group
5. for each `ReadColumn(...)`
   - project one row group and one column in Rust
   - export Arrow array through Arrow C Data
   - import Arrow array in managed code
6. dispose row-group reader, then file reader, then source bridge

## Current Constraints

These constraints are intentional, not accidental.

- writes are synchronous end-to-end
- reads are explicit by row group and column
- schema order is strict for writes
- batch width must match schema width exactly for writes
- no public row-group API for writes
- no async sink/source callbacks
- read v1 is Arrow-native only

Keeping these constraints explicit helps preserve a small and understandable interop surface.

## Why tests use external parquet producers

Reader tests intentionally use external parquet source files produced by `ParquetSharp` so read compatibility is validated independently of our writer implementation.

The test suite now covers:

- write-path correctness verified through `Parquet.Net`
- read-path compatibility verified through `ParquetSharp`
- internal end-to-end Arrow-native interop behavior

## Extension Points

The most likely future extension points are:

- richer public schema options
- lower-level array materialization paths for more write types
- richer native writer options
- `ReadColumn(string name)` convenience and additional read ergonomics
- Arrow-native full row-group `RecordBatch` reads
- optional Arrow-to-CLR materialization for reads
- async sink/source support

## Contributor Notes

When changing the interop pipeline:

1. keep the managed/native ownership rules explicit
2. preserve appendable write semantics
3. avoid introducing whole-file buffering in the read path
4. use row-group/column projection instead of over-reading where possible
5. add external-producer compatibility assertions whenever read semantics change
6. prefer small focused helpers over reintroducing generalized abstraction layers
