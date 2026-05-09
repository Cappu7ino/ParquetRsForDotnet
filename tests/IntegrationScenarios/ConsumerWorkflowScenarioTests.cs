using System.Data.SqlTypes;

namespace ParquetRsForDotnet.Tests.IntegrationScenarios;

/// <summary>
/// Executable documentation for canonical consuming-repository workflows.
/// </summary>
public sealed class ConsumerWorkflowScenarioTests
{
    [Fact]
    public void ColumnBatchWriteThenProjectedRead_RoundTripsExpectedColumns()
    {
        var schema = new ParquetSchema(new[]
        {
            new ParquetColumn("id", ParquetColumnType.Int32),
            new ParquetColumn("amount", new ParquetDecimalSettings(18, 2), isNullable: true),
            new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC")),
        });

        using var stream = new MemoryStream();
        using (var writer = new ParquetFileWriter(stream, schema, new ParquetWriteOptions
        {
            MaxRowGroupRows = 100_000,
            Compression = ParquetCompression.Zstd,
        }))
        {
            writer.WriteBatch(
                new[] { 1, 2, 3 },
                new decimal?[] { 10.5m, null, 30.25m },
                new[]
                {
                    new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 1, 1, 2, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 1, 1, 3, 0, 0, DateTimeKind.Utc),
                });
            writer.Finish();
        }

        stream.Position = 0;
        using var reader = new ParquetFileReader(stream);
        using var rowGroup = reader.OpenRowGroupReader(0);

        Assert.Equal(new[] { 1, 2, 3 }, rowGroup.ReadColumn<int>("id"));
        Assert.Equal(new SqlDecimal?[] { new SqlDecimal(10.5m), null, new SqlDecimal(30.25m) }, rowGroup.ReadColumn<SqlDecimal?>("amount"));
    }

    [Fact]
    public void SchemaOrderViolation_ProducesStableNativeErrorCode()
    {
        var schema = new ParquetSchema(new[]
        {
            new ParquetColumn("id", ParquetColumnType.Int32),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
        });

        using var stream = new MemoryStream();
        using var writer = new ParquetFileWriter(stream, schema);

        var exception = Assert.Throws<NativeParquetException>(() => writer.WriteBatch(new string?[] { "wrong" }, new[] { 1 }));
        Assert.Equal(NativeErrorCode.SchemaMismatch, exception.ErrorCode);
    }
}
