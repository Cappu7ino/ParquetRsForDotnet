using ParquetRsForDotnet;

var outputPath = Path.Combine(Path.GetTempPath(), "orders-clr.parquet");
var schema = new ParquetSchema(new[]
{
    new ParquetColumn("id", ParquetColumnType.Int32),
    new ParquetColumn("customer", ParquetColumnType.String, isNullable: true),
    new ParquetColumn("amount", new ParquetDecimalSettings(18, 2), isNullable: true),
    new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC")),
});

using var output = File.Create(outputPath);
using var writer = new ParquetFileWriter(output, schema, new ParquetWriteOptions
{
    Compression = ParquetCompression.Zstd,
    MaxRowGroupRows = 100_000,
    CreatedBy = "WriteClrBatches example",
});

writer.WriteBatch(
    new[] { 1, 2, 3 },
    new string?[] { "northwind", null, "contoso" },
    new decimal?[] { 10.5m, null, 30.25m },
    new[]
    {
        new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc),
        new DateTime(2024, 1, 1, 2, 0, 0, DateTimeKind.Utc),
        new DateTime(2024, 1, 1, 3, 0, 0, DateTimeKind.Utc),
    });

writer.Finish();
Console.WriteLine(outputPath);
