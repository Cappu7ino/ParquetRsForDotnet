using Apache.Arrow;
using ParquetRsForDotnet.Internal.SchemaMapping;
using ArrowSchema = Apache.Arrow.Schema;

namespace ParquetRsForDotnet.Read.Internal.Schema;

internal static class ParquetSchemaImporter
{
    public static ParquetSchema Import(ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new ParquetSchema(schema.FieldsList.Select(ImportColumn));
    }

    private static ParquetColumn ImportColumn(Field field)
    {
        foreach (ParquetColumnType columnType in Enum.GetValues<ParquetColumnType>())
        {
            if (columnType is ParquetColumnType.Timestamp or ParquetColumnType.Decimal128)
            {
                continue;
            }

            var candidate = new ParquetColumn(field.Name, columnType, field.IsNullable);
            if (PublicSchemaMapper.AreEquivalent(PublicSchemaMapper.MapType(candidate), field.DataType))
            {
                return candidate;
            }
        }

        if (field.DataType is Apache.Arrow.Types.TimestampType timestampType)
        {
            return new ParquetColumn(
                field.Name,
                new ParquetTimestampSettings(ImportTimestampUnit(timestampType.Unit), timestampType.Timezone),
                field.IsNullable);
        }

        if (field.DataType is Apache.Arrow.Types.Decimal128Type decimalType)
        {
            return new ParquetColumn(
                field.Name,
                new ParquetDecimalSettings(decimalType.Precision, decimalType.Scale),
                field.IsNullable);
        }

        throw new NotSupportedException($"Arrow type '{field.DataType}' is not supported by the public parquet schema importer.");
    }

    private static ParquetTimestampUnit ImportTimestampUnit(Apache.Arrow.Types.TimeUnit unit)
    {
        return unit switch
        {
            Apache.Arrow.Types.TimeUnit.Second => ParquetTimestampUnit.Second,
            Apache.Arrow.Types.TimeUnit.Millisecond => ParquetTimestampUnit.Millisecond,
            Apache.Arrow.Types.TimeUnit.Microsecond => ParquetTimestampUnit.Microsecond,
            Apache.Arrow.Types.TimeUnit.Nanosecond => ParquetTimestampUnit.Nanosecond,
            _ => throw new NotSupportedException($"Timestamp unit '{unit}' is not supported."),
        };
    }
}
