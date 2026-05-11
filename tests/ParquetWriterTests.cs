using Apache.Arrow;
using Apache.Arrow.Memory;
using Parquet;
using Parquet.Data;
#if NET8_0_OR_GREATER
using ParquetRsForDotnet.Internal;
#endif

namespace ParquetRsForDotnet.Tests;

/// <summary>
/// Verifies end-to-end parquet writes and sink behavior for the batch writer path.
/// </summary>
public sealed class ParquetWriterTests
{
    [Fact]
    public async Task FileWriter_WritesMultipleManagedBatchesInSchemaOrder()
    {
        var schema = new ParquetSchema(
        [
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
        ]);

        using var destination = new MemoryStream();
        using (var writer = new ParquetFileWriter(destination, schema))
        {
            writer.WriteBatch(new int?[] { 1, 2 }, new string?[] { "first", "second" });
            writer.WriteBatch(new int?[] { 3, null }, new string?[] { "third", null });
            writer.Finish();
        }

        destination.Position = 0;
        using var parquetReader = await ParquetReader.CreateAsync(destination, leaveStreamOpen: true);
        using var rowGroup = parquetReader.OpenRowGroupReader(0);
        var idColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[0]);
        var nameColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[1]);

        Assert.Equal(new[] { 1, 2, 3 }, (int[])idColumn.DefinedData);
        Assert.Equal(new[] { 1, 1, 1, 0 }, idColumn.DefinitionLevels);
        Assert.Equal(new string?[] { "first", "second", "third", null }, (string?[])nameColumn.Data);
    }

    [Fact]
    public async Task FileWriter_WritesArrowBatches()
    {
        var allocator = MemoryAllocator.Default.Value;
        var schema = new ParquetSchema(
        [
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
        ]);

        using var destination = new MemoryStream();
        using (var writer = new ParquetFileWriter(destination, schema))
        {
            using var id = new Int32Array.Builder().Append(10).AppendNull().Append(30).Build(allocator);
            using var name = new StringArray.Builder().Append("delta").AppendNull().Append("omega").Build(allocator);
            writer.WriteBatch(id, name);
            writer.Finish();
        }

        destination.Position = 0;
        using var parquetReader = await ParquetReader.CreateAsync(destination, leaveStreamOpen: true);
        using var rowGroup = parquetReader.OpenRowGroupReader(0);
        var idColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[0]);
        var nameColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[1]);

        Assert.Equal(new[] { 10, 30 }, (int[])idColumn.DefinedData);
        Assert.Equal(new[] { 1, 0, 1 }, idColumn.DefinitionLevels);
        Assert.Equal(new string?[] { "delta", null, "omega" }, (string?[])nameColumn.Data);
    }

    [Fact]
    public void FileWriter_RejectsInvalidMemoryTuningOptions()
    {
        var schema = new ParquetSchema([new ParquetColumn("id", ParquetColumnType.Int32)]);

        AssertInvalidOption(schema, new ParquetWriteOptions { DataPageRowCountLimit = 0 }, "DataPageRowCountLimit");
        AssertInvalidOption(schema, new ParquetWriteOptions { DataPageSizeLimitBytes = 0 }, "DataPageSizeLimitBytes");
        AssertInvalidOption(schema, new ParquetWriteOptions { DictionaryPageSizeLimitBytes = 0 }, "DictionaryPageSizeLimitBytes");
        AssertInvalidOption(schema, new ParquetWriteOptions { MaxNativeWriterMemoryBytes = 0 }, "MaxNativeWriterMemoryBytes");
    }

    [Fact]
    public void FileWriter_FlushesRowGroups_WhenNativeWriterMemoryThresholdIsExceeded()
    {
        var schema = new ParquetSchema([new ParquetColumn("name", ParquetColumnType.String)]);

        using var destination = new MemoryStream();
        using (var writer = new ParquetFileWriter(destination, schema, new ParquetWriteOptions
        {
            MaxRowGroupRows = 1_000_000,
            MaxNativeWriterMemoryBytes = 1,
        }))
        {
            writer.WriteBatch(new[] { new string('a', 4096) });
            writer.WriteBatch(new[] { new string('b', 4096) });
            writer.Finish();
        }

        destination.Position = 0;
        using var reader = new ParquetFileReader(destination);
        Assert.Equal(2, reader.RowGroupCount);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void NativeOptionScope_MarshalsMemoryTuningOptions()
    {
        using var nativeOptions = NativeParquetBridge.CreateNativeOptionScope(new ParquetWriteOptions
        {
            DataPageRowCountLimit = 123,
            DataPageSizeLimitBytes = 456,
            DictionaryPageSizeLimitBytes = 789,
            MaxNativeWriterMemoryBytes = 1024,
        });

        Assert.Equal(123, nativeOptions.Options.DataPageRowCountLimit);
        Assert.Equal(456, nativeOptions.Options.DataPageSizeLimitBytes);
        Assert.Equal(789, nativeOptions.Options.DictionaryPageSizeLimitBytes);
        Assert.Equal(1024, nativeOptions.Options.MaxNativeWriterMemoryBytes);
    }
#endif

    [Fact(Skip = "Profiling-only test; run manually when collecting write-path profiles.")]
    public void FileWriter_WritesLargePrimitiveClrBatches_ForProfiling()
    {
        const int rowCount = 1_000_000;
        const int batchCount = 10;

        var int32Values = new int[rowCount];
        var int64Values = new long[rowCount];
        var int16Values = new short[rowCount];
        var uint16Values = new ushort[rowCount];
        var uint8Values = new byte[rowCount];
        var boolValues = new bool[rowCount];
        var float32Values = new float[rowCount];
        var float64Values = new double[rowCount];
        var uint32Values = new uint[rowCount];
        var uint64Values = new ulong[rowCount];

        for (var i = 0; i < rowCount; i++)
        {
            int32Values[i] = i;
            int64Values[i] = i * 10L;
            int16Values[i] = (short)(i % short.MaxValue);
            uint16Values[i] = (ushort)(i % ushort.MaxValue);
            uint8Values[i] = (byte)(i % byte.MaxValue);
            boolValues[i] = (i & 1) == 0;
            float32Values[i] = i * 0.5f;
            float64Values[i] = i * 0.25d;
            uint32Values[i] = (uint)i;
            uint64Values[i] = (ulong)i * 100UL;
        }

        var schema = new ParquetSchema(
        [
            new ParquetColumn("i32", ParquetColumnType.Int32),
            new ParquetColumn("i64", ParquetColumnType.Int64),
            new ParquetColumn("i16", ParquetColumnType.Int16),
            new ParquetColumn("u16", ParquetColumnType.UInt16),
            new ParquetColumn("u8", ParquetColumnType.UInt8),
            new ParquetColumn("flag", ParquetColumnType.Boolean),
            new ParquetColumn("f32", ParquetColumnType.Float32),
            new ParquetColumn("f64", ParquetColumnType.Float64),
            new ParquetColumn("u32", ParquetColumnType.UInt32),
            new ParquetColumn("u64", ParquetColumnType.UInt64),
        ]);

        using var destination = new MemoryStream();
        using var writer = new ParquetFileWriter(destination, schema, new ParquetWriteOptions
        {
            Compression = ParquetCompression.Snappy,
            MaxRowGroupRows = rowCount * batchCount,
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth
        });

        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            writer.WriteBatch(
                int32Values,
                int64Values,
                int16Values,
                uint16Values,
                uint8Values,
                boolValues,
                float32Values,
                float64Values,
                uint32Values,
                uint64Values);
        }

        writer.Finish();

        Assert.True(destination.Length > 0);
    }

    [Fact]
    public void FileWriter_RejectsSchemaOrderViolations()
    {
        var schema = new ParquetSchema(
        [
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
        ]);

        using var destination = new MemoryStream();
        using var writer = new ParquetFileWriter(destination, schema);

        var exception = Assert.Throws<NativeParquetException>(() => writer.WriteBatch(new string?[] { "wrong" }, new int?[] { 1 }));
        Assert.Equal(NativeErrorCode.SchemaMismatch, exception.ErrorCode);
        Assert.Contains("CLR element type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileWriter_RejectsMismatchedArrowTypes()
    {
        var allocator = MemoryAllocator.Default.Value;
        var schema = new ParquetSchema([new ParquetColumn("id", ParquetColumnType.Int32)]);

        using var destination = new MemoryStream();
        using var writer = new ParquetFileWriter(destination, schema);
        using var values = new Int64Array.Builder().Append(1).Build(allocator);

        var exception = Assert.Throws<NativeParquetException>(() => writer.WriteBatch(values));
        Assert.Equal(NativeErrorCode.SchemaMismatch, exception.ErrorCode);
        Assert.Contains("Arrow type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FileWriter_WritesTemporalAndDecimalManagedBatches()
    {
        var schema = new ParquetSchema(
        [
            new ParquetColumn("eventDate", ParquetColumnType.Date32, isNullable: true),
            new ParquetColumn("eventTime", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
            new ParquetColumn("amount", new ParquetDecimalSettings(18, 2), isNullable: true),
        ]);

        using var destination = new MemoryStream();
        using (var writer = new ParquetFileWriter(destination, schema))
        {
            writer.WriteBatch(
                CreateNullableDateArray(new DateTime(2024, 5, 1), null, new DateTime(2024, 5, 3)),
                new DateTime?[]
                {
                    new(2024, 6, 1, 1, 2, 3, DateTimeKind.Utc),
                    null,
                    new(2024, 6, 1, 4, 5, 6, DateTimeKind.Utc),
                },
                new decimal?[] { 10.5m, null, 30.25m });
        }

        destination.Position = 0;
        using var parquetReader = await ParquetReader.CreateAsync(destination, leaveStreamOpen: true);
        using var rowGroup = parquetReader.OpenRowGroupReader(0);
        var dateColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[0]);
        var timestampColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[1]);
        var amountColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[2]);

        Assert.Equal(
            new DateTime?[] { new(2024, 5, 1), null, new(2024, 5, 3) },
            ((DateTime?[])dateColumn.Data).Select(static value => value?.Date).ToArray());

        var timestamps = (DateTime?[])timestampColumn.Data;
        Assert.Equal(3, timestamps.Length);
        Assert.NotNull(timestamps[0]);
        Assert.Null(timestamps[1]);
        Assert.NotNull(timestamps[2]);
        Assert.True(timestamps[0] < timestamps[2]);
        Assert.Equal(new decimal?[] { 10.5m, null, 30.25m }, (decimal?[])amountColumn.Data);
    }

    [Fact]
    public async Task FileWriter_WritesNullableFixedWidthManagedBatches_WhenLowLevelFixedWidthIsRequested()
    {
        var schema = new ParquetSchema(
        [
            new ParquetColumn("i32", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("f64", ParquetColumnType.Float64, isNullable: true),
            new ParquetColumn("eventDate", ParquetColumnType.Date32, isNullable: true),
            new ParquetColumn("eventTime", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
        ]);

        using var destination = new MemoryStream();
        using (var writer = new ParquetFileWriter(destination, schema, new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth
        }))
        {
            writer.WriteBatch(
                new int?[] { 10, null, 30, null },
                new double?[] { 1.25d, null, 3.5d, 4.75d },
                CreateNullableDateArray(new DateTime(2024, 5, 1), null, new DateTime(2024, 5, 3), new DateTime(2024, 5, 4)),
                new DateTime?[]
                {
                    new(2024, 6, 1, 1, 2, 3, DateTimeKind.Utc),
                    null,
                    new(2024, 6, 1, 4, 5, 6, DateTimeKind.Utc),
                    new(2024, 6, 1, 7, 8, 9, DateTimeKind.Utc),
                });

            writer.Finish();
        }

        destination.Position = 0;
        using var parquetReader = await ParquetReader.CreateAsync(destination, leaveStreamOpen: true);
        using var rowGroup = parquetReader.OpenRowGroupReader(0);
        var intColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[0]);
        var doubleColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[1]);
        var dateColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[2]);
        var timestampColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[3]);

        Assert.Equal(new int?[] { 10, null, 30, null }, (int?[])intColumn.Data);
        Assert.Equal(new double?[] { 1.25d, null, 3.5d, 4.75d }, (double?[])doubleColumn.Data);
        Assert.Equal(
            new DateTime?[] { new(2024, 5, 1), null, new(2024, 5, 3), new(2024, 5, 4) },
            ((DateTime?[])dateColumn.Data).Select(static value => value?.Date).ToArray());
        Assert.Equal(
            new DateTime?[]
            {
                new(2024, 6, 1, 1, 2, 3),
                null,
                new(2024, 6, 1, 4, 5, 6),
                new(2024, 6, 1, 7, 8, 9),
            },
            ((DateTime?[])timestampColumn.Data).Select(static value => value?.ToUniversalTime()).ToArray());
    }

    [Fact]
    public async Task FileWriter_WritesNonNullableTemporalManagedBatches_WithLowLevelMaterialization()
    {
        var schema = new ParquetSchema(
        [
            new ParquetColumn("eventDate32", ParquetColumnType.Date32),
            new ParquetColumn("eventDate64", ParquetColumnType.Date64),
            new ParquetColumn("eventTime", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC")),
        ]);

        using var destination = new MemoryStream();
        using (var writer = new ParquetFileWriter(destination, schema, new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth
        }))
        {
            writer.WriteBatch(
                CreateDateArray(new DateTime(2024, 5, 1), new DateTime(2024, 5, 2), new DateTime(2024, 5, 3)),
                CreateDateArray(new DateTime(2024, 6, 1), new DateTime(2024, 6, 2), new DateTime(2024, 6, 3)),
                new DateTime[]
                {
                    new(2024, 7, 1, 1, 2, 3, DateTimeKind.Utc),
                    new(2024, 7, 1, 4, 5, 6, DateTimeKind.Utc),
                    new(2024, 7, 1, 7, 8, 9, DateTimeKind.Utc),
                });

            writer.Finish();
        }

        destination.Position = 0;
        using var parquetReader = await ParquetReader.CreateAsync(destination, leaveStreamOpen: true);
        using var rowGroup = parquetReader.OpenRowGroupReader(0);
        var date32Column = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[0]);
        var date64Column = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[1]);
        var timestampColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[2]);

        Assert.Equal(
            new DateTime[] { new(2024, 5, 1), new(2024, 5, 2), new(2024, 5, 3) },
            ((DateTime[])date32Column.Data).Select(static value => value.Date).ToArray());
        Assert.Equal(
            new[]
            {
                MillisecondsSinceUnixEpoch(new DateTime(2024, 6, 1)),
                MillisecondsSinceUnixEpoch(new DateTime(2024, 6, 2)),
                MillisecondsSinceUnixEpoch(new DateTime(2024, 6, 3)),
            },
            (long[])date64Column.Data);
        Assert.Equal(
            new DateTime[]
            {
                new(2024, 7, 1, 1, 2, 3),
                new(2024, 7, 1, 4, 5, 6),
                new(2024, 7, 1, 7, 8, 9),
            },
            ((DateTime[])timestampColumn.Data).Select(static value => value.ToUniversalTime()).ToArray());
    }

    [Fact]
    public async Task FileWriter_WritesNonNullableStringManagedBatches_WithLowLevelMaterialization()
    {
        var schema = new ParquetSchema(
        [
            new ParquetColumn("id", ParquetColumnType.Int32),
            new ParquetColumn("name", ParquetColumnType.String),
        ]);

        using var destination = new MemoryStream();
        using (var writer = new ParquetFileWriter(destination, schema, new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth
        }))
        {
            writer.WriteBatch(
                new[] { 1, 2, 3 },
                new[] { "alpha", "beta", "gamma" });

            writer.Finish();
        }

        destination.Position = 0;
        using var parquetReader = await ParquetReader.CreateAsync(destination, leaveStreamOpen: true);
        using var rowGroup = parquetReader.OpenRowGroupReader(0);
        var idColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[0]);
        var nameColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[1]);

        Assert.Equal(new[] { 1, 2, 3 }, (int[])idColumn.Data);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, (string[])nameColumn.Data);
    }

    [Fact]
    public async Task FileWriter_WritesTemporalArrowBatches()
    {
        var allocator = MemoryAllocator.Default.Value;
        var schema = new ParquetSchema(
        [
            new ParquetColumn("eventDate", ParquetColumnType.Date32, isNullable: true),
            new ParquetColumn("eventTime", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
        ]);

        using var destination = new MemoryStream();
        using (var writer = new ParquetFileWriter(destination, schema))
        {
            using var dates = new Date32Array.Builder()
                .Append(new DateTime(2024, 7, 1))
                .AppendNull()
                .Append(new DateTime(2024, 7, 3))
                .Build(allocator);
            using var timestamps = new TimestampArray.Builder(new Apache.Arrow.Types.TimestampType(Apache.Arrow.Types.TimeUnit.Millisecond, "UTC"))
                .Append(new DateTimeOffset(new DateTime(2024, 7, 1, 10, 0, 0, DateTimeKind.Utc)))
                .AppendNull()
                .Append(new DateTimeOffset(new DateTime(2024, 7, 1, 12, 0, 0, DateTimeKind.Utc)))
                .Build(allocator);

            writer.WriteBatch(dates, timestamps);
        }

        destination.Position = 0;
        using var parquetReader = await ParquetReader.CreateAsync(destination, leaveStreamOpen: true);
        using var rowGroup = parquetReader.OpenRowGroupReader(0);
        var dateColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[0]);
        var timestampColumn = await rowGroup.ReadColumnAsync((Parquet.Schema.DataField)parquetReader.Schema[1]);

        Assert.Equal(
            new DateTime?[] { new(2024, 7, 1), null, new(2024, 7, 3) },
            ((DateTime?[])dateColumn.Data).Select(static value => value?.Date).ToArray());

        var values = (DateTime?[])timestampColumn.Data;
        Assert.Equal(3, values.Length);
        Assert.NotNull(values[0]);
        Assert.Null(values[1]);
        Assert.NotNull(values[2]);
        Assert.True(values[0] < values[2]);
    }

    [Fact]
    public void ManagedSink_ReportsWriteFailures()
    {
#if NET8_0_OR_GREATER
        using var stream = new ThrowingWriteStream();
        using var sink = new ManagedParquetSink(stream);

        unsafe
        {
            nuint written = 0;
            fixed (byte* data = "abc"u8)
            {
                var result = sink.NativeSink.Write(sink.NativeSink.Context, data, 3, &written);
                Assert.Equal((int)NativeErrorCode.SinkWriteFailed, result);
                var errorPtr = sink.NativeSink.GetLastError(sink.NativeSink.Context);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)errorPtr);
            }
        }
