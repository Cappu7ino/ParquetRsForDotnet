using System.Data.SqlTypes;
using Apache.Arrow;
using Apache.Arrow.Types;
using ParquetSharp;
using ParquetSharpFileWriter = ParquetSharp.ParquetFileWriter;
using ParquetSharpTimeUnit = ParquetSharp.TimeUnit;

namespace ParquetRsForDotnet.Tests;

public sealed class ParquetReaderTests
{
    [Fact]
    public void FileReader_ExposesSchemaAndRowGroupCount()
    {
        using var source = CreateTwoRowGroupMixedFile();
        using var reader = new ParquetFileReader(source);

        Assert.Equal(2, reader.RowGroupCount);
        Assert.Equal(4, reader.Schema.Columns.Count);
        Assert.Equal("id", reader.Schema.Columns[0].Name);
        Assert.Equal(ParquetColumnType.Int32, reader.Schema.Columns[0].ColumnType);
        Assert.True(reader.Schema.Columns[0].IsNullable);
        Assert.Equal("name", reader.Schema.Columns[1].Name);
        Assert.Equal(ParquetColumnType.String, reader.Schema.Columns[1].ColumnType);
        Assert.Equal("amount", reader.Schema.Columns[2].Name);
        if (reader.Schema.Columns[2].ColumnType == ParquetColumnType.Decimal128)
        {
            Assert.Equal(29, reader.Schema.Columns[2].DecimalSettings?.Precision);
            Assert.Equal(4, reader.Schema.Columns[2].DecimalSettings?.Scale);
        }
        else
        {
            Assert.Equal(ParquetColumnType.Guid, reader.Schema.Columns[2].ColumnType);
        }
        Assert.Equal("created", reader.Schema.Columns[3].Name);
        Assert.Equal(ParquetColumnType.Timestamp, reader.Schema.Columns[3].ColumnType);
        Assert.Equal(ParquetTimestampUnit.Millisecond, reader.Schema.Columns[3].TimestampSettings?.Unit);
        Assert.Equal("UTC", reader.Schema.Columns[3].TimestampSettings?.Timezone);
    }

    [Fact]
    public void RowGroupReader_ReadsPrimitiveAndStringColumns()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(1);

        var idColumn = Assert.IsType<Int32Array>(rowGroup.ReadColumn(0));
        var nameColumn = Assert.IsType<StringArray>(rowGroup.ReadColumn(1));

