using System.Data.SqlTypes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using ParquetSharp;
using ParquetSharp.IO;
using ManagedParquetFileReader = ParquetRsForDotnet.ParquetFileReader;
using ParquetSharpTimeUnit = ParquetSharp.TimeUnit;

namespace ParquetRsForDotnet.Benchmarks;

[MemoryDiagnoser]
[NativeMemoryProfiler]
[Config(typeof(ProductionBenchmarkConfig))]
[BenchmarkCategory("Read", "NonNullable")]
public class NonNullableReadCategoryBenchmarks
{
    private const int MultiBatchCount = 10;

    private byte[] fileBytes = default!;

    [Params(10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        fileBytes = CreateNonNullableFileBytes(RowCount);
    }

    [Benchmark]
    public int ReadArrowNativeWithParquetRsForDotnet()
    {
        using var source = new MemoryStream(fileBytes, writable: false);
        using var reader = new ManagedParquetFileReader(source);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
        {
            using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);

            using var flagColumn = rowGroup.ReadColumn(0);
            using var idColumn = rowGroup.ReadColumn(1);
            using var countColumn = rowGroup.ReadColumn(2);
            using var ratio32Column = rowGroup.ReadColumn(3);
            using var ratio64Column = rowGroup.ReadColumn(4);
            using var amountColumn = rowGroup.ReadColumn(5);
            using var createdColumn = rowGroup.ReadColumn(6);
            using var nameColumn = rowGroup.ReadColumn(7);

            score += flagColumn.Length;
            score += idColumn.Length;
            score += countColumn.Length;
            score += ratio32Column.Length;
            score += ratio64Column.Length;
            score += amountColumn.Length;
            score += createdColumn.Length;
            score += nameColumn.Length;
        }

        return score;
    }

    [Benchmark]
    public int ReadWithParquetRsForDotnet()
    {
        using var source = new MemoryStream(fileBytes, writable: false);
        using var reader = new ManagedParquetFileReader(source);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
        {
            using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);

            score += rowGroup.ReadColumn<bool>(0).Length;
            score += rowGroup.ReadColumn<int>(1).Length;
            score += rowGroup.ReadColumn<long>(2).Length;
            score += rowGroup.ReadColumn<float>(3).Length;
            score += rowGroup.ReadColumn<double>(4).Length;
            score += rowGroup.ReadColumn<SqlDecimal>(5).Length;
            score += rowGroup.ReadColumn<DateTime>(6).Length;
            score += rowGroup.ReadColumn<string>(7).Length;
        }

