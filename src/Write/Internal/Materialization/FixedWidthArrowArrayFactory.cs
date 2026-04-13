using System.Runtime.InteropServices;
using Apache.Arrow;
using Apache.Arrow.Memory;
using Apache.Arrow.Types;

namespace ParquetRsForDotnet.Internal.Materialization;

internal static class FixedWidthArrowArrayFactory
{
    private static readonly int s_unixEpochDayNumber = DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;

    public static Int8Array Build(sbyte[] values, MemoryAllocator allocator)
        => Build(values, Int8Type.Default, allocator, static data => new Int8Array(data));

    public static UInt8Array Build(byte[] values, MemoryAllocator allocator)
        => Build(values, UInt8Type.Default, allocator, static data => new UInt8Array(data));

    public static Int16Array Build(short[] values, MemoryAllocator allocator)
        => Build(values, Int16Type.Default, allocator, static data => new Int16Array(data));

    public static UInt16Array Build(ushort[] values, MemoryAllocator allocator)
        => Build(values, UInt16Type.Default, allocator, static data => new UInt16Array(data));

    public static Int32Array Build(int[] values, MemoryAllocator allocator)
        => Build(values, Int32Type.Default, allocator, static data => new Int32Array(data));

    public static UInt32Array Build(uint[] values, MemoryAllocator allocator)
        => Build(values, UInt32Type.Default, allocator, static data => new UInt32Array(data));

    public static Int64Array Build(long[] values, MemoryAllocator allocator)
        => Build(values, Int64Type.Default, allocator, static data => new Int64Array(data));

    public static UInt64Array Build(ulong[] values, MemoryAllocator allocator)
        => Build(values, UInt64Type.Default, allocator, static data => new UInt64Array(data));

    public static FloatArray Build(float[] values, MemoryAllocator allocator)
        => Build(values, FloatType.Default, allocator, static data => new FloatArray(data));

    public static DoubleArray Build(double[] values, MemoryAllocator allocator)
        => Build(values, DoubleType.Default, allocator, static data => new DoubleArray(data));

    public static Date32Array Build(DateOnly[] values, MemoryAllocator allocator)
    {
        var normalized = new int[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            normalized[i] = values[i].DayNumber - s_unixEpochDayNumber;
        }

        return Build(normalized, Date32Type.Default, allocator, static data => new Date32Array(data));
    }

    public static TimestampArray Build(DateTime[] values, TimestampType timestampType, MemoryAllocator allocator)
    {
        var normalized = new long[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            normalized[i] = NormalizeTimestamp(values[i], timestampType.Unit);
        }

        return Build(normalized, timestampType, allocator, static data => new TimestampArray(data));
    }

    private static TArray Build<T, TArray>(T[] values, IArrowType arrowType, MemoryAllocator allocator, Func<ArrayData, TArray> factory)
        where T : unmanaged
        where TArray : IArrowArray
    {
        var valueBytes = MemoryMarshal.AsBytes(values.AsSpan());
        var valueBufferBuilder = new ArrowBuffer.Builder<byte>();
        valueBufferBuilder.Reserve(valueBytes.Length);
        valueBufferBuilder.Append(valueBytes);

        var arrayData = new ArrayData(
            arrowType,
            values.Length,
            0,
            0,
            [ArrowBuffer.Empty, valueBufferBuilder.Build(allocator)]);

        return factory(arrayData);
    }

    private static long NormalizeTimestamp(DateTime value, TimeUnit unit)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        var epochTicks = utcValue.Ticks - DateTime.UnixEpoch.Ticks;

        return unit switch
        {
            TimeUnit.Second => epochTicks / TimeSpan.TicksPerSecond,
            TimeUnit.Millisecond => epochTicks / TimeSpan.TicksPerMillisecond,
            TimeUnit.Microsecond => epochTicks / 10,
            TimeUnit.Nanosecond => checked(epochTicks * 100),
            _ => throw new NotSupportedException($"Unsupported timestamp unit '{unit}'."),
        };
    }
}
