# How To Read CLR Columns

## Scenario

Use CLR reads when the application needs managed arrays.

```csharp
using System.Data.SqlTypes;

using var input = File.OpenRead("orders.parquet");
using var reader = new ParquetFileReader(input);
using var rowGroup = reader.OpenRowGroupReader(0);

int[] ids = rowGroup.ReadColumn<int>("id");
SqlDecimal?[] amounts = rowGroup.ReadColumn<SqlDecimal?>("amount");
```

## Type Rules

- `ReadColumn<T>` requires exact expected `T`.
- Nullable value columns use nullable `T?`.
- Decimal columns use `SqlDecimal` / `SqlDecimal?`.
- Date columns use `DateOnly` on `net8.0` and `DateTime` on `netstandard2.0`.

## Large Columns

Use batched CLR reads to reduce peak memory. `BatchSize` controls how many rows each returned managed array contains; without a row range, the reader still walks the full selected column in the opened row group.

```csharp
using var input = File.OpenRead("orders.parquet");
using var reader = new ParquetFileReader(input, new ParquetReadOptions { BatchSize = 8192 });
using var rowGroup = reader.OpenRowGroupReader(0);

foreach (int[] idBatch in rowGroup.ReadColumnBatches<int>("id"))
{
    // Process one managed array batch.
}
```

Use row-range batched reads to materialize only a slice of a row group. The row range limits which input rows are read, and `BatchSize` still controls how that selected slice is chunked into returned arrays.

```csharp
foreach (int[] idBatch in rowGroup.ReadColumnBatches<int>("id", rowOffset: 10_000, rowCount: 5_000))
{
    // Process one selected managed array batch.
}
```

The row range is relative to the opened row group, not the full file. `rowOffset` is zero-based, `rowCount` is the number of rows to read, and the range must fit within `rowGroup.RowCount`.

Row-range reads are useful for windowed processing, pagination-style access, retry/resume from a known row offset, sampling/debugging a large row group, or splitting a row group into deterministic chunks for staged work.

Performance-wise, this is positional selection rather than predicate pushdown. It avoids materializing managed arrays for rows outside the selected range, but it may still need to inspect parquet metadata and skip through pages before the selected rows. Use it when the caller already knows the row offsets it needs; use ordinary batched reads when the caller intends to consume the whole row-group column.