        return score;
    }

    [Benchmark(Baseline = true)]
    public int ReadWithParquetSharp()
    {
        using var source = new MemoryStream(fileBytes, writable: false);
        using var input = new ManagedRandomAccessFile(source);
        using var reader = new ParquetSharp.ParquetFileReader(input);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.FileMetaData.NumRowGroups; rowGroupIndex++)
        {
            using var rowGroup = reader.RowGroup(rowGroupIndex);

            using (var flagReader = rowGroup.Column(0).LogicalReader<bool>())
            {
                score += flagReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var idReader = rowGroup.Column(1).LogicalReader<int>())
            {
                score += idReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var countReader = rowGroup.Column(2).LogicalReader<long>())
            {
                score += countReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var ratio32Reader = rowGroup.Column(3).LogicalReader<float>())
            {
                score += ratio32Reader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var ratio64Reader = rowGroup.Column(4).LogicalReader<double>())
            {
                score += ratio64Reader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var amountReader = rowGroup.Column(5).LogicalReader<decimal>())
            {
                score += amountReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var createdReader = rowGroup.Column(6).LogicalReader<DateTime>())
            {
                score += createdReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var nameReader = rowGroup.Column(7).LogicalReader<string>())
            {
                score += nameReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }
        }

        return score;
    }

    private static byte[] CreateNonNullableFileBytes(int rowCount)
    {
        var data = CreateNonNullableData(rowCount);
        using var destination = new MemoryStream();

        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<bool>("flag"),
            new ParquetSharp.Column<int>("id"),
            new ParquetSharp.Column<long>("count"),
            new ParquetSharp.Column<float>("ratio32"),
            new ParquetSharp.Column<double>("ratio64"),
            new ParquetSharp.Column<decimal>("amount", LogicalType.Decimal(29, 4)),
            new ParquetSharp.Column<DateTime>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
            new ParquetSharp.Column<string>("name"),
        };

        using (var writer = new ParquetSharp.ParquetFileWriter(destination, columns, leaveOpen: true))
        {
            for (var batchIndex = 0; batchIndex < MultiBatchCount; batchIndex++)
            {
                var range = GetBatchRange(data.Ids.Length, MultiBatchCount, batchIndex);
                using var rowGroup = writer.AppendRowGroup();
                rowGroup.NextColumn().LogicalWriter<bool>().WriteBatch(data.Flags[range]);
                rowGroup.NextColumn().LogicalWriter<int>().WriteBatch(data.Ids[range]);
                rowGroup.NextColumn().LogicalWriter<long>().WriteBatch(data.Counts[range]);
                rowGroup.NextColumn().LogicalWriter<float>().WriteBatch(data.Ratio32[range]);
                rowGroup.NextColumn().LogicalWriter<double>().WriteBatch(data.Ratio64[range]);
                rowGroup.NextColumn().LogicalWriter<decimal>().WriteBatch(data.Amounts[range]);
                rowGroup.NextColumn().LogicalWriter<DateTime>().WriteBatch(data.Created[range]);
                rowGroup.NextColumn().LogicalWriter<string>().WriteBatch(data.Names[range]);
            }

            writer.Close();
        }

        return destination.ToArray();
    }

    private static NonNullableReadData CreateNonNullableData(int rowCount)
    {
        var flags = new bool[rowCount];
        var ids = new int[rowCount];
        var counts = new long[rowCount];
        var ratio32 = new float[rowCount];
        var ratio64 = new double[rowCount];
        var amounts = new decimal[rowCount];
        var created = new DateTime[rowCount];
        var names = new string[rowCount];

        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < rowCount; i++)
        {
            flags[i] = i % 2 == 0;
            ids[i] = i;
            counts[i] = i * 17L;
            ratio32[i] = i * 0.25f;
            ratio64[i] = i * 0.125d;
            amounts[i] = i * 1.25m;
            created[i] = start.AddSeconds(i);
            names[i] = $"row-{i}";
        }

        return new NonNullableReadData(flags, ids, counts, ratio32, ratio64, amounts, created, names);
    }

    private static Range GetBatchRange(int totalLength, int batchCount, int batchIndex)
    {
        var batchSize = totalLength / batchCount;
        var start = batchIndex * batchSize;
        var end = batchIndex == batchCount - 1 ? totalLength : start + batchSize;
        return start..end;
    }

    private sealed record NonNullableReadData(
        bool[] Flags,
        int[] Ids,
        long[] Counts,
        float[] Ratio32,
        double[] Ratio64,
        decimal[] Amounts,
        DateTime[] Created,
        string[] Names);
}

[MemoryDiagnoser]
[NativeMemoryProfiler]
[Config(typeof(ProductionBenchmarkConfig))]
[BenchmarkCategory("Read", "Nullable")]
public class NullableReadCategoryBenchmarks
{
    private const int MultiBatchCount = 10;

    private byte[] fileBytes = default!;

    [Params(10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        fileBytes = CreateNullableFileBytes(RowCount);
    }

    [Benchmark]
    public int ReadArrowNativeWithParquetRsForDotnet()
    {
        using var source = new MemoryStream(fileBytes, writable: false);
        using var reader = new ManagedParquetFileReader(source);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
        {
            using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);

            using var flagColumn = rowGroup.ReadColumn(0);
            using var idColumn = rowGroup.ReadColumn(1);
            using var countColumn = rowGroup.ReadColumn(2);
            using var ratio32Column = rowGroup.ReadColumn(3);
            using var ratio64Column = rowGroup.ReadColumn(4);
            using var amountColumn = rowGroup.ReadColumn(5);
            using var createdColumn = rowGroup.ReadColumn(6);
            using var nameColumn = rowGroup.ReadColumn(7);

            score += flagColumn.Length;
            score += idColumn.Length;
            score += countColumn.Length;
            score += ratio32Column.Length;
            score += ratio64Column.Length;
            score += amountColumn.Length;
            score += createdColumn.Length;
            score += nameColumn.Length;
        }

        return score;
    }

    [Benchmark]
    public int ReadWithParquetRsForDotnet()
    {
        using var source = new MemoryStream(fileBytes, writable: false);
        using var reader = new ManagedParquetFileReader(source);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
        {
            using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);

            score += rowGroup.ReadColumn<bool?>(0).Length;
            score += rowGroup.ReadColumn<int?>(1).Length;
            score += rowGroup.ReadColumn<long?>(2).Length;
            score += rowGroup.ReadColumn<float?>(3).Length;
            score += rowGroup.ReadColumn<double?>(4).Length;
            score += rowGroup.ReadColumn<SqlDecimal?>(5).Length;
            score += rowGroup.ReadColumn<DateTime?>(6).Length;
            score += rowGroup.ReadColumn<string>(7).Length;
        }