        Assert.Equal(3, rowGroup.RowCount);
        Assert.Equal(4, rowGroup.ColumnCount);
        Assert.Equal(3, idColumn.Length);
        Assert.Null(idColumn.GetValue(0));
        Assert.Equal(5, idColumn.GetValue(1));
        Assert.Equal(6, idColumn.GetValue(2));
        Assert.Equal("delta", nameColumn.GetString(0));
        Assert.Equal("epsilon", nameColumn.GetString(1));
        Assert.Equal("zeta", nameColumn.GetString(2));
    }

    [Fact]
    public void RowGroupReader_ReadsColumnByName()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(0);

        var nameColumn = Assert.IsType<StringArray>(rowGroup.ReadColumn("name"));
        Assert.Equal("alpha", nameColumn.GetString(0));
        Assert.Equal("beta", nameColumn.GetString(1));
        Assert.Equal("gamma", nameColumn.GetString(2));
    }

    [Fact]
    public void RowGroupReader_ReadsClrColumnsByIndexAndName()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(1);

        var ids = rowGroup.ReadColumn<int?>(0);
        var names = rowGroup.ReadColumn<string>("name");
        var created = rowGroup.ReadColumn<DateTime?>(2);
        var flags = rowGroup.ReadColumn<int?>("flag");

        Assert.Equal(new int?[] { null, 5, 6 }, ids);
        Assert.Equal(new string?[] { "delta", "epsilon", "zeta" }, names);
        Assert.Equal(new int?[] { null, 1, 0 }, flags);
        Assert.Equal(new DateTime(2024, 7, 2, 2, 0, 0, DateTimeKind.Utc), created[1]);
        Assert.Equal(new DateTime(2024, 7, 2, 3, 0, 0, DateTimeKind.Utc), created[2]);
        Assert.Null(created[0]);
    }

    [Fact]
    public void RowGroupReader_ReadsArrowColumnBatches_WhenReadBatchSizeIsConfigured()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source, new ParquetReadOptions { BatchSize = 2 });
        using var rowGroup = reader.OpenRowGroupReader(1);

        var batches = rowGroup.ReadColumnBatches(0).Cast<Int32Array>().ToArray();

        Assert.Equal(2, batches.Length);
        try
        {
            Assert.Equal(2, batches[0].Length);
            Assert.Equal(1, batches[1].Length);
            Assert.Null(batches[0].GetValue(0));
            Assert.Equal(5, batches[0].GetValue(1));
            Assert.Equal(6, batches[1].GetValue(0));
        }
        finally
        {
            foreach (var batch in batches)
            {
                batch.Dispose();
            }
        }
    }

    [Fact]
    public void RowGroupReader_ReadsClrColumnBatches_WhenReadBatchSizeIsConfigured()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source, new ParquetReadOptions { BatchSize = 2 });
        using var rowGroup = reader.OpenRowGroupReader(1);

        var batches = rowGroup.ReadColumnBatches<int?>("id").ToArray();

        Assert.Equal(2, batches.Length);
        Assert.Equal(new int?[] { null, 5 }, batches[0]);
        Assert.Equal(new int?[] { 6 }, batches[1]);
        Assert.Equal(new int?[] { null, 5, 6 }, batches.SelectMany(static batch => batch).ToArray());
    }

    [Fact]
    public void RowGroupReader_ReadsArrowColumnBatches_ForRowRange()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source, new ParquetReadOptions { BatchSize = 1 });
        using var rowGroup = reader.OpenRowGroupReader(1);

        var batches = rowGroup.ReadColumnBatches(0, rowOffset: 1, rowCount: 2).Cast<Int32Array>().ToArray();

        Assert.Equal(2, batches.Length);
        try
        {
            Assert.Equal(1, batches[0].Length);
            Assert.Equal(1, batches[1].Length);
            Assert.Equal(5, batches[0].GetValue(0));
            Assert.Equal(6, batches[1].GetValue(0));
        }
        finally
        {
            foreach (var batch in batches)
            {
                batch.Dispose();
            }
        }
    }

    [Fact]
    public void RowGroupReader_ReadsClrColumnBatches_ForRowRange()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source, new ParquetReadOptions { BatchSize = 1 });
        using var rowGroup = reader.OpenRowGroupReader(1);

        var batches = rowGroup.ReadColumnBatches<string>("name", rowOffset: 1, rowCount: 2).ToArray();

        Assert.Equal(2, batches.Length);
        Assert.Equal(new string?[] { "epsilon" }, batches[0]);
        Assert.Equal(new string?[] { "zeta" }, batches[1]);
        Assert.Equal(new string?[] { "epsilon", "zeta" }, batches.SelectMany(static batch => batch).ToArray());
    }

    [Fact]
    public void RowGroupReader_ValidatesColumnBatchRowRange()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(1);

        Assert.Empty(rowGroup.ReadColumnBatches(0, rowOffset: 1, rowCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rowGroup.ReadColumnBatches(0, rowOffset: -1, rowCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rowGroup.ReadColumnBatches(0, rowOffset: 0, rowCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rowGroup.ReadColumnBatches(0, rowOffset: 2, rowCount: 2));
    }

    [Fact]
    public void RowGroupReader_ReadsNullableStringColumnBatches_WhenNullsCrossBatchBoundaries()
    {
        using var source = CreateNullableStringBatchFile();
        using var reader = new ParquetFileReader(source, new ParquetReadOptions { BatchSize = 2 });
        using var rowGroup = reader.OpenRowGroupReader(0);

        var batches = rowGroup.ReadColumnBatches<string>("name").ToArray();

        Assert.Equal(3, batches.Length);
        Assert.Equal(new string?[] { "alpha", null }, batches[0]);
        Assert.Equal(new string?[] { "gamma", null }, batches[1]);
        Assert.Equal(new string?[] { "epsilon" }, batches[2]);
    }

    [Fact]
    public void FileReader_RejectsInvalidReadBatchSize()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ParquetFileReader(source, new ParquetReadOptions { BatchSize = 0 }));
        Assert.Equal("BatchSize", exception.ParamName);
    }

    [Fact]
    public void RowGroupReader_ReadsDecimalAndTimestampColumns()
    {
        using var source = CreateTwoRowGroupMixedFile();
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(0);

        var amountColumn = Assert.IsType<Decimal128Array>(rowGroup.ReadColumn(2));
        var createdColumn = Assert.IsType<TimestampArray>(rowGroup.ReadColumn(3));

        Assert.Equal(3, amountColumn.Length);
        Assert.Equal(10.5m, amountColumn.GetValue(0));
        Assert.Null(amountColumn.GetValue(1));
        Assert.Equal(30.25m, amountColumn.GetValue(2));

        Assert.Equal(3, createdColumn.Length);
        Assert.NotEqual(default, createdColumn.GetTimestampUnchecked(0));
        Assert.NotEqual(default, createdColumn.GetTimestampUnchecked(2));
        Assert.True(createdColumn.GetTimestampUnchecked(0) < createdColumn.GetTimestampUnchecked(2));
    }

    [Fact]
    public void RowGroupReader_ReadsClrSqlDecimalAndTimestampColumns()
    {
        using var source = CreateTwoRowGroupMixedFile();
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(0);

        var amounts = rowGroup.ReadColumn<SqlDecimal?>(2);
        var created = rowGroup.ReadColumn<DateTime?>("created");

        Assert.Equal(new SqlDecimal?[] { new(10.5m), null, new(30.25m) }, amounts);
        Assert.Equal(new DateTime(2024, 6, 1, 1, 2, 3, DateTimeKind.Utc), created[0]);
        Assert.Null(created[1]);
        Assert.Equal(new DateTime(2024, 6, 1, 4, 5, 6, DateTimeKind.Utc), created[2]);
    }

    [Fact]
    public void RowGroupReader_ReadsClrGuidBinaryAndDateColumns()
    {
        using var source = CreateEdgeTypeFile();
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(0);

        var payload = rowGroup.ReadColumn<byte[]>(0);
        var traceIds = rowGroup.ReadColumn<Guid>(1);
#if NET8_0_OR_GREATER
        var businessDates = rowGroup.ReadColumn<DateOnly?>(2);
        var snapshotDates = rowGroup.ReadColumn<DateOnly>(3);
#else
        var businessDates = rowGroup.ReadColumn<DateTime?>(2);
        var snapshotDates = rowGroup.ReadColumn<DateTime>(3);
#endif

        Assert.Equal(new byte[] { 1, 2, 3 }, payload[0]);
        Assert.Equal(new byte[] { 4, 5 }, payload[1]);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), traceIds[0]);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), traceIds[1]);
