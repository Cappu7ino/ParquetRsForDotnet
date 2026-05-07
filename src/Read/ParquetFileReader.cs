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
    {
        TargetFrameworkCompat.ThrowIfNull(input);

        _source = new ManagedParquetSource(input);
        _nativeReader = NativeParquetBridge.OpenFileReader(_source);
        _arrowSchema = NativeParquetBridge.GetFileReaderSchema(_nativeReader);
        Schema = global::ParquetRsForDotnet.Read.Internal.Schema.ParquetSchemaImporter.Import(_arrowSchema);
        RowGroupCount = NativeParquetBridge.GetFileReaderRowGroupCount(_nativeReader);
    }

    public ParquetSchema Schema { get; }

    public int RowGroupCount { get; }

    public ParquetRowGroupReader OpenRowGroupReader(int rowGroupIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);

        if ((uint)rowGroupIndex >= (uint)RowGroupCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowGroupIndex), rowGroupIndex, $"Row group index must be in the range 0..{RowGroupCount - 1}.");
        }

        var nativeRowGroupReader = NativeParquetBridge.OpenRowGroupReader(_nativeReader, rowGroupIndex);
        var rowCount = NativeParquetBridge.GetRowGroupReaderRowCount(nativeRowGroupReader);
        return new ParquetRowGroupReader(this, rowGroupIndex, nativeRowGroupReader, rowCount);
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
