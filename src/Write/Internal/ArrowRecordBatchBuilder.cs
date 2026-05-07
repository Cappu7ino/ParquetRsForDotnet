using Apache.Arrow;
using Apache.Arrow.Types;
using ParquetRsForDotnet.Internal.Materialization;
using ParquetRsForDotnet.Internal.SchemaMapping;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Builds validated Arrow record batches from the public schema API and column-oriented inputs.
/// </summary>
internal sealed class ArrowRecordBatchBuilder
{
    private readonly ParquetSchema _schema;
    private readonly Schema _arrowSchema;
    private readonly IArrowType[] _arrowTypes;
    private readonly ClrArrayMaterializer _clrArrayMaterializer;

    public ArrowRecordBatchBuilder(ParquetSchema schema, ParquetWriteOptions options)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        TargetFrameworkCompat.ThrowIfNull(options);

        _arrowSchema = PublicSchemaMapper.Map(schema);
        _arrowTypes = new IArrowType[schema.Columns.Count];
        for (var i = 0; i < _arrowTypes.Length; i++)
        {
            _arrowTypes[i] = _arrowSchema.GetFieldByIndex(i).DataType;
        }

        _clrArrayMaterializer = new ClrArrayMaterializer(CreateArrowMaterializationOptions(options));
    }

    public Schema ArrowSchema => _arrowSchema;

    public RecordBatch Build(IReadOnlyList<System.Array> columns)
    {
        TargetFrameworkCompat.ThrowIfNull(columns);
        ValidateColumnCount(columns.Count);

        var rowCount = ResolveRowCount(columns);
        var arrays = new IArrowArray[columns.Count];

        for (var i = 0; i < columns.Count; i++)
        {
            var column = _schema.Columns[i];
            var data = columns[i] ?? throw new ArgumentNullException(nameof(columns), $"Column '{column.Name}' is null.");

            ValidateManagedArrayShape(column, data, i, rowCount);

            var context = new ColumnMaterializationContext(
                column.Name,
                ResolveManagedArrayElementType(data),
                _arrowTypes[i],
                rowCount,
                column.IsNullable);

            arrays[i] = _clrArrayMaterializer.Materialize(data, context);
        }

        return new RecordBatch(_arrowSchema, arrays, rowCount);
    }

    public RecordBatch Build(IReadOnlyList<IArrowArray> columns)
    {
        TargetFrameworkCompat.ThrowIfNull(columns);
        ValidateColumnCount(columns.Count);

        var rowCount = ResolveRowCount(columns);
        var arrays = new IArrowArray[columns.Count];

        for (var i = 0; i < columns.Count; i++)
        {
            var column = _schema.Columns[i];
            var data = columns[i] ?? throw new ArgumentNullException(nameof(columns), $"Column '{column.Name}' is null.");
            var expectedType = _arrowTypes[i];

            if (!column.IsNullable && data.NullCount > 0)
            {
                throw new NativeParquetException(NativeErrorCode.SchemaMismatch, $"Column '{column.Name}' is not nullable but the supplied Arrow array contains null values.");
            }

            if (!PublicSchemaMapper.AreEquivalent(expectedType, data.Data.DataType))
            {
                throw new NativeParquetException(NativeErrorCode.SchemaMismatch, $"Column '{column.Name}' Arrow type '{data.Data.DataType}' does not match schema type '{expectedType}'.");
            }

            arrays[i] = data;
        }

        return new RecordBatch(_arrowSchema, arrays, rowCount);
    }

    private static ArrowMaterializationOptions CreateArrowMaterializationOptions(ParquetWriteOptions options)
    {
        return new ArrowMaterializationOptions
        {
            Mode = options.ArrowMaterializationMode switch
            {
                ArrowMaterializationMode.Default => ArrowMaterializationMode.BuilderOnly,
                _ => options.ArrowMaterializationMode,
            },
        };
    }

    private static int ResolveRowCount<TColumn>(IReadOnlyList<TColumn> columns)
        where TColumn : class
    {
        if (columns.Count == 0)
        {
            throw new NativeParquetException(NativeErrorCode.InvalidArgument, "At least one column is required to build a record batch.");
        }

        var first = columns[0] switch
        {
            System.Array array => array.Length,
            IArrowArray arrowArray => arrowArray.Length,
            null => -1,
            _ => throw new NativeParquetException(NativeErrorCode.InvalidArgument, "Unsupported column container type.")
        };

        var rowCount = first;
        for (var i = 1; i < columns.Count; i++)
        {
            var length = columns[i] switch
            {
                System.Array array => array.Length,
                IArrowArray arrowArray => arrowArray.Length,
                null => -1,
                _ => throw new NativeParquetException(NativeErrorCode.InvalidArgument, "Unsupported column container type.")
            };

            if (length != rowCount)
            {
                throw new NativeParquetException(NativeErrorCode.SchemaMismatch, $"Column length {length} does not match expected batch length {rowCount}.");
            }
        }

        return rowCount;
    }

    private void ValidateColumnCount(int columnCount)
    {
        if (columnCount != _schema.Columns.Count)
        {
            throw new NativeParquetException(NativeErrorCode.SchemaMismatch, $"Column count {columnCount} does not match schema column count {_schema.Columns.Count}.");
        }
    }

    private static Type ResolveManagedArrayElementType(System.Array data)
    {
        return data.GetType().GetElementType() ?? typeof(object);
    }

    private static void ValidateManagedArrayShape(ParquetColumn column, System.Array data, int ordinal, int rowCount)
    {
        if (data.Rank != 1)
        {
            throw new NativeParquetException(NativeErrorCode.InvalidArgument, $"Column '{column.Name}' at ordinal {ordinal} must be a one-dimensional CLR array.");
        }

        if (data.Length != rowCount)
        {
            throw new NativeParquetException(NativeErrorCode.SchemaMismatch, $"Column '{column.Name}' length {data.Length} does not match expected batch length {rowCount}.");
        }

        var actualElementType = ResolveManagedArrayElementType(data);
        var expectedElementType = PublicSchemaMapper.GetExpectedClrType(column);
        var nullableElementType = Nullable.GetUnderlyingType(actualElementType);

        if (actualElementType != expectedElementType && nullableElementType != expectedElementType)
        {
            throw new NativeParquetException(NativeErrorCode.SchemaMismatch, $"Column '{column.Name}' CLR element type '{actualElementType}' does not match schema type '{expectedElementType}'.");
        }
    }
}
