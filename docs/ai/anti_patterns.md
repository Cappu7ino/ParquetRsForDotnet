# Anti-Patterns

## Inferring Schema From Objects

- Incorrect because the SDK requires explicit `ParquetSchema`.
- Consequence: generated code invents missing APIs or maps types incorrectly.
- Correct alternative: build `ParquetSchema` explicitly with `ParquetColumn` instances.

## Writing Rows One At A Time

- Incorrect because `WriteBatch(...)` is columnar.
- Consequence: poor throughput and excessive allocations.
- Correct alternative: collect column arrays and write batches of many rows.

## Reading Decimal As `decimal`

- Incorrect because CLR decimal reads materialize as `SqlDecimal`.
- Consequence: `ReadColumn<decimal>` fails type validation.
- Correct alternative: use `ReadColumn<SqlDecimal>` or `ReadColumn<SqlDecimal?>`.

## Using `DateOnly` From .NET Framework

- Incorrect because `DateOnly` is unavailable on `netstandard2.0` / .NET Framework consumers.
- Consequence: consuming project does not compile.
- Correct alternative: use `DateTime` / `DateTime?` for `Date32` and `Date64` from .NET Framework.

## Treating `NativeWriteBatchSize` As Row Group Size

- Incorrect because it controls parquet-rs encoder chunking.
- Consequence: unexpected file layout and scan behavior.
- Correct alternative: use `MaxRowGroupRows` or `MaxRowGroupBytes` for row-group boundaries.

## Reading From Non-Seekable Streams

- Incorrect because parquet metadata and column chunks require random access.
- Consequence: reader construction fails.
- Correct alternative: provide a seekable stream or stage data into a seekable source before reading.

## Setting Read Batch Size But Calling `ReadColumn`

- Incorrect because `ReadColumn(...)` returns the full selected row-group column.
- Consequence: peak memory remains close to full-column materialization.
- Correct alternative: use `ReadColumnBatches(...)` or `ReadColumnBatches<T>(...)`.
