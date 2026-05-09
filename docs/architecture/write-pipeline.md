# Write Pipeline

## Flow

```text
CLR arrays / IArrowArray
  -> ParquetFileWriter
  -> ArrowRecordBatchBuilder
  -> Arrow C Data export
  -> Rust Arrow FFI import
  -> parquet-rs ArrowWriter
  -> ManagedParquetSink
  -> caller Stream
```

## Invariants

- The public schema is fixed when `ParquetFileWriter` is constructed.
- Every batch must include every schema column.
- Column order is positional and must match `ParquetSchema.Columns`.
- CLR arrays are converted to Arrow arrays before crossing the native boundary.
- `IArrowArray` inputs avoid CLR materialization but still must match schema type mapping.

## Row Groups

- Row-group boundaries are controlled by native parquet writer properties.
- Use `MaxRowGroupRows` or `MaxRowGroupBytes` to bound row groups.
- `NativeWriteBatchSize` controls parquet-rs encoder chunking, not row-group boundaries.

## Ownership

- Caller owns the output stream.
- Managed code owns temporary Arrow objects created for each batch.
- Native Rust owns parquet writer state until finish/dispose.
