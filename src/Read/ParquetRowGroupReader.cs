using Apache.Arrow;
using ParquetRsForDotnet.Internal;

namespace ParquetRsForDotnet;

/// <summary>
/// Reads Arrow-native column data from a single parquet row group.
/// </summary>
public sealed class ParquetRowGroupReader : IDisposable
{
    private static readonly ArrowArrayClrMaterializer s_clrMaterializer = new();
    private readonly IntPtr _nativeRowGroupReader;
    private readonly ParquetFileReader _owner;
    private bool _disposed;

    internal ParquetRowGroupReader(ParquetFileReader owner, int rowGroupIndex, IntPtr nativeRowGroupReader, long rowCount)
    {
        _owner = owner;
        RowGroupIndex = rowGroupIndex;
        _nativeRowGroupReader = nativeRowGroupReader;
        RowCount = rowCount;
    }

    public int RowGroupIndex { get; }

    public long RowCount { get; }

    public int ColumnCount => _owner.Schema.Columns.Count;

    public IArrowArray ReadColumn(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return NativeParquetBridge.ReadColumn(_nativeRowGroupReader, _owner.GetArrowField(columnIndex));
    }

    public IArrowArray ReadColumn(string columnName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ReadColumn(_owner.GetColumnIndex(columnName));
    }

    public T[] ReadColumn<T>(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var field = _owner.GetArrowField(columnIndex);
        ValidateRequestedClrType<T>(field);

        using var array = ReadColumn(columnIndex);
        return (T[])s_clrMaterializer.Materialize(array, field);
    }

    public T[] ReadColumn<T>(string columnName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ReadColumn<T>(_owner.GetColumnIndex(columnName));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeParquetBridge.DisposeRowGroupReader(_nativeRowGroupReader);
    }

    private static void ValidateRequestedClrType<T>(Field field)
    {
        var requestedType = typeof(T);
        var expectedType = GetExpectedRequestedClrType(field);

        if (requestedType != expectedType)
        {
            throw new InvalidOperationException($"Column '{field.Name}' expects CLR type '{expectedType}', but read was requested as '{requestedType}'.");
        }
    }

    private static Type GetExpectedRequestedClrType(Field field)
    {
        var expectedType = ArrowArrayClrMaterializer.GetExpectedClrType(field);
        if (!field.IsNullable || !expectedType.IsValueType)
        {
            return expectedType;
        }

        return typeof(Nullable<>).MakeGenericType(expectedType);
    }
}
