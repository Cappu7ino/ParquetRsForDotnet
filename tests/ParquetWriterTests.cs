using Apache.Arrow;
using Apache.Arrow.Memory;
using Parquet;
using Parquet.Data;
using ParquetRsForDotnet.Internal;

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
                new DateOnly?[] { new(2024, 5, 1), null, new(2024, 5, 3) },
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
                .Append(new DateOnly(2024, 7, 1))
                .AppendNull()
                .Append(new DateOnly(2024, 7, 3))
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

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new InvalidOperationException("write failed");
        }
    }
}
