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
            bool?[] values => BuildNullable(values, new BooleanArray.Builder()),
            byte[] values when context.ArrowType is UInt8Type => BuildUInt8(values),
            byte?[] values => BuildNullable(values, new UInt8Array.Builder()),
            sbyte[] values => BuildInt8(values),
            sbyte?[] values => BuildNullable(values, new Int8Array.Builder()),
            short[] values => BuildInt16(values),
            short?[] values => BuildNullable(values, new Int16Array.Builder()),
            ushort[] values => BuildUInt16(values),
            ushort?[] values => BuildNullable(values, new UInt16Array.Builder()),
            int[] values => BuildInt32(values),
            int?[] values => BuildNullable(values, new Int32Array.Builder()),
            uint[] values => BuildUInt32(values),
            uint?[] values => BuildNullable(values, new UInt32Array.Builder()),
            long[] values => BuildInt64(values),
            long?[] values => BuildNullable(values, new Int64Array.Builder()),
            ulong[] values => BuildUInt64(values),
            ulong?[] values => BuildNullable(values, new UInt64Array.Builder()),
            float[] values => BuildFloat(values),
            float?[] values => BuildNullable(values, new FloatArray.Builder()),
            double[] values => BuildDouble(values),
            double?[] values => BuildNullable(values, new DoubleArray.Builder()),
            string?[] values => BuildString(values),
            Guid[] values => BuildGuid(values),
            byte[][] values => BuildBinary(values),
            DateOnly[] values when context.ArrowType is Date32Type => BuildDate32(values),
            DateOnly[] values when context.ArrowType is Date64Type => BuildDate64(values),
            DateOnly?[] values when context.ArrowType is Date32Type => BuildNullableDate32(values),
            DateOnly?[] values when context.ArrowType is Date64Type => BuildNullableDate64(values),
            DateTime[] values => BuildTimestamp(values, (TimestampType)context.ArrowType),
            DateTime?[] values => BuildNullableTimestamp(values, (TimestampType)context.ArrowType),
            decimal[] values => BuildDecimal(values, (Decimal128Type)context.ArrowType),
            decimal?[] values => BuildNullableDecimal(values, (Decimal128Type)context.ArrowType),
            _ => throw new NotSupportedException($"Array-backed type '{data.GetType()}' is not supported for column '{context.ColumnName}'."),
        };
    }

    private static BooleanArray BuildBoolean(bool[] values)
    {
        var builder = new BooleanArray.Builder();
        builder.Reserve(values.Length);
        builder.AppendRange(values);
        return builder.Build(s_allocator);
    }

    private Int8Array BuildInt8(sbyte[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new Int8Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private UInt8Array BuildUInt8(byte[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new UInt8Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private Int16Array BuildInt16(short[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new Int16Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private UInt16Array BuildUInt16(ushort[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new UInt16Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private Int32Array BuildInt32(int[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new Int32Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private UInt32Array BuildUInt32(uint[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new UInt32Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private Int64Array BuildInt64(long[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new Int64Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private UInt64Array BuildUInt64(ulong[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new UInt64Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private FloatArray BuildFloat(float[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new FloatArray.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private DoubleArray BuildDouble(double[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new DoubleArray.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private Date32Array BuildDate32(DateOnly[] values)
        => _options.UseLowLevelFixedWidth ? FixedWidthArrowArrayFactory.Build(values, s_allocator) : BuildWithBuilder(values, static () => new Date32Array.Builder(), static (builder, length) => builder.Reserve(length), static (builder, value) => builder.Append(value), static builder => builder.Build(s_allocator));

    private Date64Array BuildDate64(DateOnly[] values)
    {
        if (_options.UseLowLevelFixedWidth)
        {
            return FixedWidthArrowArrayFactory.BuildDate64(values, s_allocator);
        }

        var builder = new Date64Array.Builder();
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            builder.Append(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        return builder.Build(s_allocator);
    }

    private TimestampArray BuildTimestamp(DateTime[] values, TimestampType timestampType)
    {
        if (_options.UseLowLevelFixedWidth)
        {
            return FixedWidthArrowArrayFactory.Build(values, timestampType, s_allocator);
        }

        var builder = new TimestampArray.Builder(timestampType);
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            builder.Append(new DateTimeOffset(value));
        }

        return builder.Build(s_allocator);
    }

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
        var builder = new StringArray.Builder();
        builder.Reserve(values.Length);

        if (System.Array.IndexOf(values, null) < 0)
        {
            builder.AppendRange(values!, Encoding.UTF8);
            return builder.Build(s_allocator);
        }

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
        builder.AppendRange(values);
        return builder.Build(s_allocator);
    }

    private static IArrowArray BuildNullable<T>(T?[] values, dynamic builder)
        where T : struct
    {
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            builder.Append(value);
        }

        return builder.Build(s_allocator);
    }

    private static TimestampArray BuildNullableTimestamp(DateTime?[] values, TimestampType timestampType)
    {
        var builder = new TimestampArray.Builder(timestampType);
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            if (value.HasValue)
            {
                builder.Append(new DateTimeOffset(value.Value));
            }
            else
            {
                builder.AppendNull();
            }
        }

        return builder.Build(s_allocator);
    }

    private static Decimal128Array BuildNullableDecimal(decimal?[] values, Decimal128Type type)
    {
        var builder = new Decimal128Array.Builder(type);
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            if (value.HasValue)
            {
                builder.Append(value.Value);
            }
            else
            {
                builder.AppendNull();
            }
        }

        return builder.Build(s_allocator);
    }

    private static Date32Array BuildNullableDate32(DateOnly?[] values)
    {
        var builder = new Date32Array.Builder();
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            if (value.HasValue)
            {
                builder.Append(value.Value);
            }
            else
            {
                builder.AppendNull();
            }
        }

        return builder.Build(s_allocator);
    }

    private static Date64Array BuildNullableDate64(DateOnly?[] values)
    {
        var builder = new Date64Array.Builder();
        builder.Reserve(values.Length);
        foreach (var value in values)
        {
            if (value.HasValue)
            {
                builder.Append(value.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            }
            else
            {
                builder.AppendNull();
            }
        }

        return builder.Build(s_allocator);
    }

    private static TArray BuildWithBuilder<T, TBuilder, TArray>(
        T[] values,
        Func<TBuilder> createBuilder,
        Action<TBuilder, int> reserve,
        Action<TBuilder, T> append,
        Func<TBuilder, TArray> build)
    {
        var builder = createBuilder();
        reserve(builder, values.Length);
        foreach (var value in values)
        {
            append(builder, value);
        }

        return build(builder);
    }
}
