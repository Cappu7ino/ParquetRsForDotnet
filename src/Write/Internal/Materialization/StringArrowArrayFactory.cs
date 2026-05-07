using System.Text;
using Apache.Arrow;
using Apache.Arrow.Memory;
using Apache.Arrow.Types;

namespace ParquetRsForDotnet.Internal.Materialization;

internal static class StringArrowArrayFactory
{
    private static readonly MemoryAllocator s_allocator = MemoryAllocator.Default.Value;
    private static readonly Encoding s_utf8 = Encoding.UTF8;

    public static StringArray Build(string?[] values)
    {
        TargetFrameworkCompat.ThrowIfNull(values);

        var hasNulls = false;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is null)
            {
                hasNulls = true;
                break;
            }
        }

        return BuildCore(values, hasNulls);
    }

    private static StringArray BuildCore(IReadOnlyList<string?> values, bool hasNulls)
    {
        var totalByteCount = 0;
        var nullCount = 0;

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value is null)
            {
                nullCount++;
                continue;
            }

            totalByteCount += s_utf8.GetByteCount(value);
        }

        var offsets = new int[values.Count + 1];
        var data = new byte[totalByteCount];
        byte[]? validity = hasNulls ? new byte[(values.Count + 7) / 8] : null;

        var currentOffset = 0;
        for (var i = 0; i < values.Count; i++)
        {
            offsets[i] = currentOffset;

            var value = values[i];
            if (value is null)
            {
                continue;
            }

            if (validity is not null)
            {
                validity[i >> 3] |= (byte)(1 << (i & 7));
            }

            currentOffset += s_utf8.GetBytes(value, 0, value.Length, data, currentOffset);
        }

        offsets[offsets.Length - 1] = currentOffset;

        var offsetBytes = new byte[offsets.Length * sizeof(int)];
        Buffer.BlockCopy(offsets, 0, offsetBytes, 0, offsetBytes.Length);

        var nullBuffer = ArrowBuffer.Empty;
        if (validity is not null)
        {
            var validityBufferBuilder = new ArrowBuffer.Builder<byte>();
            validityBufferBuilder.Reserve(validity.Length);
            validityBufferBuilder.Append(validity);
            nullBuffer = validityBufferBuilder.Build(s_allocator);
        }

        var offsetBufferBuilder = new ArrowBuffer.Builder<byte>();
        offsetBufferBuilder.Reserve(offsetBytes.Length);
        offsetBufferBuilder.Append(offsetBytes);

        var valueBuffer = ArrowBuffer.Empty;
        if (data.Length != 0)
        {
            var valueBufferBuilder = new ArrowBuffer.Builder<byte>();
            valueBufferBuilder.Reserve(data.Length);
            valueBufferBuilder.Append(data);
            valueBuffer = valueBufferBuilder.Build(s_allocator);
        }

        var arrayData = new ArrayData(
            StringType.Default,
            values.Count,
            nullCount,
            0,
            [
                nullBuffer,
                offsetBufferBuilder.Build(s_allocator),
                valueBuffer,
            ]);

        return new StringArray(arrayData);
    }
}
