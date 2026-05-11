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

Use batched reads to reduce peak memory:

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
