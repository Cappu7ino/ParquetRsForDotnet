# Write Architecture

This document is for contributors working on the parquet write pipeline. The public README stays customer-facing; this note goes deeper into implementation details, ownership, and extension points.

## Scope

Current scope covers parquet write only.

- public schema-driven batch writing
- managed CLR-array materialization into Arrow arrays
- Arrow C Data export
- Rust parquet encoding
- managed sink callbacks

Read APIs are intentionally out of scope for now.

## Layer Responsibilities

### Managed layer (`src/`)

The managed layer owns:

- public writer API
- public schema definition and validation
- CLR-array materialization into Arrow arrays
- Arrow schema and record-batch export through Arrow C Data
- sink callback table over `Stream`
- native option marshalling

Important internal types:

- `ParquetFileWriter`
- `ArrowBatchBuilder`
- `ClrArrayMaterializer`
- `ArrowSchemaExporter`
- `ManagedParquetSink`
- `NativeParquetBridge`

### Native layer (`native/`)

The native Rust layer owns:

- Arrow schema import
- Arrow record-batch import
- `WriterProperties` construction
- parquet encoding with `ArrowWriter`
- writing encoded bytes into the sink callback table
- native error projection

Important Rust types/functions:

- `ParquetWriteOptionsFFI`
- `ParquetOutputSink`
- `SinkWriter`
- `parquet_file_writer_create`
- `parquet_file_writer_write_batch`
- `parquet_file_writer_finish`
- `parquet_file_writer_dispose`

## End-to-End Flow

```text
CLR arrays / IArrowArray
  -> ParquetFileWriter
  -> ArrowBatchBuilder
  -> Arrow C Data export
  -> Rust Arrow FFI import
  -> parquet::arrow::ArrowWriter
  -> ManagedParquetSink
  -> Stream
```

## Ownership Rules

### Managed Arrow objects

Managed Arrow arrays and record batches are created per batch and then exported through Arrow C Data.

Key rule:

- the managed `RecordBatch` passed into native export is owned by the bridge for the duration of the native write call and disposed when the call completes

### Native sink callbacks

`ManagedParquetSink` allocates a `GCHandle` to itself and passes that handle as the sink context.

Key rules:

- the sink object must outlive the native writer handle
- the destination `Stream` is still caller-owned
- sink callbacks must never assume the stream is disposed by native code

### Native option marshalling

`NativeParquetBridge.NativeOptionScope` owns marshalled UTF-8 strings and metadata buffers for the duration of a native call.

Key rule:

- any pointer placed into `ParquetWriteOptionsNative` must remain valid until the native call returns

## Public Schema Mapping

The public schema path is centralized in `PublicSchemaMapper`.

### Defaults

- `Guid` -> `FixedSizeBinary(16)`
- `DateOnly` -> `Date32` or `Date64`
- `DateTime` -> configured `Timestamp`
- `decimal` -> configured `Decimal128(precision, scale)`

The public writer does not infer schema from external reader contracts. Callers provide the schema explicitly through `ParquetSchema` and `ParquetColumn`.

## Materialization Strategy

`ArrowBatchBuilder` handles two batch inputs:

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

## Current Constraints

These constraints are intentional, not accidental.

- writes are synchronous end-to-end
- schema order is strict
- batch width must match schema width exactly
- no public row-group API
- no async sink callbacks
- no read API yet

Keeping these constraints explicit helps preserve a small and understandable interop surface.

## Why the tests use `Parquet.Net`

The repository does not yet implement a managed read API. Tests therefore use `Parquet.Net` as an external verifier to ensure the produced parquet stream is readable and semantically correct.

That gives useful coverage for:

- parquet structure correctness
- round-trip values for common types
- metadata visibility
- multi-batch behavior

## Extension Points

The most likely future extension points are:

- richer public schema options
- lower-level array materialization paths for more types
- richer native writer options
- async sink support
- a public read API using a similar managed/native split

## Contributor Notes

When changing the write path:

1. keep the managed/native ownership rules explicit
2. preserve appendable streaming semantics
3. avoid introducing whole-file buffering accidentally
4. add read-back assertions in tests whenever parquet payload semantics change
5. prefer small focused helpers over reintroducing generalized reader-based abstractions
