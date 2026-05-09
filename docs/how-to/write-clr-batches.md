# How To Write CLR Batches

## Scenario

Use CLR arrays when your application data is already columnar or can be grouped into column arrays.

## Steps

1. Create `ParquetSchema` in output column order.
2. Open a writable stream.
3. Create `ParquetFileWriter`.
4. Call `WriteBatch(...)` with one array per schema column.
5. Call `Finish()`.

```csharp
var schema = new ParquetSchema(new[]
{
    new ParquetColumn("id", ParquetColumnType.Int32),
    new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
    new ParquetColumn("amount", new ParquetDecimalSettings(18, 2), isNullable: true),
});

using var output = File.Create("orders.parquet");
using var writer = new ParquetFileWriter(output, schema);

writer.WriteBatch(
    new[] { 1, 2, 3 },
    new string?[] { "first", null, "third" },
    new decimal?[] { 10.5m, null, 30.25m });

writer.Finish();
```

## Pitfalls

- Do not omit columns.
- Do not reorder columns.
- Do not mix row counts within one batch.
- Do not read decimal columns later as `decimal`; use `SqlDecimal` for CLR reads.
