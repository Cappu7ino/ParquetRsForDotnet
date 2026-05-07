#if NET472
using System.Data.SqlTypes;

using Apache.Arrow;
using Apache.Arrow.Memory;

namespace ParquetRsForDotnet.Tests;

public sealed class NetStandardCompatibilityTests
{
    [Fact]
    public void FileReader_ExposesSchemaAndRowGroupCount()
    {
        using (var stream = CreateMixedRoundTripFile())
        using (var reader = new ParquetFileReader(stream))
        {
            Assert.Equal(1, reader.RowGroupCount);
            Assert.Equal(6, reader.Schema.Columns.Count);
            Assert.Equal("id", reader.Schema.Columns[0].Name);
            Assert.Equal(ParquetColumnType.Int32, reader.Schema.Columns[0].ColumnType);
            Assert.False(reader.Schema.Columns[0].IsNullable);
            Assert.Equal("name", reader.Schema.Columns[1].Name);
            Assert.Equal(ParquetColumnType.String, reader.Schema.Columns[1].ColumnType);
            Assert.True(reader.Schema.Columns[1].IsNullable);
            Assert.Equal("businessDate", reader.Schema.Columns[2].Name);
            Assert.Equal(ParquetColumnType.Date32, reader.Schema.Columns[2].ColumnType);
            Assert.Equal("snapshotDate", reader.Schema.Columns[3].Name);
            Assert.Equal(ParquetColumnType.Date64, reader.Schema.Columns[3].ColumnType);
            Assert.Equal("created", reader.Schema.Columns[4].Name);
            Assert.Equal(ParquetColumnType.Timestamp, reader.Schema.Columns[4].ColumnType);
            Assert.Equal(ParquetTimestampUnit.Millisecond, reader.Schema.Columns[4].TimestampSettings?.Unit);
            Assert.Equal("amount", reader.Schema.Columns[5].Name);
            Assert.True(reader.Schema.Columns[5].ColumnType is ParquetColumnType.Decimal128 or ParquetColumnType.Guid);
        }
    }

    [Fact]
    public void RowGroupReader_ReadsClrColumnsByIndexAndName()
    {
        using (var stream = CreateMixedRoundTripFile())
        using (var reader = new ParquetFileReader(stream))
        using (var rowGroup = reader.OpenRowGroupReader(0))
        {
            var ids = rowGroup.ReadColumn<int>(0);
            var names = rowGroup.ReadColumn<string>("name");
            var businessDates = rowGroup.ReadColumn<DateTime?>("businessDate");
            var snapshotDates = rowGroup.ReadColumn<DateTime>("snapshotDate");
            var created = rowGroup.ReadColumn<DateTime?>("created");
            var amounts = rowGroup.ReadColumn<SqlDecimal?>("amount");

            Assert.Equal(new[] { 1, 2, 3 }, ids);
            Assert.Equal(new string?[] { "alpha", null, "gamma" }, names);
            Assert.Equal(new DateTime?[] { new DateTime(2024, 8, 1), null, new DateTime(2024, 8, 3) }, businessDates);
            Assert.Equal(new[] { new DateTime(2024, 9, 1), new DateTime(2024, 9, 2), new DateTime(2024, 9, 3) }, snapshotDates);
            Assert.Equal(new DateTime?[] { new DateTime(2024, 10, 1, 1, 2, 3, DateTimeKind.Utc), null, new DateTime(2024, 10, 3, 4, 5, 6, DateTimeKind.Utc) }, created);
            Assert.Equal(new SqlDecimal?[] { new SqlDecimal(10.5m), null, new SqlDecimal(30.25m) }, amounts);
        }
    }

    [Fact]
    public void FileWriter_WritesMultipleManagedBatchesInSchemaOrder()
    {
        var schema = new ParquetSchema(new[]
        {
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
        });

        using (var stream = new MemoryStream())
        {
            using (var writer = new ParquetFileWriter(stream, schema))
            {
                writer.WriteBatch(new int?[] { 1, 2 }, new string?[] { "first", "second" });
                writer.WriteBatch(new int?[] { 3, null }, new string?[] { "third", null });
                writer.Finish();
            }

            stream.Position = 0;
            using (var reader = new ParquetFileReader(stream))
            using (var rowGroup = reader.OpenRowGroupReader(0))
            {
                Assert.Equal(new int?[] { 1, 2, 3, null }, rowGroup.ReadColumn<int?>("id"));
                Assert.Equal(new string?[] { "first", "second", "third", null }, rowGroup.ReadColumn<string>("name"));
            }
        }
    }

    [Fact]
    public void FileWriter_WritesArrowBatches()
    {
        var allocator = MemoryAllocator.Default.Value;
        var schema = new ParquetSchema(new[]
        {
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
        });

        using (var stream = new MemoryStream())
        using (var writer = new ParquetFileWriter(stream, schema))
        using (var id = new Int32Array.Builder().Append(10).AppendNull().Append(30).Build(allocator))
        using (var name = new StringArray.Builder().Append("delta").AppendNull().Append("omega").Build(allocator))
        {
            writer.WriteBatch(id, name);
            writer.Finish();

            stream.Position = 0;
            using (var reader = new ParquetFileReader(stream))
            using (var rowGroup = reader.OpenRowGroupReader(0))
            {
                Assert.Equal(new int?[] { 10, null, 30 }, rowGroup.ReadColumn<int?>("id"));
                Assert.Equal(new string?[] { "delta", null, "omega" }, rowGroup.ReadColumn<string>("name"));
            }
        }
    }

