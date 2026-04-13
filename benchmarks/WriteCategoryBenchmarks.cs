using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using ParquetSharp;
using System.Linq;
using ManagedParquetFileWriter = ParquetRsForDotnet.ParquetFileWriter;
using ParquetSharpTimeUnit = ParquetSharp.TimeUnit;

namespace ParquetRsForDotnet.Benchmarks;

[MemoryDiagnoser]
[NativeMemoryProfiler]
[Config(typeof(ProductionBenchmarkConfig))]
[BenchmarkCategory("Write", "NonNullable")]
public class NonNullableWriteCategoryBenchmarks
{
    private const int MultiBatchCount = 10;

    private NonNullableWriteData[] batches = default!;
    private ParquetSchema schema = default!;

    [Params(10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        schema = new ParquetSchema(
        [
            new ParquetColumn("flag", ParquetColumnType.Boolean),
            new ParquetColumn("id", ParquetColumnType.Int32),
            new ParquetColumn("count", ParquetColumnType.Int64),
            new ParquetColumn("ratio32", ParquetColumnType.Float32),
            new ParquetColumn("ratio64", ParquetColumnType.Float64),
            new ParquetColumn("amount", new ParquetDecimalSettings(29, 4)),
            new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC")),
            new ParquetColumn("name", ParquetColumnType.String),
        ]);

        batches = Split(CreateNonNullableWriteData(RowCount), MultiBatchCount);
    }

    [Benchmark]
    public long WriteWithParquetRsForDotnet()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, schema, CreateOptions());

        foreach (var batch in batches)
        {
            writer.WriteBatch(batch.Flags, batch.Ids, batch.Counts, batch.Ratio32, batch.Ratio64, batch.Amounts, batch.Created, batch.Names);
        }

