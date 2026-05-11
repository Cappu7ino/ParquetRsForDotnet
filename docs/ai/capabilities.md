# Capabilities

## Write CLR Column Batches

- Purpose: write parquet from managed arrays.
- Recommended usage: create `ParquetSchema`, then call `WriteBatch(...)` with one array per schema column.
- Performance expectations: efficient for already-columnar input; avoid row-at-a-time calls.
- Constraints: strict schema order, equal column lengths, supported CLR type mapping only.

## Write Arrow Arrays

- Purpose: write Arrow-native data without CLR re-materialization.
- Recommended usage: pass `IArrowArray` columns matching the public schema.
- Performance expectations: avoids builder-based CLR materialization.
- Constraints: Arrow type must match mapped schema exactly.

## Read Arrow Columns

- Purpose: consume parquet as Arrow arrays.
- Recommended usage: open `ParquetRowGroupReader` and call `ReadColumn(...)` by index or name.
- Performance expectations: uses parquet-rs column projection for the selected row-group column.
- Constraints: one column at a time; caller owns returned Arrow array disposal where applicable.

## Read CLR Columns

- Purpose: materialize selected columns into managed arrays.
- Recommended usage: call `ReadColumn<T>(...)` with the exact expected CLR type.
- Performance expectations: allocates managed arrays for the selected column only.
- Constraints: decimal columns materialize as `SqlDecimal`; date columns depend on target framework.

## Read Column Batches

- Purpose: reduce peak memory when reading large row-group columns.
- Recommended usage: pass `ParquetReadOptions { BatchSize = ... }` to `ParquetFileReader`, then call `ReadColumnBatches(...)` or `ReadColumnBatches<T>(...)`.
- Performance expectations: parquet-rs returns projected column data in smaller record batches.
- Constraints: existing `ReadColumn(...)` APIs still return the full row-group column and ignore the batch size for their public return shape.

## Configure Writer Properties

- Purpose: control parquet layout and metadata.
- Recommended usage: use `MaxRowGroupRows` or `MaxRowGroupBytes` for row-group sizing; use `Compression` and `StatisticsLevel` for file characteristics.
- Performance expectations: row-group sizing affects memory and reader scan granularity.
- Constraints: `NativeWriteBatchSize` is an advanced encoder knob, not a managed batch splitter.

## Target Framework Compatibility

- Purpose: support modern .NET and .NET Framework consumers.
- Recommended usage: use `net8.0` when available; .NET Framework 4.7.2 consumes the `netstandard2.0` asset.
- Constraints: `DateOnly` is unavailable to `netstandard2.0` consumers.
