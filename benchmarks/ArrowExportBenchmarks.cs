using System.Linq;
using Apache.Arrow;
using Apache.Arrow.C;
using Apache.Arrow.Memory;
using Apache.Arrow.Types;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using ArrowTimeUnit = Apache.Arrow.Types.TimeUnit;

namespace ParquetRsForDotnet.Benchmarks;

/// <summary>
/// Measures just the Arrow C Data export/free cost for prebuilt record batches.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class ArrowExportBenchmarks
{
    private static readonly MemoryAllocator Allocator = MemoryAllocator.Default.Value;

    private MixedBatchData mixedData = default!;
    private NoDecimalBatchData noDecimalData = default!;
    private Schema mixedSchema = default!;
    private Schema noDecimalSchema = default!;
    private RecordBatch? mixedBatch;
    private RecordBatch? noDecimalBatch;

    [Params(10_000, 1_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        mixedData = CreateMixedBatchData(RowCount);
        noDecimalData = CreateNoDecimalBatchData(RowCount);

        mixedSchema = new Schema(
        [
            new Field("id", Int32Type.Default, nullable: true),
            new Field("name", StringType.Default, nullable: true),
            new Field("amount", new Decimal128Type(29, 4), nullable: true),
            new Field("created", new TimestampType(ArrowTimeUnit.Millisecond, "UTC"), nullable: true),
        ], metadata: []);

        noDecimalSchema = new Schema(
        [
            new Field("id", Int32Type.Default, nullable: true),
            new Field("name", StringType.Default, nullable: true),
            new Field("created", new TimestampType(ArrowTimeUnit.Millisecond, "UTC"), nullable: true),
            new Field("flag", Int32Type.Default, nullable: true),
        ], metadata: []);
    }

    [IterationSetup(Target = nameof(ExportMixedRecordBatch))]
    public void PrepareMixedBatch()
    {
        mixedBatch = new RecordBatch(mixedSchema, CreateMixedArrowColumns(mixedData), mixedData.Ids.Length);
    }

    [IterationCleanup(Target = nameof(ExportMixedRecordBatch))]
    public void CleanupMixedBatch()
    {
        mixedBatch = null;
    }

    [IterationSetup(Target = nameof(ExportNoDecimalRecordBatch))]
    public void PrepareNoDecimalBatch()
    {
        noDecimalBatch = new RecordBatch(noDecimalSchema, CreateNoDecimalArrowColumns(noDecimalData), noDecimalData.Ids.Length);
    }

    [IterationCleanup(Target = nameof(ExportNoDecimalRecordBatch))]
    public void CleanupNoDecimalBatch()
    {
        noDecimalBatch = null;
    }

    [Benchmark]
    public unsafe int ExportMixedRecordBatch()
    {
        var batch = mixedBatch!.Clone();
        var cArray = CArrowArray.Create();

        try
        {
            CArrowArrayExporter.ExportRecordBatch(batch, cArray);
            return (int)cArray->n_children;
        }
        finally
        {
            CArrowArray.Free(cArray);
        }
    }

    [Benchmark]
    public unsafe int ExportNoDecimalRecordBatch()
    {
        var batch = noDecimalBatch!.Clone();
        var cArray = CArrowArray.Create();

        try
        {
            CArrowArrayExporter.ExportRecordBatch(batch, cArray);
            return (int)cArray->n_children;
        }
        finally
        {
            CArrowArray.Free(cArray);
        }
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

    private sealed class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.MediumRun
                .WithRuntime(BenchmarkDotNet.Environments.CoreRuntime.Core80)
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                .WithId("InProcessNet80_ArrowExport"));
        }
    }

    private sealed record MixedBatchData(int?[] Ids, string?[] Names, decimal?[] Amounts, DateTime?[] Created);

    private sealed record NoDecimalBatchData(int?[] Ids, string?[] Names, DateTime?[] Created, int?[] Flags);
}