#else
        var schema = new ParquetSchema(new[] { new ParquetColumn("id", ParquetColumnType.Int32) });
        using var stream = new ThrowingWriteStream();
        var writer = new ParquetFileWriter(stream, schema);

        try
        {
            writer.WriteBatch(new[] { 1, 2, 3 });
            var exception = Assert.Throws<NativeParquetException>(() => writer.Finish());
            Assert.True(exception.ErrorCode is NativeErrorCode.SinkWriteFailed or NativeErrorCode.ParquetEncodeFailed);
        }
        finally
        {
            try
            {
                writer.Dispose();
            }
            catch (NativeParquetException)
            {
            }
        }
#endif
    }

    /// <summary>
    /// Simulates a managed destination stream that fails on write so sink error propagation can be tested.
    /// </summary>
    private sealed class ThrowingWriteStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("write failed");
        }

#if NET8_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new InvalidOperationException("write failed");
        }
#endif
    }

    private static void AssertInvalidOption(ParquetSchema schema, ParquetWriteOptions options, string paramName)
    {
        using var destination = new MemoryStream();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ParquetFileWriter(destination, schema, options));
        Assert.Equal(paramName, exception.ParamName);
    }

    private static System.Array CreateNullableDateArray(params DateTime?[] values)
    {
#if NET8_0_OR_GREATER
        var dates = new DateOnly?[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            dates[i] = value.HasValue ? DateOnly.FromDateTime(value.GetValueOrDefault()) : null;
        }

        return dates;
#else
        return values;
#endif
    }

    private static System.Array CreateDateArray(params DateTime[] values)
    {
#if NET8_0_OR_GREATER
        var dates = new DateOnly[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            dates[i] = DateOnly.FromDateTime(values[i]);
        }

        return dates;
#else
        return values;
#endif
    }

    private static long MillisecondsSinceUnixEpoch(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value.Date, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }
}
