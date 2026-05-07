using System.Data.SqlTypes;
using System.Text;
using Apache.Arrow;
using Apache.Arrow.Memory;
using Apache.Arrow.Types;
using ParquetRsForDotnet.Internal.Materialization;

namespace ParquetRsForDotnet.Internal;

internal sealed class ClrArrayMaterializer
{
    private static readonly MemoryAllocator s_allocator = MemoryAllocator.Default.Value;
    private readonly ArrowMaterializationOptions _options;

    public ClrArrayMaterializer(ArrowMaterializationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IArrowArray Materialize(System.Array data, ColumnMaterializationContext context)
    {
        return data switch
        {
            bool[] values => BuildBoolean(values),
            bool?[] values => BuildBoolean(values),
            byte[] values when context.ArrowType is UInt8Type => BuildUInt8(values),
            byte?[] values => BuildUInt8(values),
            sbyte[] values => BuildInt8(values),
            sbyte?[] values => BuildInt8(values),
            short[] values => BuildInt16(values),
            short?[] values => BuildInt16(values),
            ushort[] values => BuildUInt16(values),
            ushort?[] values => BuildUInt16(values),
            int[] values => BuildInt32(values),
            int?[] values => BuildInt32(values),
            uint[] values => BuildUInt32(values),
            uint?[] values => BuildUInt32(values),
            long[] values => BuildInt64(values),
            long?[] values => BuildInt64(values),
            ulong[] values => BuildUInt64(values),
            ulong?[] values => BuildUInt64(values),
            float[] values => BuildFloat(values),
            float?[] values => BuildFloat(values),
            double[] values => BuildDouble(values),
            double?[] values => BuildDouble(values),
            string?[] values => BuildString(values),
            Guid[] values => BuildGuid(values),
            byte[][] values => BuildBinary(values),
#if NET8_0_OR_GREATER
            DateOnly[] values when context.ArrowType is Date32Type => BuildDate32(values),
            DateOnly[] values when context.ArrowType is Date64Type => BuildDate64(values),
            DateOnly?[] values when context.ArrowType is Date32Type => BuildDate32(values),
            DateOnly?[] values when context.ArrowType is Date64Type => BuildDate64(values),
#endif
            // DateTime date inputs are required for netstandard2.0 and also accepted
            // on net8.0 for callers that prefer a cross-target source shape.
            DateTime[] values when context.ArrowType is Date32Type => BuildDate32(values),
            DateTime[] values when context.ArrowType is Date64Type => BuildDate64(values),
            DateTime?[] values when context.ArrowType is Date32Type => BuildDate32(values),
            DateTime?[] values when context.ArrowType is Date64Type => BuildDate64(values),
            DateTime[] values => BuildTimestamp(values, (TimestampType)context.ArrowType),
            DateTime?[] values => BuildTimestamp(values, (TimestampType)context.ArrowType),
            decimal[] values => BuildDecimal(values, (Decimal128Type)context.ArrowType),
            decimal?[] values => BuildDecimal(values, (Decimal128Type)context.ArrowType),
            _ => throw new NotSupportedException($"Array-backed type '{data.GetType()}' is not supported for column '{context.ColumnName}'."),
        };
    }

    private static BooleanArray BuildBoolean(bool[] values)
        => BuildWithBuilder(values, static () => new BooleanArray.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private static BooleanArray BuildBoolean(bool?[] values)
        => BuildWithBuilder(
            values,
            static () => new BooleanArray.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) =>
            {
                if (value.HasValue)
                {
                    builder.Append(value.Value);
                }
                else
                {
                    builder.AppendNull();
                }
            },
            static builder => builder.Build(s_allocator));

    private Int8Array BuildInt8(sbyte[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new Int8Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private Int8Array BuildInt8(sbyte?[] values)
        => BuildWithBuilder(values, static () => new Int8Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private UInt8Array BuildUInt8(byte[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new UInt8Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private UInt8Array BuildUInt8(byte?[] values)
        => BuildWithBuilder(values, static () => new UInt8Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private Int16Array BuildInt16(short[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new Int16Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private Int16Array BuildInt16(short?[] values)
        => BuildWithBuilder(values, static () => new Int16Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private UInt16Array BuildUInt16(ushort[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new UInt16Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private UInt16Array BuildUInt16(ushort?[] values)
        => BuildWithBuilder(values, static () => new UInt16Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private Int32Array BuildInt32(int[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new Int32Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private Int32Array BuildInt32(int?[] values)
        => BuildWithBuilder(values, static () => new Int32Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private UInt32Array BuildUInt32(uint[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new UInt32Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private UInt32Array BuildUInt32(uint?[] values)
        => BuildWithBuilder(values, static () => new UInt32Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private Int64Array BuildInt64(long[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new Int64Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private Int64Array BuildInt64(long?[] values)
        => BuildWithBuilder(values, static () => new Int64Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private UInt64Array BuildUInt64(ulong[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new UInt64Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private UInt64Array BuildUInt64(ulong?[] values)
        => BuildWithBuilder(values, static () => new UInt64Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private FloatArray BuildFloat(float[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new FloatArray.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private FloatArray BuildFloat(float?[] values)
        => BuildWithBuilder(values, static () => new FloatArray.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private DoubleArray BuildDouble(double[] values)
        => _options.UseLowLevelFixedWidth
            ? FixedWidthArrowArrayFactory.Build(values, s_allocator)
            : BuildWithBuilder(values, static () => new DoubleArray.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator), appendAll: static (builder, source) => builder.AppendRange(source));

    private DoubleArray BuildDouble(double?[] values)
        => BuildWithBuilder(values, static () => new DoubleArray.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

#if NET8_0_OR_GREATER
    private Date32Array BuildDate32(DateOnly[] values)
        => BuildWithBuilder(
            values,
            static () => new Date32Array.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) => builder.Append(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            static builder => builder.Build(s_allocator));

    private Date32Array BuildDate32(DateOnly?[] values)
        => BuildWithBuilder(
            values,
            static () => new Date32Array.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) =>
            {
                if (value.HasValue)
                {
                    builder.Append(value.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                }
                else
                {
                    builder.AppendNull();
                }
            },
            static builder => builder.Build(s_allocator));

    private Date64Array BuildDate64(DateOnly[] values)
        => BuildWithBuilder(
            values,
            static () => new Date64Array.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) => builder.Append(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            static builder => builder.Build(s_allocator));

    private Date64Array BuildDate64(DateOnly?[] values)
        => BuildWithBuilder(
            values,
            static () => new Date64Array.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) =>
            {
                if (value.HasValue)
                {
                    builder.Append(value.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                }
                else
                {
                    builder.AppendNull();
                }
            },
            static builder => builder.Build(s_allocator));
#endif

    private Date32Array BuildDate32(DateTime[] values)
        => BuildWithBuilder(
            values,
            static () => new Date32Array.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) => builder.Append(value.Date),
            static builder => builder.Build(s_allocator));

    private Date32Array BuildDate32(DateTime?[] values)
        => BuildWithBuilder(
            values,
            static () => new Date32Array.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) =>
            {
                if (value.HasValue)
                {
                    builder.Append(value.Value.Date);
                }
                else
                {
                    builder.AppendNull();
                }
            },
            static builder => builder.Build(s_allocator));

    private Date64Array BuildDate64(DateTime[] values)
        => BuildWithBuilder(
            values,
            static () => new Date64Array.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) => builder.Append(value.Date),
            static builder => builder.Build(s_allocator));

    private Date64Array BuildDate64(DateTime?[] values)
        => BuildWithBuilder(
            values,
            static () => new Date64Array.Builder(),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) =>
            {
                if (value.HasValue)
                {
                    builder.Append(value.Value.Date);
                }
                else
                {
                    builder.AppendNull();
                }
            },
            static builder => builder.Build(s_allocator));

    private TimestampArray BuildTimestamp(DateTime[] values, TimestampType timestampType)
        => BuildWithBuilder(
            values,
            () => new TimestampArray.Builder(timestampType),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) => builder.Append(new DateTimeOffset(value)),
            static builder => builder.Build(s_allocator));

    private TimestampArray BuildTimestamp(DateTime?[] values, TimestampType timestampType)
        => BuildWithBuilder(
            values,
            () => new TimestampArray.Builder(timestampType),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) =>
            {
                if (value.HasValue)
                {
                    builder.Append(new DateTimeOffset(value.Value));
                }
                else
                {
                    builder.AppendNull();
                }
            },
            static builder => builder.Build(s_allocator));

    private static BinaryArray BuildGuid(Guid[] values)
    {
        var builder = new BinaryArray.Builder(new FixedSizeBinaryType(16));
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            builder.Append(new ReadOnlySpan<byte>(value.ToByteArray()));
        }

        return builder.Build(s_allocator);
    }

    private static BinaryArray BuildBinary(byte[][] values)
    {
        var builder = new BinaryArray.Builder();
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            builder.Append(new ReadOnlySpan<byte>(value));
        }

        return builder.Build(s_allocator);
    }

    private static StringArray BuildString(string?[] values)
    {
        if (values.Length == 0)
        {
            return new StringArray.Builder().Build(s_allocator);
        }

        if (System.Array.IndexOf(values, null) < 0)
        {
            return StringArrowArrayFactory.Build(values);
        }

        var builder = new StringArray.Builder();
        builder.Reserve(values.Length);

        foreach (var value in values)
        {
            if (value is null)
            {
                builder.AppendNull();
            }
            else
            {
                builder.Append(value, Encoding.UTF8);
            }
        }

        return builder.Build(s_allocator);
    }

    private static Decimal128Array BuildDecimal(decimal[] values, Decimal128Type type)
    {
        var builder = new Decimal128Array.Builder(type);
        builder.Reserve(values.Length);

        for (var i = 0; i < values.Length; i++)
        {
            builder.Append(new SqlDecimal(values[i]));
        }

        return builder.Build(s_allocator);
    }

    private static Decimal128Array BuildDecimal(decimal?[] values, Decimal128Type type)
    {
        return BuildWithBuilder(
            values,
            () => new Decimal128Array.Builder(type),
            static (builder, length) => builder.Reserve(length),
            static (builder, value) =>
            {
                if (value.HasValue)
                {
                    builder.Append(new SqlDecimal(value.Value));
                }
                else
                {
                    builder.AppendNull();
                }
            },
            static builder => builder.Build(s_allocator));
    }

    private static TArray BuildWithBuilder<T, TBuilder, TArray>(
        T[] values,
        Func<TBuilder> createBuilder,
        Action<TBuilder, int> reserve,
        Action<TBuilder, T> append,
        Func<TBuilder, TArray> build,
        Action<TBuilder, T[]>? appendAll = null)
    {
        var builder = createBuilder();
        reserve(builder, values.Length);

        if (appendAll is not null)
        {
            appendAll(builder, values);
            return build(builder);
        }

        foreach (var value in values)
        {
            append(builder, value);
        }

        return build(builder);
    }

}