        return score;
    }

    [Benchmark(Baseline = true)]
    public int ReadWithParquetSharp()
    {
        using var source = new MemoryStream(fileBytes, writable: false);
        using var input = new ManagedRandomAccessFile(source);
        using var reader = new ParquetSharp.ParquetFileReader(input);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.FileMetaData.NumRowGroups; rowGroupIndex++)
        {
            using var rowGroup = reader.RowGroup(rowGroupIndex);

            using (var flagReader = rowGroup.Column(0).LogicalReader<bool?>())
            {
                score += flagReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var idReader = rowGroup.Column(1).LogicalReader<int?>())
            {
                score += idReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var countReader = rowGroup.Column(2).LogicalReader<long?>())
            {
                score += countReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var ratio32Reader = rowGroup.Column(3).LogicalReader<float?>())
            {
                score += ratio32Reader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var ratio64Reader = rowGroup.Column(4).LogicalReader<double?>())
            {
                score += ratio64Reader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var amountReader = rowGroup.Column(5).LogicalReader<decimal?>())
            {
                score += amountReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var createdReader = rowGroup.Column(6).LogicalReader<DateTime?>())
            {
                score += createdReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var nameReader = rowGroup.Column(7).LogicalReader<string>())
            {
                score += nameReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }
        }

        return score;
    }

    private static byte[] CreateNullableFileBytes(int rowCount)
    {
        var data = CreateNullableData(rowCount);
        using var destination = new MemoryStream();

        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<bool?>("flag"),
            new ParquetSharp.Column<int?>("id"),
            new ParquetSharp.Column<long?>("count"),
            new ParquetSharp.Column<float?>("ratio32"),
            new ParquetSharp.Column<double?>("ratio64"),
            new ParquetSharp.Column<decimal?>("amount", LogicalType.Decimal(29, 4)),
            new ParquetSharp.Column<DateTime?>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
            new ParquetSharp.Column<string>("name"),
        };

        using (var writer = new ParquetSharp.ParquetFileWriter(destination, columns, leaveOpen: true))
        {
            for (var batchIndex = 0; batchIndex < MultiBatchCount; batchIndex++)
            {
                var range = GetBatchRange(data.Ids.Length, MultiBatchCount, batchIndex);
                using var rowGroup = writer.AppendRowGroup();
                rowGroup.NextColumn().LogicalWriter<bool?>().WriteBatch(data.Flags[range]);
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(data.Ids[range]);
                rowGroup.NextColumn().LogicalWriter<long?>().WriteBatch(data.Counts[range]);
                rowGroup.NextColumn().LogicalWriter<float?>().WriteBatch(data.Ratio32[range]);
                rowGroup.NextColumn().LogicalWriter<double?>().WriteBatch(data.Ratio64[range]);
                rowGroup.NextColumn().LogicalWriter<decimal?>().WriteBatch(data.Amounts[range]);
                rowGroup.NextColumn().LogicalWriter<DateTime?>().WriteBatch(data.Created[range]);
                rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(data.Names[range]);
            }

            writer.Close();
        }

        return destination.ToArray();
    }

    private static NullableReadData CreateNullableData(int rowCount)
    {
        var flags = new bool?[rowCount];
        var ids = new int?[rowCount];
        var counts = new long?[rowCount];
        var ratio32 = new float?[rowCount];
        var ratio64 = new double?[rowCount];
        var amounts = new decimal?[rowCount];
        var created = new DateTime?[rowCount];
        var names = new string?[rowCount];

        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < rowCount; i++)
        {
            flags[i] = i % 3 == 0 ? null : i % 2 == 0;
            ids[i] = i % 5 == 0 ? null : i;
            counts[i] = i % 7 == 0 ? null : i * 17L;
            ratio32[i] = i % 11 == 0 ? null : i * 0.25f;
            ratio64[i] = i % 13 == 0 ? null : i * 0.125d;
            amounts[i] = i % 17 == 0 ? null : i * 1.25m;
            created[i] = i % 19 == 0 ? null : start.AddSeconds(i);
            names[i] = i % 23 == 0 ? null : $"row-{i}";
        }

        return new NullableReadData(flags, ids, counts, ratio32, ratio64, amounts, created, names);
    }

    private static Range GetBatchRange(int totalLength, int batchCount, int batchIndex)
    {
        var batchSize = totalLength / batchCount;
        var start = batchIndex * batchSize;
        var end = batchIndex == batchCount - 1 ? totalLength : start + batchSize;
        return start..end;
    }

    private sealed record NullableReadData(
        bool?[] Flags,
        int?[] Ids,
        long?[] Counts,
        float?[] Ratio32,
        double?[] Ratio64,
        decimal?[] Amounts,
        DateTime?[] Created,
        string?[] Names);
}
