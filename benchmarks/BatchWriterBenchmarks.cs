using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using ParquetSharp;
using ManagedParquetFileWriter = ParquetRsForDotnet.ParquetFileWriter;
using ParquetSharpTimeUnit = ParquetSharp.TimeUnit;

namespace ParquetRsForDotnet.Benchmarks;

/// <summary>
/// Compares true-parity multi-batch writes against ParquetSharp for two representative shapes:
/// a mixed schema and a no-decimal schema. Both sides write one row group per batch.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class BatchWriterBenchmarks
{
    private const int MultiBatchCount = 10;

    private MixedBatchData mixedData = default!;
    private NoDecimalBatchData noDecimalData = default!;
    private NoDecimalNonNullableBatchData noDecimalNonNullableData = default!;
    private NoDecimalStringBatchData noDecimalStringData = default!;
    private ParquetSchema mixedSchema = default!;
    private ParquetSchema noDecimalSchema = default!;
    private ParquetSchema noDecimalNonNullableSchema = default!;
    private ParquetSchema noDecimalStringSchema = default!;
    private MixedBatchData[] mixedBatches = default!;
    private NoDecimalBatchData[] noDecimalBatches = default!;
    private NoDecimalNonNullableBatchData[] noDecimalNonNullableBatches = default!;
    private NoDecimalStringBatchData[] noDecimalStringBatches = default!;

    [Params(10_000, 1_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        mixedData = CreateMixedBatchData(RowCount);
        noDecimalData = CreateNoDecimalBatchData(RowCount);
        noDecimalNonNullableData = CreateNoDecimalNonNullableBatchData(RowCount);
        noDecimalStringData = CreateNoDecimalStringBatchData(RowCount);
        mixedBatches = SplitMixedBatchData(mixedData, MultiBatchCount);
        noDecimalBatches = SplitNoDecimalBatchData(noDecimalData, MultiBatchCount);
        noDecimalNonNullableBatches = SplitNoDecimalNonNullableBatchData(noDecimalNonNullableData, MultiBatchCount);
        noDecimalStringBatches = SplitNoDecimalStringBatchData(noDecimalStringData, MultiBatchCount);

        mixedSchema = new ParquetSchema(
        [
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
            new ParquetColumn("amount", new ParquetDecimalSettings(29, 4), isNullable: true),
            new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
        ]);

        noDecimalSchema = new ParquetSchema(
        [
            new ParquetColumn("id", ParquetColumnType.Int32, isNullable: true),
            new ParquetColumn("name", ParquetColumnType.String, isNullable: true),
            new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC"), isNullable: true),
            new ParquetColumn("flag", ParquetColumnType.Int32, isNullable: true),
        ]);

        noDecimalNonNullableSchema = new ParquetSchema(
        [
            new ParquetColumn("id", ParquetColumnType.Int32),
            new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC")),
            new ParquetColumn("flag", ParquetColumnType.Int32),
        ]);

        noDecimalStringSchema = new ParquetSchema(
        [
            new ParquetColumn("id", ParquetColumnType.Int32),
            new ParquetColumn("name", ParquetColumnType.String),
            new ParquetColumn("created", new ParquetTimestampSettings(ParquetTimestampUnit.Millisecond, "UTC")),
            new ParquetColumn("flag", ParquetColumnType.Int32),
        ]);
    }

    [Benchmark]
    public long WriteMixedMultiBatchWithParquetRsForDotnet()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, mixedSchema, CreateMultiBatchOptions());

        foreach (var batch in mixedBatches)
        {
            writer.WriteBatch(batch.Ids, batch.Names, batch.Amounts, batch.Created);
        }

        writer.Finish();
        return destination.Length;
    }

    [Benchmark]
    public long WriteMixedMultiBatchWithParquetSharp()
    {
        using var destination = new MemoryStream();
        using var writerPropertiesBuilder = new WriterPropertiesBuilder();
        using var writerProperties = writerPropertiesBuilder.Compression(Compression.Snappy).Build();

        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int?>("id"),
            new ParquetSharp.Column<string>("name"),
            new ParquetSharp.Column<decimal?>("amount", LogicalType.Decimal(29, 4)),
            new ParquetSharp.Column<DateTime?>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
        };

        using var writer = new ParquetSharp.ParquetFileWriter(destination, columns, logicalTypeFactory: null, writerProperties, keyValueMetadata: null, leaveOpen: true);

        foreach (var batch in mixedBatches)
        {
            using var rowGroupWriter = writer.AppendRowGroup();

            using (var idWriter = rowGroupWriter.NextColumn().LogicalWriter<int?>())
            {
                idWriter.WriteBatch(batch.Ids);
            }

            using (var nameWriter = rowGroupWriter.NextColumn().LogicalWriter<string?>())
            {
                nameWriter.WriteBatch(batch.Names);
            }

            using (var amountWriter = rowGroupWriter.NextColumn().LogicalWriter<decimal?>())
            {
                amountWriter.WriteBatch(batch.Amounts);
            }

            using (var createdWriter = rowGroupWriter.NextColumn().LogicalWriter<DateTime?>())
            {
                createdWriter.WriteBatch(batch.Created);
            }
        }

        writer.Close();
        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalMultiBatchWithParquetRsForDotnet()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, noDecimalSchema, CreateMultiBatchOptions());

        foreach (var batch in noDecimalBatches)
        {
            writer.WriteBatch(batch.Ids, batch.Names, batch.Created, batch.Flags);
        }

        writer.Finish();
        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalMultiBatchWithParquetSharp()
    {
        using var destination = new MemoryStream();
        using var writerPropertiesBuilder = new WriterPropertiesBuilder();
        using var writerProperties = writerPropertiesBuilder.Compression(Compression.Snappy).Build();

        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int?>("id"),
            new ParquetSharp.Column<string>("name"),
            new ParquetSharp.Column<DateTime?>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
            new ParquetSharp.Column<int?>("flag"),
        };

        using var writer = new ParquetSharp.ParquetFileWriter(destination, columns, logicalTypeFactory: null, writerProperties, keyValueMetadata: null, leaveOpen: true);

        foreach (var batch in noDecimalBatches)
        {
            using var rowGroupWriter = writer.AppendRowGroup();

            using (var idWriter = rowGroupWriter.NextColumn().LogicalWriter<int?>())
            {
                idWriter.WriteBatch(batch.Ids);
            }

            using (var nameWriter = rowGroupWriter.NextColumn().LogicalWriter<string?>())
            {
                nameWriter.WriteBatch(batch.Names);
            }

            using (var createdWriter = rowGroupWriter.NextColumn().LogicalWriter<DateTime?>())
            {
                createdWriter.WriteBatch(batch.Created);
            }

            using (var flagWriter = rowGroupWriter.NextColumn().LogicalWriter<int?>())
            {
                flagWriter.WriteBatch(batch.Flags);
            }
        }

        writer.Close();
        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalNonNullableBuilderOnlyWithParquetRsForDotnet()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, noDecimalNonNullableSchema, CreateBuilderOnlyOptions());

        foreach (var batch in noDecimalNonNullableBatches)
        {
            writer.WriteBatch(batch.Ids, batch.Created, batch.Flags);
        }

        writer.Finish();
        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalNonNullableLowLevelWithParquetRsForDotnet()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, noDecimalNonNullableSchema, CreateLowLevelFixedWidthOptions());

        foreach (var batch in noDecimalNonNullableBatches)
        {
            writer.WriteBatch(batch.Ids, batch.Created, batch.Flags);
        }

        writer.Finish();
        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalNonNullableWithParquetSharp()
    {
        using var destination = new MemoryStream();
        using var writerPropertiesBuilder = new WriterPropertiesBuilder();
        using var writerProperties = writerPropertiesBuilder.Compression(Compression.Snappy).Build();

        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int>("id"),
            new ParquetSharp.Column<DateTime>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
            new ParquetSharp.Column<int>("flag"),
        };

        using var writer = new ParquetSharp.ParquetFileWriter(destination, columns, logicalTypeFactory: null, writerProperties, keyValueMetadata: null, leaveOpen: true);

        foreach (var batch in noDecimalNonNullableBatches)
        {
            using var rowGroupWriter = writer.AppendRowGroup();

            using (var idWriter = rowGroupWriter.NextColumn().LogicalWriter<int>())
            {
                idWriter.WriteBatch(batch.Ids);
            }

            using (var createdWriter = rowGroupWriter.NextColumn().LogicalWriter<DateTime>())
            {
                createdWriter.WriteBatch(batch.Created);
            }

            using (var flagWriter = rowGroupWriter.NextColumn().LogicalWriter<int>())
            {
                flagWriter.WriteBatch(batch.Flags);
            }
        }

        writer.Close();
        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalStringBuilderOnlyWithParquetRsForDotnet()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, noDecimalStringSchema, CreateBuilderOnlyOptions());

        foreach (var batch in noDecimalStringBatches)
        {
            writer.WriteBatch(batch.Ids, batch.Names, batch.Created, batch.Flags);
        }

        writer.Finish();
        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalStringLowLevelWithParquetRsForDotnet()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, noDecimalStringSchema, CreateLowLevelFixedWidthOptions());

        foreach (var batch in noDecimalStringBatches)
        {
            writer.WriteBatch(batch.Ids, batch.Names, batch.Created, batch.Flags);
        }

        writer.Finish();
        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalStringWithParquetSharp()
    {
        using var destination = new MemoryStream();
        using var writerPropertiesBuilder = new WriterPropertiesBuilder();
        using var writerProperties = writerPropertiesBuilder.Compression(Compression.Snappy).Build();

        var columns = new ParquetSharp.Column[]
        {
            new ParquetSharp.Column<int>("id"),
            new ParquetSharp.Column<string>("name"),
            new ParquetSharp.Column<DateTime>("created", LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: ParquetSharpTimeUnit.Millis)),
            new ParquetSharp.Column<int>("flag"),
        };

        using var writer = new ParquetSharp.ParquetFileWriter(destination, columns, logicalTypeFactory: null, writerProperties, keyValueMetadata: null, leaveOpen: true);

        foreach (var batch in noDecimalStringBatches)
        {
            using var rowGroupWriter = writer.AppendRowGroup();

            using (var idWriter = rowGroupWriter.NextColumn().LogicalWriter<int>())
            {
                idWriter.WriteBatch(batch.Ids);
            }

            using (var nameWriter = rowGroupWriter.NextColumn().LogicalWriter<string>())
            {
                nameWriter.WriteBatch(batch.Names);
            }

            using (var createdWriter = rowGroupWriter.NextColumn().LogicalWriter<DateTime>())
            {
                createdWriter.WriteBatch(batch.Created);
            }

            using (var flagWriter = rowGroupWriter.NextColumn().LogicalWriter<int>())
            {
                flagWriter.WriteBatch(batch.Flags);
            }
        }

        writer.Close();
        return destination.Length;
    }

    private ParquetWriteOptions CreateMultiBatchOptions()
    {
        return new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth,
            Compression = ParquetCompression.Snappy,
            StatisticsLevel = ParquetStatisticsLevel.Chunk,
            MaxRowGroupRows = RowCount / MultiBatchCount,
        };
    }

    private ParquetWriteOptions CreateBuilderOnlyOptions()
    {
        return new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.BuilderOnly,
            Compression = ParquetCompression.Snappy,
            StatisticsLevel = ParquetStatisticsLevel.Chunk,
            MaxRowGroupRows = RowCount / MultiBatchCount,
        };
    }

    private ParquetWriteOptions CreateLowLevelFixedWidthOptions()
    {
        return new ParquetWriteOptions
        {
            ArrowMaterializationMode = ArrowMaterializationMode.LowLevelFixedWidth,
            Compression = ParquetCompression.Snappy,
            StatisticsLevel = ParquetStatisticsLevel.Chunk,
            MaxRowGroupRows = RowCount / MultiBatchCount,
        };
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

    private static NoDecimalNonNullableBatchData CreateNoDecimalNonNullableBatchData(int rowCount)
    {
        var ids = new int[rowCount];
        var created = new DateTime[rowCount];
        var flags = new int[rowCount];

        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < rowCount; i++)
        {
            ids[i] = i;
            created[i] = start.AddMinutes(i);
            flags[i] = i % 2 == 0 ? 1 : 0;
        }

        return new NoDecimalNonNullableBatchData(ids, created, flags);
    }

    private static NoDecimalStringBatchData CreateNoDecimalStringBatchData(int rowCount)
    {
        var ids = new int[rowCount];
        var names = new string[rowCount];
        var created = new DateTime[rowCount];
        var flags = new int[rowCount];

        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < rowCount; i++)
        {
            ids[i] = i;
            names[i] = $"row-{i}";
            created[i] = start.AddMinutes(i);
            flags[i] = i % 2 == 0 ? 1 : 0;
        }

        return new NoDecimalStringBatchData(ids, names, created, flags);
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

    private static NoDecimalNonNullableBatchData[] SplitNoDecimalNonNullableBatchData(NoDecimalNonNullableBatchData source, int batchCount)
    {
        return Enumerable.Range(0, batchCount)
            .Select(batchIndex =>
            {
                var range = GetBatchRange(source.Ids.Length, batchCount, batchIndex);
                return new NoDecimalNonNullableBatchData(
                    source.Ids[range].ToArray(),
                    source.Created[range].ToArray(),
                    source.Flags[range].ToArray());
            })
            .ToArray();
    }

    private static NoDecimalStringBatchData[] SplitNoDecimalStringBatchData(NoDecimalStringBatchData source, int batchCount)
    {
        return Enumerable.Range(0, batchCount)
            .Select(batchIndex =>
            {
                var range = GetBatchRange(source.Ids.Length, batchCount, batchIndex);
                return new NoDecimalStringBatchData(
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
                .WithId("InProcessNet80"));
        }
    }

    private sealed record MixedBatchData(int?[] Ids, string?[] Names, decimal?[] Amounts, DateTime?[] Created);

    private sealed record NoDecimalBatchData(int?[] Ids, string?[] Names, DateTime?[] Created, int?[] Flags);

    private sealed record NoDecimalNonNullableBatchData(int[] Ids, DateTime[] Created, int[] Flags);

    private sealed record NoDecimalStringBatchData(int[] Ids, string[] Names, DateTime[] Created, int[] Flags);
}
