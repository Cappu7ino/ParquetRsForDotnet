# How To Write Arrow Arrays

## Scenario

Use Arrow arrays when your pipeline already uses Apache.Arrow and you want to avoid CLR-array materialization.

```csharp
using Apache.Arrow;
using Apache.Arrow.Memory;

var allocator = MemoryAllocator.Default.Value;
var schema = new ParquetSchema(new[]
{
    new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
    new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
});

using var ids = new Int32Array.Builder().Append(1).AppendNull().Append(3).Build(allocator);
using var names = new StringArray.Builder().Append("alpha").AppendNull().Append("gamma").Build(allocator);

using var output = File.Create("arrow.parquet");
using var writer = new ParquetFileWriter(output, schema);
writer.WriteBatch(ids, names);
writer.Finish();
```

## Pitfalls

- Arrow array types must match the public schema mapping.
- Array lengths must match.
- Arrow arrays should remain alive until `WriteBatch(...)` returns.
