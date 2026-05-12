using Apache.Arrow;
using ArrowSchema = Apache.Arrow.Schema;
using ParquetRsForDotnet.Internal;

namespace ParquetRsForDotnet;

/// <summary>
/// Reads parquet data through explicit row-group and Arrow-native column access.
/// </summary>
public sealed class ParquetFileReader : IDisposable
{
    private readonly ManagedParquetSource _source;
    private readonly ArrowSchema _arrowSchema;
    private readonly IntPtr _nativeReader;
    private bool _disposed;

    public ParquetFileReader(Stream input)
        : this(input, null)
    {
    }

    public ParquetFileReader(Stream input, ParquetReadOptions? options)
    {
        TargetFrameworkCompat.ThrowIfNull(input);
        options ??= new ParquetReadOptions();
        options.Validate();

        Options = options;
        _source = new ManagedParquetSource(input);
        _nativeReader = NativeParquetBridge.OpenFileReader(_source);
        _arrowSchema = NativeParquetBridge.GetFileReaderSchema(_nativeReader);
        Schema = global::ParquetRsForDotnet.Read.Internal.Schema.ParquetSchemaImporter.Import(_arrowSchema);
        RowGroupCount = NativeParquetBridge.GetFileReaderRowGroupCount(_nativeReader);
    }

    public ParquetSchema Schema { get; }

    public int RowGroupCount { get; }

    internal ParquetReadOptions Options { get; }

    public ParquetRowGroupReader OpenRowGroupReader(int rowGroupIndex)
    {
        ValidateRowGroupIndex(rowGroupIndex);
        return OpenRowGroupReaderInternal(rowGroupIndex, null, null);
    }

    /// <summary>
    /// Opens a row-group reader constrained to the specified schema column names.
    /// </summary>
    /// <remarks>
    /// Projection is selected by column name only. Integer read APIs on the returned reader still use original schema ordinals.
    /// </remarks>
    public ParquetRowGroupReader OpenRowGroupReader(int rowGroupIndex, params string[] projectedColumnNames)
    {
        ValidateRowGroupIndex(rowGroupIndex);
        var projectedSchemaOrdinals = ResolveProjectedColumnNameProjection(projectedColumnNames, out var projectedColumnNamesCopy);
        return OpenRowGroupReaderInternal(rowGroupIndex, projectedColumnNamesCopy, projectedSchemaOrdinals);
    }

    private ParquetRowGroupReader OpenRowGroupReaderInternal(int rowGroupIndex, string[]? projectedColumnNames, int[]? projectedSchemaOrdinals)
    {
        var nativeRowGroupReader = NativeParquetBridge.OpenRowGroupReader(_nativeReader, rowGroupIndex);
        var rowCount = NativeParquetBridge.GetRowGroupReaderRowCount(nativeRowGroupReader);
        return new ParquetRowGroupReader(this, rowGroupIndex, nativeRowGroupReader, rowCount, projectedColumnNames, projectedSchemaOrdinals);
    }

    private void ValidateRowGroupIndex(int rowGroupIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);

        if ((uint)rowGroupIndex >= (uint)RowGroupCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowGroupIndex), rowGroupIndex, $"Row group index must be in the range 0..{RowGroupCount - 1}.");
        }
    }

    private int[] ResolveProjectedColumnNameProjection(string[] projectedColumnNames, out string[] projectedColumnNamesCopy)
    {
        TargetFrameworkCompat.ThrowIfNull(projectedColumnNames);

        if (projectedColumnNames.Length == 0)
        {
            throw new ArgumentException("Column projection must include at least one column name.", nameof(projectedColumnNames));
        }

        projectedColumnNamesCopy = new string[projectedColumnNames.Length];
        var projectedSchemaOrdinals = new int[projectedColumnNames.Length];
        var seenSchemaOrdinals = new HashSet<int>();

        for (var i = 0; i < projectedColumnNames.Length; i++)
        {
            var columnName = projectedColumnNames[i];
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("Projected column names cannot contain null, empty, or whitespace values.", nameof(projectedColumnNames));
            }

            var schemaOrdinal = GetColumnIndex(columnName);
            if (!seenSchemaOrdinals.Add(schemaOrdinal))
            {
                throw new ArgumentException($"Column projection contains duplicate column '{Schema.Columns[schemaOrdinal].Name}'.", nameof(projectedColumnNames));
            }

            projectedColumnNamesCopy[i] = Schema.Columns[schemaOrdinal].Name;
            projectedSchemaOrdinals[i] = schemaOrdinal;
        }

        return projectedSchemaOrdinals;
    }

    internal Field GetArrowField(int columnIndex)
    {
        if ((uint)columnIndex >= (uint)_arrowSchema.FieldsList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, $"Column index must be in the range 0..{_arrowSchema.FieldsList.Count - 1}.");
        }

        return _arrowSchema.GetFieldByIndex(columnIndex);
    }

    internal int GetColumnIndex(string columnName)
    {
        TargetFrameworkCompat.ThrowIfNullOrWhiteSpace(columnName);

        for (var i = 0; i < Schema.Columns.Count; i++)
        {
            if (string.Equals(Schema.Columns[i].Name, columnName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(columnName), columnName, $"Column '{columnName}' was not found in the parquet schema.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_nativeReader != IntPtr.Zero)
            {
                NativeParquetBridge.DisposeFileReader(_nativeReader);
            }
        }
        finally
        {
            _source.Dispose();
        }
    }
}
