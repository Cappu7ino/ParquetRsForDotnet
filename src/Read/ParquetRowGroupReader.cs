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
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return NativeParquetBridge.ReadColumn(_nativeRowGroupReader, _owner.GetArrowField(columnIndex));
    }

    public IArrowArray ReadColumn(string columnName)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumn(_owner.GetColumnIndex(columnName));
    }

    public IEnumerable<IArrowArray> ReadColumnBatches(int columnIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        var field = _owner.GetArrowField(columnIndex);
        return ReadColumnBatchesCore(field, GetReadBatchSize());
    }

    public IEnumerable<IArrowArray> ReadColumnBatches(string columnName)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumnBatches(_owner.GetColumnIndex(columnName));
    }

    public T[] ReadColumn<T>(int columnIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);

        var field = _owner.GetArrowField(columnIndex);
        ValidateRequestedClrType<T>(field);

        using var array = ReadColumn(columnIndex);
        return (T[])s_clrMaterializer.Materialize(array, field);
    }

    public T[] ReadColumn<T>(string columnName)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumn<T>(_owner.GetColumnIndex(columnName));
    }

    public IEnumerable<T[]> ReadColumnBatches<T>(int columnIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);

        var field = _owner.GetArrowField(columnIndex);
        ValidateRequestedClrType<T>(field);

        return ReadColumnBatchesCore<T>(field, GetReadBatchSize());
    }

    public IEnumerable<T[]> ReadColumnBatches<T>(string columnName)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumnBatches<T>(_owner.GetColumnIndex(columnName));
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

    private int GetReadBatchSize()
    {
        if (_owner.Options.BatchSize is int batchSize)
        {
            return batchSize;
        }

        if (RowCount <= 0)
        {
            return 1;
        }

        return RowCount > int.MaxValue ? int.MaxValue : (int)RowCount;
    }

    private IEnumerable<IArrowArray> ReadColumnBatchesCore(Field field, int batchSize)
    {
        var batchReader = NativeParquetBridge.OpenColumnBatchReader(_nativeRowGroupReader, field, batchSize);
        try
        {
            while (true)
            {
                TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
                var batch = NativeParquetBridge.ReadNextColumnBatch(batchReader, field);
                if (batch is null)
                {
                    yield break;
                }

                yield return batch;
            }
        }
        finally
        {
            NativeParquetBridge.DisposeColumnBatchReader(batchReader);
        }
    }

    private IEnumerable<T[]> ReadColumnBatchesCore<T>(Field field, int batchSize)
    {
        foreach (var batch in ReadColumnBatchesCore(field, batchSize))
        {
            using (batch)
            {
                yield return (T[])s_clrMaterializer.Materialize(batch, field);
            }
        }
    }
}
