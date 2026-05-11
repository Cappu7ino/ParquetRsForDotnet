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
