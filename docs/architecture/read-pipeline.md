# Read Pipeline

## Flow

```text
seekable Stream
  -> ManagedParquetSource callbacks
  -> Rust parquet metadata reader
  -> managed row-group projection contract
  -> parquet-rs column projection for the requested schema column
  -> Arrow C Data export
  -> managed Arrow import
  -> IArrowArray or CLR array
```

## Invariants

- Input stream must be seekable.
- Metadata is loaded when `ParquetFileReader` is constructed.
- Reads are scoped to one row group and one column.
- Row-group projection is optional and selected by schema column names when the row-group reader is opened.
- Integer read APIs always refer to original schema ordinals, not positions within a row-group projection.
- Managed code rejects reads outside a projected row-group reader before calling into native code.
- Native column reads use parquet-rs projection masks so unrequested columns do not cross back through Arrow C Data.

## Materialization Choices

- `ReadColumn(...)` returns Arrow arrays for Arrow-native consumers.
- `ReadColumn<T>(...)` materializes CLR arrays and validates exact CLR type.
- `ReadColumnBatches(...)` and `ReadColumnBatches<T>(...)` stream projected column data as smaller arrays when `ParquetReadOptions.BatchSize` is configured.
- Decimal CLR reads use `SqlDecimal`.
- Date CLR reads use `DateOnly` on `net8.0` and `DateTime` on `netstandard2.0`.

## Ownership

- Caller owns the input stream.
- `ParquetFileReader` owns the native file reader handle.
- `ParquetRowGroupReader` owns the native row-group reader handle.
- Managed code owns imported Arrow arrays after a successful read.
