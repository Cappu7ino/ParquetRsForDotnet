using System.Linq;
using Apache.Arrow;
using Apache.Arrow.Memory;
using Apache.Arrow.Types;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using ManagedParquetFileWriter = ParquetRsForDotnet.ParquetFileWriter;
using ArrowTimeUnit = Apache.Arrow.Types.TimeUnit;

namespace ParquetRsForDotnet.Benchmarks;

/// <summary>
/// Measures the new writer with Arrow arrays prepared outside the timed section so the benchmark
/// isolates write/export cost rather than Arrow array construction cost.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class PrebuiltArrowBatchWriterBenchmarks
{
    private static readonly MemoryAllocator Allocator = MemoryAllocator.Default.Value;

    private MixedBatchData mixedData = default!;
    private NoDecimalBatchData noDecimalData = default!;
    private ParquetSchema mixedSchema = default!;
    private ParquetSchema noDecimalSchema = default!;
    private IReadOnlyList<IArrowArray>? mixedColumns;
    private IReadOnlyList<IArrowArray>? noDecimalColumns;

    [Params(10_000, 1_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        mixedData = CreateMixedBatchData(RowCount);
        noDecimalData = CreateNoDecimalBatchData(RowCount);

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
    }

    [IterationSetup(Target = nameof(WriteMixedPrebuiltArrowBatch))]
    public void PrepareMixedPrebuiltArrowBatch()
    {
        mixedColumns = CreateMixedArrowColumns(mixedData);
    }

    [IterationCleanup(Target = nameof(WriteMixedPrebuiltArrowBatch))]
    public void CleanupMixedPrebuiltArrowBatch()
    {
        DisposeColumns(mixedColumns);
        mixedColumns = null;
    }

    [IterationSetup(Target = nameof(WriteNoDecimalPrebuiltArrowBatch))]
    public void PrepareNoDecimalPrebuiltArrowBatch()
    {
        noDecimalColumns = CreateNoDecimalArrowColumns(noDecimalData);
    }

    [IterationCleanup(Target = nameof(WriteNoDecimalPrebuiltArrowBatch))]
    public void CleanupNoDecimalPrebuiltArrowBatch()
    {
        DisposeColumns(noDecimalColumns);
        noDecimalColumns = null;
    }

    [Benchmark]
    public long WriteMixedPrebuiltArrowBatch()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, mixedSchema, CreateOptions());

        writer.WriteBatch(mixedColumns!);
        writer.Finish();

        return destination.Length;
    }

    [Benchmark]
    public long WriteNoDecimalPrebuiltArrowBatch()
    {
        using var destination = new MemoryStream();
        using var writer = new ManagedParquetFileWriter(destination, noDecimalSchema, CreateOptions());

        writer.WriteBatch(noDecimalColumns!);
        writer.Finish();

        return destination.Length;
    }

    private ParquetWriteOptions CreateOptions()
    {
        return new ParquetWriteOptions
        {
            Compression = ParquetCompression.Snappy,
            StatisticsLevel = ParquetStatisticsLevel.Chunk,
            MaxRowGroupRows = RowCount,
        };
    }

    private static IReadOnlyList<IArrowArray> CreateMixedArrowColumns(MixedBatchData data)
    {
        var idBuilder = new Int32Array.Builder();
        var nameBuilder = new StringArray.Builder();
        var amountBuilder = new Decimal128Array.Builder(new Decimal128Type(29, 4));
        var createdBuilder = new TimestampArray.Builder(new TimestampType(ArrowTimeUnit.Millisecond, "UTC"));
        idBuilder.Reserve(data.Ids.Length);
        nameBuilder.Reserve(data.Ids.Length);
        amountBuilder.Reserve(data.Ids.Length);
        createdBuilder.Reserve(data.Ids.Length);

        for (var i = 0; i < data.Ids.Length; i++)
        {
            if (data.Ids[i] is int id)
            {
                idBuilder.Append(id);
            }
            else
            {
                idBuilder.AppendNull();
            }

            if (data.Names[i] is string name)
            {
                nameBuilder.Append(name, System.Text.Encoding.UTF8);
            }
            else
            {
                nameBuilder.AppendNull();
            }

            if (data.Amounts[i] is decimal amount)
            {
                amountBuilder.Append(amount);
            }
            else
            {
                amountBuilder.AppendNull();
            }

            if (data.Created[i] is DateTime created)
            {
                createdBuilder.Append(new DateTimeOffset(created));
            }
            else
            {
                createdBuilder.AppendNull();
            }
        }

        return
        [
            idBuilder.Build(Allocator),
            nameBuilder.Build(Allocator),
            amountBuilder.Build(Allocator),
            createdBuilder.Build(Allocator),
        ];
    }

    private static IReadOnlyList<IArrowArray> CreateNoDecimalArrowColumns(NoDecimalBatchData data)
    {
        var idBuilder = new Int32Array.Builder();
        var nameBuilder = new StringArray.Builder();
        var createdBuilder = new TimestampArray.Builder(new TimestampType(ArrowTimeUnit.Millisecond, "UTC"));
        var flagBuilder = new Int32Array.Builder();
        idBuilder.Reserve(data.Ids.Length);
        nameBuilder.Reserve(data.Ids.Length);
        createdBuilder.Reserve(data.Ids.Length);
        flagBuilder.Reserve(data.Ids.Length);

        for (var i = 0; i < data.Ids.Length; i++)
        {
            if (data.Ids[i] is int id)
            {
                idBuilder.Append(id);
            }
            else
            {
                idBuilder.AppendNull();
            }

            if (data.Names[i] is string name)
            {
                nameBuilder.Append(name, System.Text.Encoding.UTF8);
            }
            else
            {
                nameBuilder.AppendNull();
            }

            if (data.Created[i] is DateTime created)
            {
                createdBuilder.Append(new DateTimeOffset(created));
            }
            else
            {
                createdBuilder.AppendNull();
            }

            if (data.Flags[i] is int flag)
            {
                flagBuilder.Append(flag);
            }
            else
            {
                flagBuilder.AppendNull();
            }
        }

        return
        [
            idBuilder.Build(Allocator),
            nameBuilder.Build(Allocator),
            createdBuilder.Build(Allocator),
            flagBuilder.Build(Allocator),
        ];
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

    private static void DisposeColumns(IReadOnlyList<IArrowArray>? columns)
    {
        if (columns is null)
        {
            return;
        }

        foreach (var disposable in columns.OfType<IDisposable>())
        {
            disposable.Dispose();
        }
    }

    private sealed class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.MediumRun
                .WithRuntime(BenchmarkDotNet.Environments.CoreRuntime.Core80)
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                .WithId("InProcessNet80_PrebuiltArrow"));
        }
    }

    private sealed record MixedBatchData(int?[] Ids, string?[] Names, decimal?[] Amounts, DateTime?[] Created);

    private sealed record NoDecimalBatchData(int?[] Ids, string?[] Names, DateTime?[] Created, int?[] Flags);
}
