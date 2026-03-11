using Apache.Arrow;
using ParquetRsForDotnet.Internal.Materialization;
using ParquetRsForDotnet.Internal.SchemaMapping;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Builds validated Arrow record batches from the public schema API and column-oriented inputs.
/// </summary>
internal sealed class ArrowRecordBatchBuilder
{
    private readonly ParquetSchema schema;
    private readonly Schema arrowSchema;
    private readonly ClrArrayMaterializer clrArrayMaterializer;

    public ArrowRecordBatchBuilder(ParquetSchema schema, ParquetWriteOptions options)
    {
        this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
        ArgumentNullException.ThrowIfNull(options);

        arrowSchema = PublicSchemaMapper.Map(schema);
        clrArrayMaterializer = new ClrArrayMaterializer(CreateArrowMaterializationOptions(options));
    }

    public Schema ArrowSchema => arrowSchema;

    public RecordBatch Build(IReadOnlyList<System.Array> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ValidateColumnCount(columns.Count);

        var rowCount = ResolveRowCount(columns.Select(static column => column?.Length ?? -1).ToArray());
        var arrays = new IArrowArray[columns.Count];

        for (var i = 0; i < columns.Count; i++)
        {
            var column = schema.Columns[i];
            var data = columns[i] ?? throw new ArgumentNullException(nameof(columns), $"Column '{column.Name}' is null.");

            ValidateManagedArrayShape(column, data, i, rowCount);

            var context = new ColumnMaterializationContext(
                column.Name,
                ResolveManagedArrayElementType(data),
                arrowSchema.GetFieldByIndex(i).DataType,
                rowCount,
                column.IsNullable);

            arrays[i] = clrArrayMaterializer.Materialize(data, context);
        }

        return new RecordBatch(arrowSchema, arrays, rowCount);
    }

    public RecordBatch Build(IReadOnlyList<IArrowArray> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ValidateColumnCount(columns.Count);

        var rowCount = ResolveRowCount(columns.Select(static column => column?.Length ?? -1).ToArray());
        var arrays = new IArrowArray[columns.Count];

        for (var i = 0; i < columns.Count; i++)
        {
            var column = schema.Columns[i];
            var data = columns[i] ?? throw new ArgumentNullException(nameof(columns), $"Column '{column.Name}' is null.");
            var expectedType = arrowSchema.GetFieldByIndex(i).DataType;

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

        return new RecordBatch(arrowSchema, arrays, rowCount);
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

    private static int ResolveRowCount(IReadOnlyList<int> lengths)
    {
        if (lengths.Count == 0)
        {
            throw new NativeParquetException(NativeErrorCode.InvalidArgument, "At least one column is required to build a record batch.");
        }

        var rowCount = lengths[0];
        for (var i = 1; i < lengths.Count; i++)
        {
            if (lengths[i] != rowCount)
            {
                throw new NativeParquetException(NativeErrorCode.SchemaMismatch, $"Column length {lengths[i]} does not match expected batch length {rowCount}.");
            }
        }

        return rowCount;
    }

    private void ValidateColumnCount(int columnCount)
    {
        if (columnCount != schema.Columns.Count)
        {
            throw new NativeParquetException(NativeErrorCode.SchemaMismatch, $"Column count {columnCount} does not match schema column count {schema.Columns.Count}.");
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
