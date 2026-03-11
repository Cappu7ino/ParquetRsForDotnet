using Apache.Arrow;
using Apache.Arrow.Types;

namespace ParquetRsForDotnet.Internal.SchemaMapping;

/// <summary>
/// Maps the public schema API onto Arrow schema objects and batch-validation rules.
/// </summary>
internal static class PublicSchemaMapper
{
    public static Schema Map(ParquetSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return new Schema(schema.Columns.Select(MapField), metadata: []);
    }

    public static Field MapField(ParquetColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return new Field(column.Name, MapType(column), column.IsNullable);
    }

    public static IArrowType MapType(ParquetColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        return column.ColumnType switch
        {
            ParquetColumnType.Boolean => BooleanType.Default,
            ParquetColumnType.Int8 => Int8Type.Default,
            ParquetColumnType.UInt8 => UInt8Type.Default,
            ParquetColumnType.Int16 => Int16Type.Default,
            ParquetColumnType.UInt16 => UInt16Type.Default,
            ParquetColumnType.Int32 => Int32Type.Default,
            ParquetColumnType.UInt32 => UInt32Type.Default,
            ParquetColumnType.Int64 => Int64Type.Default,
            ParquetColumnType.UInt64 => UInt64Type.Default,
            ParquetColumnType.Float32 => FloatType.Default,
            ParquetColumnType.Float64 => DoubleType.Default,
            ParquetColumnType.String => StringType.Default,
            ParquetColumnType.Binary => BinaryType.Default,
            ParquetColumnType.Guid => new FixedSizeBinaryType(16),
            ParquetColumnType.Date32 => Date32Type.Default,
            ParquetColumnType.Date64 => Date64Type.Default,
            ParquetColumnType.Timestamp => CreateTimestampType(column),
            ParquetColumnType.Decimal128 => CreateDecimalType(column),
            _ => throw new NotSupportedException($"Unsupported public column type '{column.ColumnType}'."),
        };
    }

    public static Type GetExpectedClrType(ParquetColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        return column.ColumnType switch
        {
            ParquetColumnType.Boolean => typeof(bool),
            ParquetColumnType.Int8 => typeof(sbyte),
            ParquetColumnType.UInt8 => typeof(byte),
            ParquetColumnType.Int16 => typeof(short),
            ParquetColumnType.UInt16 => typeof(ushort),
            ParquetColumnType.Int32 => typeof(int),
            ParquetColumnType.UInt32 => typeof(uint),
            ParquetColumnType.Int64 => typeof(long),
            ParquetColumnType.UInt64 => typeof(ulong),
            ParquetColumnType.Float32 => typeof(float),
            ParquetColumnType.Float64 => typeof(double),
            ParquetColumnType.String => typeof(string),
            ParquetColumnType.Binary => typeof(byte[]),
            ParquetColumnType.Guid => typeof(Guid),
            ParquetColumnType.Date32 or ParquetColumnType.Date64 => typeof(DateOnly),
            ParquetColumnType.Timestamp => typeof(DateTime),
            ParquetColumnType.Decimal128 => typeof(decimal),
            _ => throw new NotSupportedException($"Unsupported public column type '{column.ColumnType}'."),
        };
    }

    public static bool AreEquivalent(IArrowType expected, IArrowType actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        return (expected, actual) switch
        {
            (BooleanType, BooleanType) => true,
            (Int8Type, Int8Type) => true,
            (UInt8Type, UInt8Type) => true,
            (Int16Type, Int16Type) => true,
            (UInt16Type, UInt16Type) => true,
            (Int32Type, Int32Type) => true,
            (UInt32Type, UInt32Type) => true,
            (Int64Type, Int64Type) => true,
            (UInt64Type, UInt64Type) => true,
            (FloatType, FloatType) => true,
            (DoubleType, DoubleType) => true,
            (StringType, StringType) => true,
            (BinaryType, BinaryType) => true,
            (Date32Type, Date32Type) => true,
            (Date64Type, Date64Type) => true,
            (Decimal128Type left, Decimal128Type right) => left.Precision == right.Precision && left.Scale == right.Scale,
            (FixedSizeBinaryType left, FixedSizeBinaryType right) => left.ByteWidth == right.ByteWidth,
            (TimestampType left, TimestampType right) => left.Unit == right.Unit && string.Equals(left.Timezone, right.Timezone, StringComparison.Ordinal),
            _ => false,
        };
    }

    private static TimestampType CreateTimestampType(ParquetColumn column)
    {
        var settings = column.TimestampSettings ?? throw new InvalidOperationException($"Column '{column.Name}' is missing timestamp settings.");
        return new TimestampType(MapTimeUnit(settings.Unit), settings.Timezone ?? string.Empty);
    }

    private static Decimal128Type CreateDecimalType(ParquetColumn column)
    {
        var settings = column.DecimalSettings ?? throw new InvalidOperationException($"Column '{column.Name}' is missing decimal settings.");
        return new Decimal128Type(settings.Precision, settings.Scale);
    }

    private static TimeUnit MapTimeUnit(ParquetTimestampUnit unit)
    {
        return unit switch
        {
            ParquetTimestampUnit.Second => TimeUnit.Second,
            ParquetTimestampUnit.Millisecond => TimeUnit.Millisecond,
            ParquetTimestampUnit.Microsecond => TimeUnit.Microsecond,
            ParquetTimestampUnit.Nanosecond => TimeUnit.Nanosecond,
            _ => throw new NotSupportedException($"Unsupported timestamp unit '{unit}'."),
        };
    }
}
