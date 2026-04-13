using System.Data.SqlTypes;
using System.Text;
using Apache.Arrow;
using Apache.Arrow.Arrays;
using Apache.Arrow.Types;

namespace ParquetRsForDotnet.Internal;

internal sealed class ArrowArrayClrMaterializer
{
    private static readonly Encoding s_utf8 = Encoding.UTF8;

    public System.Array Materialize(IArrowArray array, Field field)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(field);

        return field.DataType.TypeId switch
        {
            ArrowTypeId.Boolean => MaterializeBoolean((BooleanArray)array, field.IsNullable),
            ArrowTypeId.Int8 => MaterializeInt8((Int8Array)array, field.IsNullable),
            ArrowTypeId.UInt8 => MaterializeUInt8((UInt8Array)array, field.IsNullable),
            ArrowTypeId.Int16 => MaterializeInt16((Int16Array)array, field.IsNullable),
            ArrowTypeId.UInt16 => MaterializeUInt16((UInt16Array)array, field.IsNullable),
            ArrowTypeId.Int32 => MaterializeInt32((Int32Array)array, field.IsNullable),
            ArrowTypeId.UInt32 => MaterializeUInt32((UInt32Array)array, field.IsNullable),
            ArrowTypeId.Int64 => MaterializeInt64((Int64Array)array, field.IsNullable),
            ArrowTypeId.UInt64 => MaterializeUInt64((UInt64Array)array, field.IsNullable),
            ArrowTypeId.Float => MaterializeFloat((FloatArray)array, field.IsNullable),
            ArrowTypeId.Double => MaterializeDouble((DoubleArray)array, field.IsNullable),
            ArrowTypeId.String => MaterializeString((StringArray)array),
            ArrowTypeId.Binary => MaterializeBinary((BinaryArray)array),
            ArrowTypeId.FixedSizedBinary => MaterializeFixedSizeBinary((FixedSizeBinaryArray)array, (FixedSizeBinaryType)field.DataType, field.IsNullable),
            ArrowTypeId.Date32 => MaterializeDate32((Date32Array)array, field.IsNullable),
            ArrowTypeId.Date64 => MaterializeDate64((Date64Array)array, field.IsNullable),
            ArrowTypeId.Timestamp => MaterializeTimestamp((TimestampArray)array, field),
            ArrowTypeId.Decimal128 => MaterializeDecimal((Decimal128Array)array, field.IsNullable),
            _ => throw new NotSupportedException($"Arrow type '{field.DataType}' is not supported for CLR materialization."),
        };
    }

    public static Type GetExpectedClrType(Field field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return field.DataType switch
        {
            BooleanType => typeof(bool),
            Int8Type => typeof(sbyte),
            UInt8Type => typeof(byte),
            Int16Type => typeof(short),
            UInt16Type => typeof(ushort),
            Int32Type => typeof(int),
            UInt32Type => typeof(uint),
            Int64Type => typeof(long),
            UInt64Type => typeof(ulong),
            FloatType => typeof(float),
            DoubleType => typeof(double),
            StringType => typeof(string),
            BinaryType => typeof(byte[]),
            Decimal128Type => typeof(SqlDecimal),
            FixedSizeBinaryType fixedSizeBinaryType => fixedSizeBinaryType.ByteWidth == 16 ? typeof(Guid) : typeof(byte[]),
            Date32Type or Date64Type => typeof(DateOnly),
            TimestampType => typeof(DateTime),
            _ => throw new NotSupportedException($"Arrow type '{field.DataType}' is not supported for CLR materialization."),
        };
    }

    private static System.Array MaterializeBoolean(BooleanArray array, bool isNullable)
    {
        if (array.Offset == 0)
        {
            return isNullable ? MaterializeNullableBooleanFromBuffers(array) : MaterializeRequiredBooleanFromBuffers(array);
        }

        if (!isNullable)
        {
            var values = new bool[array.Length];
            if (array.NullCount == 0)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = array.GetValue(i)!.Value;
                }

                return values;
            }

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = array.GetValue(i) ?? throw CreateUnexpectedNullException(i);
            }

            return values;
        }

        var nullableValues = new bool?[array.Length];
        if (array.NullCount == 0)
        {
            for (var i = 0; i < nullableValues.Length; i++)
            {
                nullableValues[i] = array.GetValue(i)!.Value;
            }

            return nullableValues;
        }

        for (var i = 0; i < nullableValues.Length; i++)
        {
            nullableValues[i] = array.GetValue(i);
        }

        return nullableValues;
    }

    private static System.Array MaterializeInt8(Int8Array array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeUInt8(UInt8Array array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeInt16(Int16Array array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeUInt16(UInt16Array array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeInt32(Int32Array array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeUInt32(UInt32Array array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeInt64(Int64Array array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeUInt64(UInt64Array array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeFloat(FloatArray array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static System.Array MaterializeDouble(DoubleArray array, bool isNullable)
        => MaterializePrimitive(array, isNullable);

    private static string?[] MaterializeString(StringArray array)
    {
        if (array.Offset == 0)
        {
            return MaterializeStringFromBuffers(array);
        }

        if (!array.IsMaterialized(s_utf8))
        {
            array.Materialize(s_utf8);
        }

        var materialized = new string?[array.Length];
        for (var i = 0; i < materialized.Length; i++)
        {
            materialized[i] = array.IsValid(i) ? array.GetString(i, s_utf8) : null;
        }

        return materialized;
    }

    private static byte[]?[] MaterializeBinary(BinaryArray array)
    {
        if (array.Offset == 0)
        {
            return MaterializeBinaryFromBuffers(array);
        }

        var materialized = new byte[]?[array.Length];
        for (var i = 0; i < materialized.Length; i++)
        {
            materialized[i] = array.IsValid(i) ? array.GetBytes(i).ToArray() : null;
        }

        return materialized;
    }

    private static System.Array MaterializeFixedSizeBinary(FixedSizeBinaryArray array, FixedSizeBinaryType type, bool isNullable)
    {
        if (type.ByteWidth == 16)
        {
            return MaterializeGuid(array, isNullable);
        }

        return MaterializeBinary(array);
    }

    private static System.Array MaterializeGuid(FixedSizeBinaryArray array, bool isNullable)
    {
        if (array.Offset == 0)
        {
            return isNullable ? MaterializeNullableGuidFromBuffers(array) : MaterializeRequiredGuidFromBuffers(array);
        }

        if (!isNullable)
        {
            var values = new Guid[array.Length];
            if (array.NullCount == 0)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = new Guid(array.GetBytes(i));
                }

                return values;
            }

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = array.IsValid(i) ? new Guid(array.GetBytes(i)) : throw CreateUnexpectedNullException(i);
            }

            return values;
        }

        var nullableValues = new Guid?[array.Length];
        if (array.NullCount == 0)
        {
            for (var i = 0; i < nullableValues.Length; i++)
            {
                nullableValues[i] = new Guid(array.GetBytes(i));
            }

            return nullableValues;
        }

        for (var i = 0; i < nullableValues.Length; i++)
        {
            nullableValues[i] = array.IsValid(i) ? new Guid(array.GetBytes(i)) : null;
        }

        return nullableValues;
    }

    private static byte[]?[] MaterializeBinary(FixedSizeBinaryArray array)
    {
        if (array.Offset == 0)
        {
            return MaterializeFixedSizeBinaryFromBuffer(array);
        }

        var materialized = new byte[]?[array.Length];
        for (var i = 0; i < materialized.Length; i++)
        {
            materialized[i] = array.IsValid(i) ? array.GetBytes(i).ToArray() : null;
        }

        return materialized;
    }

    private static System.Array MaterializeDate32(Date32Array array, bool isNullable)
    {
        if (array.Offset == 0)
        {
            return isNullable ? MaterializeNullableDate32FromValues(array) : MaterializeRequiredDate32FromValues(array);
        }

        if (!isNullable)
        {
            var values = new DateOnly[array.Length];
            if (array.NullCount == 0)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = array.GetDateOnly(i) ?? throw CreateUnexpectedNullException(i);
                }

                return values;
            }

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = array.GetDateOnly(i) ?? throw CreateUnexpectedNullException(i);
            }

            return values;
        }

        var nullableValues = new DateOnly?[array.Length];
        if (array.NullCount == 0)
        {
            for (var i = 0; i < nullableValues.Length; i++)
            {
                nullableValues[i] = array.GetDateOnly(i) ?? throw CreateUnexpectedNullException(i);
            }

            return nullableValues;
        }

        for (var i = 0; i < nullableValues.Length; i++)
        {
            nullableValues[i] = array.GetDateOnly(i);
        }

        return nullableValues;
    }

    private static System.Array MaterializeDate64(Date64Array array, bool isNullable)
    {
        if (array.Offset == 0)
        {
            return isNullable ? MaterializeNullableDate64FromValues(array) : MaterializeRequiredDate64FromValues(array);
        }

        if (!isNullable)
        {
            var values = new DateOnly[array.Length];
            if (array.NullCount == 0)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = array.GetDateOnly(i) ?? throw CreateUnexpectedNullException(i);
                }

                return values;
            }

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = array.GetDateOnly(i) ?? throw CreateUnexpectedNullException(i);
            }

            return values;
        }

        var nullableValues = new DateOnly?[array.Length];
        if (array.NullCount == 0)
        {
            for (var i = 0; i < nullableValues.Length; i++)
            {
                nullableValues[i] = array.GetDateOnly(i) ?? throw CreateUnexpectedNullException(i);
            }

            return nullableValues;
        }

        for (var i = 0; i < nullableValues.Length; i++)
        {
            nullableValues[i] = array.GetDateOnly(i);
        }

        return nullableValues;
    }

    private static System.Array MaterializeTimestamp(TimestampArray array, Field field)
    {
        if (array.Offset == 0 && field.DataType is TimestampType timestampType && IsFastTimestampType(timestampType))
        {
            return field.IsNullable
                ? MaterializeNullableTimestampFromValues(array, timestampType)
                : MaterializeRequiredTimestampFromValues(array, timestampType);
        }

        if (!field.IsNullable)
        {
            var values = new DateTime[array.Length];
            if (array.NullCount == 0)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = ConvertTimestamp(array.GetTimestampUnchecked(i), field);
                }

                return values;
            }

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = ConvertTimestamp(array.GetTimestamp(i) ?? throw CreateUnexpectedNullException(i), field);
            }

            return values;
        }

        var nullableValues = new DateTime?[array.Length];
        if (array.NullCount == 0)
        {
            for (var i = 0; i < nullableValues.Length; i++)
            {
                nullableValues[i] = ConvertTimestamp(array.GetTimestampUnchecked(i), field);
            }

            return nullableValues;
        }

        for (var i = 0; i < nullableValues.Length; i++)
        {
            var value = array.GetTimestamp(i);
            nullableValues[i] = value.HasValue ? ConvertTimestamp(value.Value, field) : null;
        }

        return nullableValues;
    }

    private static System.Array MaterializeDecimal(Decimal128Array array, bool isNullable)
    {
        if (!isNullable)
        {
            var values = new SqlDecimal[array.Length];
            if (array.NullCount == 0)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = new SqlDecimal(array.GetValue(i) ?? throw CreateUnexpectedNullException(i));
                }

                return values;
            }

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = new SqlDecimal(array.GetValue(i) ?? throw CreateUnexpectedNullException(i));
            }

            return values;
        }

        var nullableValues = new SqlDecimal?[array.Length];
        if (array.NullCount == 0)
        {
            for (var i = 0; i < nullableValues.Length; i++)
            {
                nullableValues[i] = new SqlDecimal(array.GetValue(i) ?? throw CreateUnexpectedNullException(i));
            }

            return nullableValues;
        }

        for (var i = 0; i < nullableValues.Length; i++)
        {
            var value = array.GetValue(i);
            nullableValues[i] = value.HasValue ? new SqlDecimal(value.Value) : null;
        }

        return nullableValues;
    }

    private static T[] MaterializeRequired<T, TArray>(
        TArray array,
        Func<TArray, T[]> copyValues,
        Func<TArray, int, T?> getValue)
        where T : struct
        where TArray : IArrowArray
    {
        if (array.NullCount == 0)
        {
            return copyValues(array);
        }

        var values = new T[array.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = getValue(array, i) ?? throw CreateUnexpectedNullException(i);
        }

        return values;
    }

    private static T?[] MaterializeNullable<T, TArray>(
        TArray array,
        Func<TArray, T[]> copyValues,
        Func<TArray, int, T?> getValue)
        where T : struct
        where TArray : IArrowArray
    {
        if (array.NullCount == 0)
        {
            return MaterializeNullableNoNulls(copyValues(array));
        }

        var values = new T?[array.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = getValue(array, i);
        }

        return values;
    }

    private static T?[] MaterializeNullableNoNulls<T>(T[] values)
        where T : struct
    {
        var result = new T?[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = values[i];
        }

        return result;
    }

    private static System.Array MaterializePrimitive<T>(PrimitiveArray<T> array, bool isNullable)
        where T : struct, IEquatable<T>
    {
        if (array.Offset == 0)
        {
            return isNullable ? MaterializeNullablePrimitiveFromValues(array) : MaterializeRequiredPrimitiveFromValues(array);
        }

        return isNullable
            ? MaterializeNullable<T, PrimitiveArray<T>>(array, static value => value.Values.ToArray(), static (value, index) => value.GetValue(index))
            : MaterializeRequired<T, PrimitiveArray<T>>(array, static value => value.Values.ToArray(), static (value, index) => value.GetValue(index));
    }

    private static DateTime ConvertTimestamp(DateTimeOffset value, Field field)
    {
        return field.DataType is TimestampType timestampType && string.Equals(timestampType.Timezone, "UTC", StringComparison.OrdinalIgnoreCase)
            ? value.UtcDateTime
            : value.DateTime;
    }

    private static InvalidOperationException CreateUnexpectedNullException(int index)
    {
        return new InvalidOperationException($"Arrow array contained an unexpected null at index {index} for a required parquet column.");
    }

    private static bool[] MaterializeRequiredBooleanFromBuffers(BooleanArray array)
    {
        var values = new bool[array.Length];
        var bits = array.Values;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = GetBit(bits, i);
        }

        return values;
    }

    private static T[] MaterializeRequiredPrimitiveFromValues<T>(PrimitiveArray<T> array)
        where T : struct, IEquatable<T>
    {
        var values = new T[array.Length];
        array.Values.CopyTo(values);
        return values;
    }

    private static T?[] MaterializeNullablePrimitiveFromValues<T>(PrimitiveArray<T> array)
        where T : struct, IEquatable<T>
    {
        var values = new T?[array.Length];
        var rawValues = array.Values;

        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = rawValues[i];
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i) ? rawValues[i] : null;
        }

        return values;
    }

    private static bool?[] MaterializeNullableBooleanFromBuffers(BooleanArray array)
    {
        var values = new bool?[array.Length];
        var bits = array.Values;
        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = GetBit(bits, i);
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i) ? GetBit(bits, i) : null;
        }

        return values;
    }

    private static string?[] MaterializeStringFromBuffers(StringArray array)
    {
        var values = new string?[array.Length];
        var offsets = array.ValueOffsets;
        var bytes = array.Values;

        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = DecodeString(bytes, offsets[i], offsets[i + 1]);
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i)
                ? DecodeString(bytes, offsets[i], offsets[i + 1])
                : null;
        }

        return values;
    }

    private static byte[]?[] MaterializeBinaryFromBuffers(BinaryArray array)
    {
        var values = new byte[]?[array.Length];
        var offsets = array.ValueOffsets;
        var bytes = array.Values;

        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = CopySlice(bytes, offsets[i], offsets[i + 1]);
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i)
                ? CopySlice(bytes, offsets[i], offsets[i + 1])
                : null;
        }

        return values;
    }

    private static Guid[] MaterializeRequiredGuidFromBuffers(FixedSizeBinaryArray array)
    {
        var values = new Guid[array.Length];
        var buffer = array.ValueBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = new Guid(buffer.Slice(i * 16, 16));
        }

        return values;
    }

    private static Guid?[] MaterializeNullableGuidFromBuffers(FixedSizeBinaryArray array)
    {
        var values = new Guid?[array.Length];
        var buffer = array.ValueBuffer.Span;
        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = new Guid(buffer.Slice(i * 16, 16));
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i)
                ? new Guid(buffer.Slice(i * 16, 16))
                : null;
        }

        return values;
    }

    private static byte[]?[] MaterializeFixedSizeBinaryFromBuffer(FixedSizeBinaryArray array)
    {
        var values = new byte[]?[array.Length];
        var width = array.ValueBuffer.Length / array.Length;
        var buffer = array.ValueBuffer.Span;

        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = buffer.Slice(i * width, width).ToArray();
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i)
                ? buffer.Slice(i * width, width).ToArray()
                : null;
        }

        return values;
    }

    private static DateOnly[] MaterializeRequiredDate32FromValues(Date32Array array)
    {
        var values = new DateOnly[array.Length];
        var raw = array.Values;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ConvertDate32(raw[i]);
        }

        return values;
    }

    private static DateOnly?[] MaterializeNullableDate32FromValues(Date32Array array)
    {
        var values = new DateOnly?[array.Length];
        var raw = array.Values;
        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = ConvertDate32(raw[i]);
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i)
                ? ConvertDate32(raw[i])
                : null;
        }

        return values;
    }

    private static DateOnly[] MaterializeRequiredDate64FromValues(Date64Array array)
    {
        var values = new DateOnly[array.Length];
        var raw = array.Values;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ConvertDate64(raw[i]);
        }

        return values;
    }

    private static DateOnly?[] MaterializeNullableDate64FromValues(Date64Array array)
    {
        var values = new DateOnly?[array.Length];
        var raw = array.Values;
        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = ConvertDate64(raw[i]);
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i)
                ? ConvertDate64(raw[i])
                : null;
        }

        return values;
    }

    private static DateTime[] MaterializeRequiredTimestampFromValues(TimestampArray array, TimestampType timestampType)
    {
        var values = new DateTime[array.Length];
        var raw = array.Values;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ConvertTimestamp(raw[i], timestampType);
        }

        return values;
    }

    private static DateTime?[] MaterializeNullableTimestampFromValues(TimestampArray array, TimestampType timestampType)
    {
        var values = new DateTime?[array.Length];
        var raw = array.Values;
        if (array.NullCount == 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = ConvertTimestamp(raw[i], timestampType);
            }

            return values;
        }

        var validity = array.NullBitmapBuffer.Span;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = IsValid(validity, i)
                ? ConvertTimestamp(raw[i], timestampType)
                : null;
        }

        return values;
    }

    private static string DecodeString(ReadOnlySpan<byte> bytes, int start, int end)
    {
        return s_utf8.GetString(bytes.Slice(start, end - start));
    }

    private static byte[] CopySlice(ReadOnlySpan<byte> bytes, int start, int end)
    {
        return bytes.Slice(start, end - start).ToArray();
    }

    private static DateOnly ConvertDate64(long millisecondsSinceEpoch)
    {
        return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(millisecondsSinceEpoch).UtcDateTime);
    }

    private static DateOnly ConvertDate32(int daysSinceEpoch)
    {
        return DateOnly.FromDateTime(DateTime.UnixEpoch.Date).AddDays(daysSinceEpoch);
    }

    private static DateTime ConvertTimestamp(long value, TimestampType timestampType)
    {
        var ticks = timestampType.Unit switch
        {
            TimeUnit.Second => checked(value * TimeSpan.TicksPerSecond),
            TimeUnit.Millisecond => checked(value * TimeSpan.TicksPerMillisecond),
            TimeUnit.Microsecond => checked(value * 10),
            TimeUnit.Nanosecond => value / 100,
            _ => throw new NotSupportedException($"Unsupported timestamp unit '{timestampType.Unit}'."),
        };

        return new DateTime(DateTime.UnixEpoch.Ticks + ticks, DateTimeKind.Utc);
    }

    private static bool IsFastTimestampType(TimestampType timestampType)
    {
        return string.Equals(timestampType.Timezone, "UTC", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValid(ReadOnlySpan<byte> bitmap, int index)
    {
        return (bitmap[index >> 3] & (1 << (index & 7))) != 0;
    }

    private static bool GetBit(ReadOnlySpan<byte> bitmap, int index)
    {
        return (bitmap[index >> 3] & (1 << (index & 7))) != 0;
    }
}
