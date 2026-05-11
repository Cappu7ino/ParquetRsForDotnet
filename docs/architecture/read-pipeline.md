# Read Pipeline

## Flow

```text
seekable Stream
  -> ManagedParquetSource callbacks
  -> Rust parquet metadata reader
  -> row-group scoped projection
  -> Arrow C Data export
  -> managed Arrow import
  -> IArrowArray or CLR array
```

## Invariants

- Input stream must be seekable.
- Metadata is loaded when `ParquetFileReader` is constructed.
- Reads are scoped to one row group and one column.
- Projection should happen before Arrow data crosses back to managed code.

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
