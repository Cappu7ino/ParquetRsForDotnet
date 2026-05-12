using System.Data.SqlTypes;
using ParquetRsForDotnet;

var inputPath = args.Length == 0 ? Path.Combine(Path.GetTempPath(), "orders-clr.parquet") : args[0];

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    Console.Error.WriteLine("Run examples/WriteClrBatches first or pass a parquet file path.");
    return 1;
}

using var input = File.OpenRead(inputPath);
using var reader = new ParquetFileReader(input);

for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
{
    using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex, "id", "amount");
    int[] ids = rowGroup.ReadColumn<int>("id");
    SqlDecimal?[] amounts = rowGroup.ReadColumn<SqlDecimal?>("amount");

    Console.WriteLine($"row group {rowGroupIndex}: {ids.Length} ids, {amounts.Length} amounts");
}

return 0;
