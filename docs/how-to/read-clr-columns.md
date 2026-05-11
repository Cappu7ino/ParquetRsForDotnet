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

Use batched CLR reads to reduce peak memory:

```csharp
using var input = File.OpenRead("orders.parquet");
using var reader = new ParquetFileReader(input, new ParquetReadOptions { BatchSize = 8192 });
using var rowGroup = reader.OpenRowGroupReader(0);

foreach (int[] idBatch in rowGroup.ReadColumnBatches<int>("id"))
{
    // Process one managed array batch.
}
```

Use row-range batched reads to materialize only a slice of a row group:

```csharp
foreach (int[] idBatch in rowGroup.ReadColumnBatches<int>("id", rowOffset: 10_000, rowCount: 5_000))
{
    // Process one selected managed array batch.
}
```

The row range is relative to the opened row group. `rowOffset` and `rowCount` must be non-negative, and the range must fit within `rowGroup.RowCount`.
