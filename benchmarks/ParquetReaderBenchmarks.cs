using Apache.Arrow;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using ParquetSharp;
using ParquetSharp.IO;
using ManagedParquetFileReader = ParquetRsForDotnet.ParquetFileReader;
using ParquetSharpTimeUnit = ParquetSharp.TimeUnit;

namespace ParquetRsForDotnet.Benchmarks;

/// <summary>
/// Compares Arrow-native row-group column reads against ParquetSharp using the same externally produced parquet bytes.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class ParquetReaderBenchmarks
{
    private const int MultiBatchCount = 10;

    private byte[] mixedFileBytes = default!;
    private byte[] noDecimalFileBytes = default!;

    [Params(10_000, 1_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        mixedFileBytes = CreateMixedFileBytes(RowCount);
        noDecimalFileBytes = CreateNoDecimalFileBytes(RowCount);
    }

    [Benchmark]
    public int ReadMixedColumnsWithParquetRsForDotnet()
    {
        using var source = new MemoryStream(mixedFileBytes, writable: false);
        using var reader = new ManagedParquetFileReader(source);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
        {
            using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);
            score += rowGroup.ReadColumn(0).Length;
            score += rowGroup.ReadColumn(1).Length;
            score += rowGroup.ReadColumn(2).Length;
            score += rowGroup.ReadColumn(3).Length;
        }

        return score;
    }

    [Benchmark]
    public int ReadMixedClrColumnsWithParquetRsForDotnet()
    {
        using var source = new MemoryStream(mixedFileBytes, writable: false);
        using var reader = new ManagedParquetFileReader(source);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
        {
            using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);
            score += rowGroup.ReadColumn<int?>(0).Length;
            score += rowGroup.ReadColumn<string>(1).Length;
            score += rowGroup.ReadColumn<decimal?>(2).Length;
            score += rowGroup.ReadColumn<DateTime?>(3).Length;
        }

        return score;
    }

    [Benchmark]
    public int ReadMixedColumnsWithParquetSharp()
    {
        using var source = new MemoryStream(mixedFileBytes, writable: false);
        using var input = new ManagedRandomAccessFile(source);
        using var reader = new ParquetSharp.ParquetFileReader(input);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.FileMetaData.NumRowGroups; rowGroupIndex++)
        {
            using var rowGroup = reader.RowGroup(rowGroupIndex);

            using (var idReader = rowGroup.Column(0).LogicalReader<int?>())
            {
                score += idReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var nameReader = rowGroup.Column(1).LogicalReader<string>())
            {
                score += nameReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var amountReader = rowGroup.Column(2).LogicalReader<decimal?>())
            {
                score += amountReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var createdReader = rowGroup.Column(3).LogicalReader<DateTime?>())
            {
                score += createdReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }
        }

        return score;
    }

    [Benchmark]
    public int ReadNoDecimalColumnsWithParquetRsForDotnet()
    {
        using var source = new MemoryStream(noDecimalFileBytes, writable: false);
        using var reader = new ManagedParquetFileReader(source);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
        {
            using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);
            score += rowGroup.ReadColumn(0).Length;
            score += rowGroup.ReadColumn(1).Length;
            score += rowGroup.ReadColumn(2).Length;
            score += rowGroup.ReadColumn(3).Length;
        }

        return score;
    }

    [Benchmark]
    public int ReadNoDecimalClrColumnsWithParquetRsForDotnet()
    {
        using var source = new MemoryStream(noDecimalFileBytes, writable: false);
        using var reader = new ManagedParquetFileReader(source);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
        {
            using var rowGroup = reader.OpenRowGroupReader(rowGroupIndex);
            score += rowGroup.ReadColumn<int?>(0).Length;
            score += rowGroup.ReadColumn<string>(1).Length;
            score += rowGroup.ReadColumn<DateTime?>(2).Length;
            score += rowGroup.ReadColumn<int?>(3).Length;
        }

        return score;
    }

    [Benchmark]
    public int ReadNoDecimalColumnsWithParquetSharp()
    {
        using var source = new MemoryStream(noDecimalFileBytes, writable: false);
        using var input = new ManagedRandomAccessFile(source);
        using var reader = new ParquetSharp.ParquetFileReader(input);

        var score = 0;
        for (var rowGroupIndex = 0; rowGroupIndex < reader.FileMetaData.NumRowGroups; rowGroupIndex++)
        {
            using var rowGroup = reader.RowGroup(rowGroupIndex);

            using (var idReader = rowGroup.Column(0).LogicalReader<int?>())
            {
                score += idReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var nameReader = rowGroup.Column(1).LogicalReader<string>())
            {
                score += nameReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var createdReader = rowGroup.Column(2).LogicalReader<DateTime?>())
            {
                score += createdReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }

            using (var flagReader = rowGroup.Column(3).LogicalReader<int?>())
            {
                score += flagReader.ReadAll((int)rowGroup.MetaData.NumRows).Length;
            }
        }

        return score;
    }

    private static byte[] CreateMixedFileBytes(int rowCount)
    {
        var data = CreateMixedBatchData(rowCount);
        var batches = SplitMixedBatchData(data, MultiBatchCount);
        using var destination = new MemoryStream();

        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int?>("id"),
            new ParquetSharp.Column<string>("name"),
            new ParquetSharp.Column<decimal?>("amount", LogicalType.Decimal(29, 4)),
            new ParquetSharp.Column<DateTime?>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
        };

        using (var writer = new ParquetSharp.ParquetFileWriter(destination, columns, leaveOpen: true))
        {
            foreach (var batch in batches)
            {
                using var rowGroup = writer.AppendRowGroup();
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(batch.Ids);
                rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(batch.Names);
                rowGroup.NextColumn().LogicalWriter<decimal?>().WriteBatch(batch.Amounts);
                rowGroup.NextColumn().LogicalWriter<DateTime?>().WriteBatch(batch.Created);
            }

            writer.Close();
        }

        return destination.ToArray();
    }

    private static byte[] CreateNoDecimalFileBytes(int rowCount)
    {
        var data = CreateNoDecimalBatchData(rowCount);
        var batches = SplitNoDecimalBatchData(data, MultiBatchCount);
        using var destination = new MemoryStream();

        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int?>("id"),
            new ParquetSharp.Column<string>("name"),
            new ParquetSharp.Column<DateTime?>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
            new ParquetSharp.Column<int?>("flag"),
        };

        using (var writer = new ParquetSharp.ParquetFileWriter(destination, columns, leaveOpen: true))
        {
            foreach (var batch in batches)
            {
                using var rowGroup = writer.AppendRowGroup();
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(batch.Ids);
                rowGroup.NextColumn().LogicalWriter<string?>().WriteBatch(batch.Names);
                rowGroup.NextColumn().LogicalWriter<DateTime?>().WriteBatch(batch.Created);
                rowGroup.NextColumn().LogicalWriter<int?>().WriteBatch(batch.Flags);
            }

            writer.Close();
        }

        return destination.ToArray();
    }

    private static MixedBatchData CreateMixedBatchData(int rowCount)
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

        return new MixedBatchData(ids, names, amounts, created);
    }

    private static NoDecimalBatchData CreateNoDecimalBatchData(int rowCount)
    {
        var ids = new int?[rowCount];
        var names = new string?[rowCount];
        var created = new DateTime?[rowCount];
        var flags = new int?[rowCount];

        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < rowCount; i++)
        {
            ids[i] = i % 11 == 0 ? null : i;
            names[i] = i % 5 == 0 ? null : $"row-{i}";
            created[i] = i % 13 == 0 ? null : start.AddMinutes(i);
            flags[i] = i % 7 == 0 ? null : (i % 2 == 0 ? 1 : 0);
        }

        return new NoDecimalBatchData(ids, names, created, flags);
    }

    private static MixedBatchData[] SplitMixedBatchData(MixedBatchData source, int batchCount)
    {
        return Enumerable.Range(0, batchCount)
            .Select(batchIndex =>
            {
                var range = GetBatchRange(source.Ids.Length, batchCount, batchIndex);
                return new MixedBatchData(
                    source.Ids[range].ToArray(),
                    source.Names[range].ToArray(),
                    source.Amounts[range].ToArray(),
                    source.Created[range].ToArray());
            })
            .ToArray();
    }

    private static NoDecimalBatchData[] SplitNoDecimalBatchData(NoDecimalBatchData source, int batchCount)
    {
        return Enumerable.Range(0, batchCount)
            .Select(batchIndex =>
            {
                var range = GetBatchRange(source.Ids.Length, batchCount, batchIndex);
                return new NoDecimalBatchData(
                    source.Ids[range].ToArray(),
                    source.Names[range].ToArray(),
                    source.Created[range].ToArray(),
                    source.Flags[range].ToArray());
            })
            .ToArray();
    }

    private static Range GetBatchRange(int totalLength, int batchCount, int batchIndex)
    {
        var batchSize = totalLength / batchCount;
        var start = batchIndex * batchSize;
        var end = batchIndex == batchCount - 1 ? totalLength : start + batchSize;
        return start..end;
    }

    private sealed class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.MediumRun
                .WithRuntime(BenchmarkDotNet.Environments.CoreRuntime.Core80)
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithId("InProcessNet80_Read"));
        }
    }

    private sealed record MixedBatchData(int?[] Ids, string?[] Names, decimal?[] Amounts, DateTime?[] Created);

    private sealed record NoDecimalBatchData(int?[] Ids, string?[] Names, DateTime?[] Created, int?[] Flags);

}