        writer.Finish();
        return destination.Length;
    }

    [Benchmark(Baseline = true)]
    public long WriteWithParquetSharp()
    {
        using var destination = new MemoryStream();
        using var writerPropertiesBuilder = new WriterPropertiesBuilder();
        using var writerProperties = writerPropertiesBuilder.Compression(Compression.Snappy).Build();

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

        using var writer = new ParquetSharp.ParquetFileWriter(destination, columns, logicalTypeFactory: null, writerProperties, keyValueMetadata: null, leaveOpen: true);

        foreach (var batch in batches)
        {
            using var rowGroupWriter = writer.AppendRowGroup();

            using (var flagWriter = rowGroupWriter.NextColumn().LogicalWriter<bool>())
            {
                flagWriter.WriteBatch(batch.Flags);
            }

            using (var idWriter = rowGroupWriter.NextColumn().LogicalWriter<int>())
            {
                idWriter.WriteBatch(batch.Ids);
            }

            using (var countWriter = rowGroupWriter.NextColumn().LogicalWriter<long>())
            {
                countWriter.WriteBatch(batch.Counts);
            }

            using (var ratio32Writer = rowGroupWriter.NextColumn().LogicalWriter<float>())
            {
                ratio32Writer.WriteBatch(batch.Ratio32);
            }

            using (var ratio64Writer = rowGroupWriter.NextColumn().LogicalWriter<double>())
            {
                ratio64Writer.WriteBatch(batch.Ratio64);
            }

            using (var amountWriter = rowGroupWriter.NextColumn().LogicalWriter<decimal>())
            {
                amountWriter.WriteBatch(batch.Amounts);
            }

            using (var createdWriter = rowGroupWriter.NextColumn().LogicalWriter<DateTime>())
            {
                createdWriter.WriteBatch(batch.Created);
            }

            using (var nameWriter = rowGroupWriter.NextColumn().LogicalWriter<string>())
            {
                nameWriter.WriteBatch(batch.Names);
            }
        }

        writer.Close();
        return destination.Length;
    }

    private ParquetWriteOptions CreateOptions()
    {
        return new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth,
            Compression = ParquetCompression.Snappy,
            StatisticsLevel = ParquetStatisticsLevel.Chunk,
            MaxRowGroupRows = RowCount / MultiBatchCount,
        };
    }

    private static NonNullableWriteData CreateNonNullableWriteData(int rowCount)
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

        return new NonNullableWriteData(flags, ids, counts, ratio32, ratio64, amounts, created, names);
    }

    private static NonNullableWriteData[] Split(NonNullableWriteData source, int batchCount)
    {
        return Enumerable.Range(0, batchCount)
            .Select(batchIndex =>
            {
                var range = GetBatchRange(source.Ids.Length, batchCount, batchIndex);
                return new NonNullableWriteData(
                    source.Flags[range].ToArray(),
                    source.Ids[range].ToArray(),
                    source.Counts[range].ToArray(),
                    source.Ratio32[range].ToArray(),
                    source.Ratio64[range].ToArray(),
                    source.Amounts[range].ToArray(),
                    source.Created[range].ToArray(),
                    source.Names[range].ToArray());
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

    private sealed record NonNullableWriteData(
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
[BenchmarkCategory("Write", "Nullable")]
public class NullableWriteCategoryBenchmarks
{
    private const int MultiBatchCount = 10;

    private NullableWriteData[] batches = default!;
    private ParquetSchema schema = default!;

    [Params(10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        schema = new ParquetSchema(
        [
            new ParquetColumn("flag", ParquetColumnType.Boolean, isNullable: true),
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("count", ParquetColumnType.Int64, isNullable: true),
            new ParquetColumn("ratio32", ParquetColumnType.Float32, isNullable: true),
            new ParquetColumn("ratio64", ParquetColumnType.Float64, isNullable: true),
            new ParquetColumn("amount", new ParquetDecimalSettings(29, 4), isNullable: true),
            new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
        ]);

        batches = Split(CreateNullableWriteData(RowCount), MultiBatchCount);
    }

    [Benchmark]
    public long WriteWithParquetRsForDotnet()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, schema, CreateOptions());

        foreach (var batch in batches)
        {
            writer.WriteBatch(batch.Flags, batch.Ids, batch.Counts, batch.Ratio32, batch.Ratio64, batch.Amounts, batch.Created, batch.Names);
        }

        writer.Finish();
        return destination.Length;
    }

    [Benchmark(Baseline = true)]
    public long WriteWithParquetSharp()
    {
        using var destination = new MemoryStream();
        using var writerPropertiesBuilder = new WriterPropertiesBuilder();
        using var writerProperties = writerPropertiesBuilder.Compression(Compression.Snappy).Build();

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

        using var writer = new ParquetSharp.ParquetFileWriter(destination, columns, logicalTypeFactory: null, writerProperties, keyValueMetadata: null, leaveOpen: true);

        foreach (var batch in batches)
        {
            using var rowGroupWriter = writer.AppendRowGroup();

            using (var flagWriter = rowGroupWriter.NextColumn().LogicalWriter<bool?>())
            {
                flagWriter.WriteBatch(batch.Flags);
            }

            using (var idWriter = rowGroupWriter.NextColumn().LogicalWriter<int?>())
            {
                idWriter.WriteBatch(batch.Ids);
            }

            using (var countWriter = rowGroupWriter.NextColumn().LogicalWriter<long?>())
            {
                countWriter.WriteBatch(batch.Counts);
            }

            using (var ratio32Writer = rowGroupWriter.NextColumn().LogicalWriter<float?>())
            {
                ratio32Writer.WriteBatch(batch.Ratio32);
            }

            using (var ratio64Writer = rowGroupWriter.NextColumn().LogicalWriter<double?>())
            {
                ratio64Writer.WriteBatch(batch.Ratio64);
            }

            using (var amountWriter = rowGroupWriter.NextColumn().LogicalWriter<decimal?>())
            {
                amountWriter.WriteBatch(batch.Amounts);
            }

            using (var createdWriter = rowGroupWriter.NextColumn().LogicalWriter<DateTime?>())
            {
                createdWriter.WriteBatch(batch.Created);
            }

            using (var nameWriter = rowGroupWriter.NextColumn().LogicalWriter<string?>())
            {
                nameWriter.WriteBatch(batch.Names);
            }
        }

        writer.Close();
        return destination.Length;
    }

    private ParquetWriteOptions CreateOptions()
    {
        return new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth,
            Compression = ParquetCompression.Snappy,
            StatisticsLevel = ParquetStatisticsLevel.Chunk,
            MaxRowGroupRows = RowCount / MultiBatchCount,
        };
    }

    private static NullableWriteData CreateNullableWriteData(int rowCount)
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

        return new NullableWriteData(flags, ids, counts, ratio32, ratio64, amounts, created, names);
    }

    private static NullableWriteData[] Split(NullableWriteData source, int batchCount)
    {
        return Enumerable.Range(0, batchCount)
            .Select(batchIndex =>
            {
                var range = GetBatchRange(source.Ids.Length, batchCount, batchIndex);
                return new NullableWriteData(
                    source.Flags[range].ToArray(),
                    source.Ids[range].ToArray(),
                    source.Counts[range].ToArray(),
                    source.Ratio32[range].ToArray(),
                    source.Ratio64[range].ToArray(),
                    source.Amounts[range].ToArray(),
                    source.Created[range].ToArray(),
                    source.Names[range].ToArray());
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

    private sealed record NullableWriteData(
        bool?[] Flags,
        int?[] Ids,
        long?[] Counts,
        float?[] Ratio32,
        double?[] Ratio64,
        decimal?[] Amounts,
        DateTime?[] Created,
        string?[] Names);
}

internal sealed class ProductionBenchmarkConfig : ManualConfig
{
    public ProductionBenchmarkConfig()
    {
        AddJob(Job.MediumRun
            .WithRuntime(BenchmarkDotNet.Environments.CoreRuntime.Core80)
            .WithGcServer(true)
            .WithId("Net80OutOfProcess"));

        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(CsvExporter.Default, JsonExporter.Brief);
        AddExporter(MarkdownExporter.GitHub);
    }
}