    [Fact]
    public void FileWriter_WritesNullableFixedWidthManagedBatches_WhenLowLevelFixedWidthIsRequested()
    {
        var schema = new ParquetSchema(new[]
        {
            new ParquetColumn("i32", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("f64", ParquetColumnType.Float64, isNullable: true),
            new ParquetColumn("eventDate", ParquetColumnType.Date32, isNullable: true),
            new ParquetColumn("eventTime", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
        });

        using (var stream = new MemoryStream())
        using (var writer = new ParquetFileWriter(stream, schema, new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth
        }))
        {
            writer.WriteBatch(
                new int?[] { 10, null, 30, null },
                new double?[] { 1.25d, null, 3.5d, 4.75d },
                new DateTime?[] { new DateTime(2024, 5, 1), null, new DateTime(2024, 5, 3), new DateTime(2024, 5, 4) },
                new DateTime?[]
                {
                    new DateTime(2024, 6, 1, 1, 2, 3, DateTimeKind.Utc),
                    null,
                    new DateTime(2024, 6, 1, 4, 5, 6, DateTimeKind.Utc),
                    new DateTime(2024, 6, 1, 7, 8, 9, DateTimeKind.Utc),
                });
            writer.Finish();

            stream.Position = 0;
            using (var reader = new ParquetFileReader(stream))
            using (var rowGroup = reader.OpenRowGroupReader(0))
            {
                Assert.Equal(new int?[] { 10, null, 30, null }, rowGroup.ReadColumn<int?>("i32"));
                Assert.Equal(new double?[] { 1.25d, null, 3.5d, 4.75d }, rowGroup.ReadColumn<double?>("f64"));
                Assert.Equal(new DateTime?[] { new DateTime(2024, 5, 1), null, new DateTime(2024, 5, 3), new DateTime(2024, 5, 4) }, rowGroup.ReadColumn<DateTime?>("eventDate"));
                Assert.Equal(
                    new DateTime?[]
                    {
                        new DateTime(2024, 6, 1, 1, 2, 3, DateTimeKind.Utc),
                        null,
                        new DateTime(2024, 6, 1, 4, 5, 6, DateTimeKind.Utc),
                        new DateTime(2024, 6, 1, 7, 8, 9, DateTimeKind.Utc),
                    },
                    rowGroup.ReadColumn<DateTime?>("eventTime"));
            }
        }
    }

    [Fact]
    public void FileWriter_RejectsSchemaOrderViolations()
    {
        var schema = new ParquetSchema(new[]
        {
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
        });

        using (var stream = new MemoryStream())
        using (var writer = new ParquetFileWriter(stream, schema))
        {
            var exception = Assert.Throws<NativeParquetException>(() => writer.WriteBatch(new string?[] { "wrong" }, new int?[] { 1 }));
            Assert.Equal(NativeErrorCode.SchemaMismatch, exception.ErrorCode);
            Assert.Contains("CLR element type", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RowGroupReader_RejectsMismatchedClrType()
    {
        using (var stream = CreateMixedRoundTripFile())
        using (var reader = new ParquetFileReader(stream))
        using (var rowGroup = reader.OpenRowGroupReader(0))
        {
            var exception = Assert.Throws<InvalidOperationException>(() => rowGroup.ReadColumn<long>("id"));
            Assert.Contains("expects CLR type", exception.Message, StringComparison.Ordinal);
        }
    }

    private static MemoryStream CreateMixedRoundTripFile()
    {
        var schema = new ParquetSchema(new[]
        {
            new ParquetColumn("id", ParquetColumnType.Int32),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
            new ParquetColumn("businessDate", ParquetColumnType.Date32, isNullable: true),
            new ParquetColumn("snapshotDate", ParquetColumnType.Date64),
            new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
            new ParquetColumn("amount", new ParquetDecimalSettings(18, 2), isNullable: true),
        });

        var stream = new MemoryStream();
        using (var writer = new ParquetFileWriter(stream, schema))
        {
            writer.WriteBatch(
                new[] { 1, 2, 3 },
                new string?[] { "alpha", null, "gamma" },
                new DateTime?[] { new DateTime(2024, 8, 1), null, new DateTime(2024, 8, 3) },
                new[] { new DateTime(2024, 9, 1), new DateTime(2024, 9, 2), new DateTime(2024, 9, 3) },
                new DateTime?[]
                {
                    new DateTime(2024, 10, 1, 1, 2, 3, DateTimeKind.Utc),
                    null,
                    new DateTime(2024, 10, 3, 4, 5, 6, DateTimeKind.Utc),
                },
                new decimal?[] { 10.5m, null, 30.25m });
            writer.Finish();
        }

        stream.Position = 0;
        return stream;
    }
}
#endif
