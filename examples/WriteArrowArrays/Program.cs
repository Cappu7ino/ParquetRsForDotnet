using Apache.Arrow;
using Apache.Arrow.Memory;
using ParquetRsForDotnet;

var outputPath = Path.Combine(Path.GetTempPath(), "orders-arrow.parquet");
var allocator = MemoryAllocator.Default.Value;
var schema = new ParquetSchema(new[]
{
    new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
    new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
});

using var ids = new Int32Array.Builder().Append(10).AppendNull().Append(30).Build(allocator);
using var names = new StringArray.Builder().Append("alpha").AppendNull().Append("gamma").Build(allocator);
using var output = File.Create(outputPath);
using var writer = new ParquetFileWriter(output, schema);

writer.WriteBatch(ids, names);
writer.Finish();

Console.WriteLine(outputPath);
