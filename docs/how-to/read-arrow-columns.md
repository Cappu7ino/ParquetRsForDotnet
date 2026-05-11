# How To Read Arrow Columns

## Scenario

Use Arrow reads when the consuming pipeline can operate on Arrow arrays directly.

```csharp
using Apache.Arrow;

using var input = File.OpenRead("orders.parquet");
using var reader = new ParquetFileReader(input);

for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
{
    using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);
    using var ids = rowGroup.ReadColumn("id");
}
```

## Pitfalls

- Input stream must be seekable.
- Read selected columns rather than all columns when possible.
- Cast returned `IArrowArray` to the expected Arrow array type only after checking schema.

## Large Columns

Use batched reads to reduce peak memory. `BatchSize` controls how many rows each returned Arrow array contains; without a row range, the reader still walks the full selected column in the opened row group.

```csharp
using var input = File.OpenRead("orders.parquet");
using var reader = new ParquetFileReader(input, new ParquetReadOptions { BatchSize = 8192 });
using var rowGroup = reader.OpenRowGroupReader(0);

foreach (var batch in rowGroup.ReadColumnBatches("id"))
{
    using (batch)
    {
        // Process one Arrow array batch.
    }
}
```

Use row-range batched reads when the consumer only needs a slice of a row group. The row range limits which input rows are read, and `BatchSize` still controls how that selected slice is chunked into returned arrays.

```csharp
foreach (var batch in rowGroup.ReadColumnBatches("id", rowOffset: 10_000, rowCount: 5_000))
{
    using (batch)
    {
        // Process one selected Arrow array batch.
    }
}
```

The row range is relative to the opened row group, not the full file. `rowOffset` is zero-based, `rowCount` is the number of rows to read, and the range must fit within `rowGroup.RowCount`.