#if NET8_0_OR_GREATER
        Assert.Equal(new DateOnly(2024, 8, 1), businessDates[0]);
        Assert.Null(businessDates[1]);
        Assert.Equal(new DateOnly(2024, 8, 3), businessDates[2]);
        Assert.Equal(new DateOnly(2024, 9, 1), snapshotDates[0]);
        Assert.Equal(new DateOnly(2024, 9, 2), snapshotDates[1]);
        Assert.Equal(new DateOnly(2024, 9, 3), snapshotDates[2]);
#else
        Assert.Equal(new DateTime(2024, 8, 1), businessDates[0]);
        Assert.Null(businessDates[1]);
        Assert.Equal(new DateTime(2024, 8, 3), businessDates[2]);
        Assert.Equal(new DateTime(2024, 9, 1), snapshotDates[0]);
        Assert.Equal(new DateTime(2024, 9, 2), snapshotDates[1]);
        Assert.Equal(new DateTime(2024, 9, 3), snapshotDates[2]);
#endif
    }

    [Fact]
    public void RowGroupReader_RejectsMismatchedClrType()
    {
        using var source = CreateTwoRowGroupNoDecimalFile();
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(0);

        var exception = Assert.Throws<InvalidOperationException>(() => rowGroup.ReadColumn<long?>(0));
        Assert.Contains("expects CLR type", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FileReader_RejectsNonSeekableStreams()
    {
        using var source = new NonSeekableReadStream(CreateTwoRowGroupNoDecimalFile().ToArray());
        var exception = Assert.Throws<ArgumentException>(() => new ParquetFileReader(source));
        Assert.Contains("seek", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Profiling-only test; run manually when collecting read-path CLR materialization profiles.")]
    public void RowGroupReader_ReadsLargeClrColumns_ForProfiling()
    {
        const int rowCount = 1_000_000;

        using var source = CreateLargeMixedFile(rowCount);
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(0);

        var ids = rowGroup.ReadColumn<int?>(0);
        var names = rowGroup.ReadColumn<string>(1);
        var amounts = rowGroup.ReadColumn<SqlDecimal?>(2);
        var created = rowGroup.ReadColumn<DateTime?>(3);

        Assert.Equal(rowCount, ids.Length);
        Assert.Equal(rowCount, names.Length);
        Assert.Equal(rowCount, amounts.Length);
        Assert.Equal(rowCount, created.Length);
        Assert.Equal(1, ids[1]);
        Assert.Equal("row-1", names[1]);
        Assert.Equal(new SqlDecimal(1.25m), amounts[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 1, 0, DateTimeKind.Utc), created[1]);
    }

    [Fact(Skip = "Profiling-only test; run manually when isolating string CLR materialization hot paths.")]
    public void RowGroupReader_ReadsLargeStringColumn_ForProfiling()
    {
        const int rowCount = 1_000_000;

        using var source = CreateLargeMixedFile(rowCount);
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(0);

        var names = rowGroup.ReadColumn<string>(1);

        Assert.Equal(rowCount, names.Length);
        Assert.Null(names[0]);
        Assert.Equal("row-1", names[1]);
        Assert.Equal("row-999999", names[names.Length - 1]);
    }

    [Fact(Skip = "Profiling-only test; run manually when isolating temporal and decimal CLR materialization hot paths.")]
    public void RowGroupReader_ReadsLargeTemporalAndDecimalColumns_ForProfiling()
    {
        const int rowCount = 1_000_000;

        using var source = CreateLargeMixedFile(rowCount);
        using var reader = new ParquetFileReader(source);
        using var rowGroup = reader.OpenRowGroupReader(0);

        var amounts = rowGroup.ReadColumn<SqlDecimal?>(2);
        var created = rowGroup.ReadColumn<DateTime?>(3);

        Assert.Equal(rowCount, amounts.Length);
        Assert.Equal(rowCount, created.Length);
        Assert.Equal(new SqlDecimal(1.25m), amounts[1]);
        Assert.Null(amounts[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 1, 0, DateTimeKind.Utc), created[1]);
        Assert.Null(created[0]);
    }

    private static MemoryStream CreateTwoRowGroupMixedFile()
    {
        var destination = new MemoryStream();
        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int?>("id"),
            new ParquetSharp.Column<string>("name"),
            new(typeof(decimal?), "amount", LogicalType.Decimal(29, 4), 16),
            new ParquetSharp.Column<DateTime?>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
        };

        using (var writer = new ParquetSharpFileWriter(destination, columns, leaveOpen: true))
        {
            using (var rowGroup = writer.AppendRowGroup())
            {
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(new int?[] { 1, null, 3 });
                rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(new string?[] { "alpha", "beta", "gamma" });
                rowGroup.NextColumn().LogicalWriter<decimal?>().WriteBatch(new decimal?[] { 10.5m, null, 30.25m });
                rowGroup.NextColumn().LogicalWriter<DateTime?>().WriteBatch(new DateTime?[]
                {
                    new DateTime(2024, 6, 1, 1, 2, 3, DateTimeKind.Utc),
                    null,
                    new DateTime(2024, 6, 1, 4, 5, 6, DateTimeKind.Utc),
                });
            }

            using (var rowGroup = writer.AppendRowGroup())
            {
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(new int?[] { 4, 5, null });
                rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(new string?[] { "delta", "epsilon", "zeta" });
                rowGroup.NextColumn().LogicalWriter<decimal?>().WriteBatch(new decimal?[] { 40.75m, 50.5m, null });
                rowGroup.NextColumn().LogicalWriter<DateTime?>().WriteBatch(new DateTime?[]
                {
                    new DateTime(2024, 6, 2, 1, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 6, 2, 2, 0, 0, DateTimeKind.Utc),
                    null,
                });
            }

            writer.Close();
        }

        destination.Position = 0;
        return destination;
    }

    private static MemoryStream CreateNullableStringBatchFile()
    {
        var destination = new MemoryStream();
        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<string>("name"),
        };

        using (var writer = new ParquetSharpFileWriter(destination, columns, leaveOpen: true))
        {
            using var rowGroup = writer.AppendRowGroup();
            rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(new string?[] { "alpha", null, "gamma", null, "epsilon" });
            writer.Close();
        }

        destination.Position = 0;
        return destination;
    }

    private static MemoryStream CreateLargeMixedFile(int rowCount)
    {
        var ids = new int?[rowCount];
        var names = new string?[rowCount];
        var amounts = new decimal?[rowCount];
        var created = new DateTime?[rowCount];

        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < rowCount; i++)
        {
            ids[i] = i % 11 == 0 ? null : i;
            names[i] = i % 5 == 0 ? null : $"row-{i}";
            amounts[i] = i % 7 == 0 ? null : i * 1.25m;
            created[i] = i % 13 == 0 ? null : start.AddMinutes(i);
        }

        var destination = new MemoryStream();
        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int?>("id"),
            new ParquetSharp.Column<string>("name"),
            new(typeof(decimal?), "amount", LogicalType.Decimal(29, 4), 16),
            new ParquetSharp.Column<DateTime?>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
        };

        using (var writer = new ParquetSharpFileWriter(destination, columns, leaveOpen: true))
        {
            using var rowGroup = writer.AppendRowGroup();
            rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(ids);
            rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(names);
            rowGroup.NextColumn().LogicalWriter<decimal?>().WriteBatch(amounts);
            rowGroup.NextColumn().LogicalWriter<DateTime?>().WriteBatch(created);
            writer.Close();
        }

        destination.Position = 0;
        return destination;
    }

    private static MemoryStream CreateTwoRowGroupNoDecimalFile()
    {
        var destination = new MemoryStream();
        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int?>("id"),
            new ParquetSharp.Column<string>("name"),
            new ParquetSharp.Column<DateTime?>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
            new ParquetSharp.Column<int?>("flag"),
        };

        using (var writer = new ParquetSharpFileWriter(destination, columns, leaveOpen: true))
        {
            using (var rowGroup = writer.AppendRowGroup())
            {
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(new int?[] { 1, 2, 3 });
                rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(new string?[] { "alpha", "beta", "gamma" });
                rowGroup.NextColumn().LogicalWriter<DateTime?>().WriteBatch(new DateTime?[]
                {
                    new DateTime(2024, 7, 1, 1, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 7, 1, 2, 0, 0, DateTimeKind.Utc),
                    null,
                });
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(new int?[] { 1, 0, null });
            }

            using (var rowGroup = writer.AppendRowGroup())
            {
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(new int?[] { null, 5, 6 });
                rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(new string?[] { "delta", "epsilon", "zeta" });
                rowGroup.NextColumn().LogicalWriter<DateTime?>().WriteBatch(new DateTime?[]
                {
                    null,
                    new DateTime(2024, 7, 2, 2, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 7, 2, 3, 0, 0, DateTimeKind.Utc),
                });
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(new int?[] { null, 1, 0 });
            }

            writer.Close();
        }

        destination.Position = 0;
        return destination;
    }

    private static MemoryStream CreateEdgeTypeFile()
    {
        var destination = new MemoryStream();
        var columns = new ParquetSharp.Column[]
        {
            new(typeof(byte[]), "payload", LogicalType.None()),
            new(typeof(Guid), "traceId", LogicalType.Uuid(), 16),
            new ParquetSharp.Column<int?>("businessDate", LogicalType.Date()),
            new ParquetSharp.Column<int>("snapshotDate", LogicalType.Date()),
        };

        using (var writer = new ParquetSharpFileWriter(destination, columns, leaveOpen: true))
        {
            using (var rowGroup = writer.AppendRowGroup())
            {
                rowGroup.NextColumn().LogicalWriter<byte[]>().WriteBatch(
                    new byte[][]
                    {
                        new byte[] { 1, 2, 3 },
                        new byte[] { 4, 5 },
                        new byte[] { 6 },
                    });

                rowGroup.NextColumn().LogicalWriter<Guid>().WriteBatch(
                    new Guid[]
                    {
                        Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    });

                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(
                    new int?[]
                    {
                        DaysSinceUnixEpoch(new DateTime(2024, 8, 1)),
                        null,
                        DaysSinceUnixEpoch(new DateTime(2024, 8, 3)),
                    });

                rowGroup.NextColumn().LogicalWriter<int>().WriteBatch(
                    new int[]
                    {
                        DaysSinceUnixEpoch(new DateTime(2024, 9, 1)),
                        DaysSinceUnixEpoch(new DateTime(2024, 9, 2)),
                        DaysSinceUnixEpoch(new DateTime(2024, 9, 3)),
                    });
            }

            writer.Close();
        }

        destination.Position = 0;
        return destination;
    }

    private static int DaysSinceUnixEpoch(DateTime value)
    {
        return (value.Date - new DateTime(1970, 1, 1)).Days;
    }

    private sealed class NonSeekableReadStream : MemoryStream
    {
        public NonSeekableReadStream(byte[] buffer)
            : base(buffer, writable: false)
        {
        }

        public override bool CanSeek => false;
    }
}
