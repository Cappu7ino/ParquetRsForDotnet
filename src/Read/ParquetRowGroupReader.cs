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
    private readonly HashSet<int>? _projectedSchemaOrdinalSet;
    private bool _disposed;

    internal ParquetRowGroupReader(ParquetFileReader owner, int rowGroupIndex, IntPtr nativeRowGroupReader, long rowCount, string[]? projectedColumnNames = null, int[]? projectedSchemaOrdinals = null)
    {
        if ((projectedColumnNames is null) != (projectedSchemaOrdinals is null))
        {
            throw new ArgumentException("Projected column names and schema ordinals must be provided together.", nameof(projectedColumnNames));
        }

        _owner = owner;
        RowGroupIndex = rowGroupIndex;
        _nativeRowGroupReader = nativeRowGroupReader;
        RowCount = rowCount;
        ProjectedColumnNames = projectedColumnNames is null ? null : System.Array.AsReadOnly(projectedColumnNames);
        _projectedSchemaOrdinalSet = projectedSchemaOrdinals is null ? null : new HashSet<int>(projectedSchemaOrdinals);
    }

    public int RowGroupIndex { get; }

    public long RowCount { get; }

    public int ColumnCount => _owner.Schema.Columns.Count;

    /// <summary>
    /// Gets the number of columns allowed by this row-group reader's projection, or all columns when unprojected.
    /// </summary>
    public int ProjectedColumnCount => ProjectedColumnNames?.Count ?? ColumnCount;

    /// <summary>
    /// Gets the projected schema column names in requested order, or <see langword="null" /> when this reader is unprojected.
    /// </summary>
    public IReadOnlyList<string>? ProjectedColumnNames { get; }

    /// <summary>
    /// Reads an Arrow array by original schema ordinal.
    /// </summary>
    /// <remarks>
    /// For projected row-group readers, <paramref name="columnIndex" /> is still the original schema ordinal, not the position within <see cref="ProjectedColumnNames" />.
    /// </remarks>
    public IArrowArray ReadColumn(int columnIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return NativeParquetBridge.ReadColumn(_nativeRowGroupReader, GetProjectedArrowField(columnIndex));
    }

    public IArrowArray ReadColumn(string columnName)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumn(_owner.GetColumnIndex(columnName));
    }

    /// <summary>
    /// Reads Arrow array batches by original schema ordinal.
    /// </summary>
    /// <remarks>
    /// For projected row-group readers, <paramref name="columnIndex" /> is still the original schema ordinal, not the position within <see cref="ProjectedColumnNames" />.
    /// </remarks>
    public IEnumerable<IArrowArray> ReadColumnBatches(int columnIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        var field = GetProjectedArrowField(columnIndex);
        return ReadColumnBatchesInternal(field, GetReadBatchSize(), 0, null);
    }

    public IEnumerable<IArrowArray> ReadColumnBatches(string columnName)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumnBatches(_owner.GetColumnIndex(columnName));
    }

    /// <summary>
    /// Reads Arrow array batches for a row range within this row group.
    /// The row range limits which input rows are read; read batch size still controls returned array chunking.
    /// </summary>
    /// <remarks>
    /// For projected row-group readers, <paramref name="columnIndex" /> is still the original schema ordinal, not the position within <see cref="ProjectedColumnNames" />.
    /// </remarks>
    public IEnumerable<IArrowArray> ReadColumnBatches(int columnIndex, long rowOffset, long rowCount)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        ValidateReadRange(rowOffset, rowCount);
        var field = GetProjectedArrowField(columnIndex);
        if (rowCount == 0)
        {
            return Enumerable.Empty<IArrowArray>();
        }

        return ReadColumnBatchesInternal(field, GetReadBatchSize(rowCount), rowOffset, rowCount);
    }

    /// <summary>
    /// Reads Arrow array batches for a row range within this row group.
    /// The row range limits which input rows are read; read batch size still controls returned array chunking.
    /// </summary>
    public IEnumerable<IArrowArray> ReadColumnBatches(string columnName, long rowOffset, long rowCount)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumnBatches(_owner.GetColumnIndex(columnName), rowOffset, rowCount);
    }

    /// <summary>
    /// Reads a CLR array by original schema ordinal.
    /// </summary>
    /// <remarks>
    /// For projected row-group readers, <paramref name="columnIndex" /> is still the original schema ordinal, not the position within <see cref="ProjectedColumnNames" />.
    /// </remarks>
    public T[] ReadColumn<T>(int columnIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);

        var field = GetProjectedArrowField(columnIndex);
        ValidateRequestedClrType<T>(field);

        using var array = NativeParquetBridge.ReadColumn(_nativeRowGroupReader, field);
        return (T[])s_clrMaterializer.Materialize(array, field);
    }

    public T[] ReadColumn<T>(string columnName)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumn<T>(_owner.GetColumnIndex(columnName));
    }

    /// <summary>
    /// Reads CLR array batches by original schema ordinal.
    /// </summary>
    /// <remarks>
    /// For projected row-group readers, <paramref name="columnIndex" /> is still the original schema ordinal, not the position within <see cref="ProjectedColumnNames" />.
    /// </remarks>
    public IEnumerable<T[]> ReadColumnBatches<T>(int columnIndex)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);

        var field = GetProjectedArrowField(columnIndex);
        ValidateRequestedClrType<T>(field);

        return ReadColumnBatchesInternal<T>(field, GetReadBatchSize(), 0, null);
    }

    public IEnumerable<T[]> ReadColumnBatches<T>(string columnName)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumnBatches<T>(_owner.GetColumnIndex(columnName));
    }

    /// <summary>
    /// Reads CLR array batches for a row range within this row group.
    /// The row range limits which input rows are read; read batch size still controls returned array chunking.
    /// </summary>
    /// <remarks>
    /// For projected row-group readers, <paramref name="columnIndex" /> is still the original schema ordinal, not the position within <see cref="ProjectedColumnNames" />.
    /// </remarks>
    public IEnumerable<T[]> ReadColumnBatches<T>(int columnIndex, long rowOffset, long rowCount)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        ValidateReadRange(rowOffset, rowCount);
        var field = GetProjectedArrowField(columnIndex);
        ValidateRequestedClrType<T>(field);
        if (rowCount == 0)
        {
            return Enumerable.Empty<T[]>();
        }

        return ReadColumnBatchesInternal<T>(field, GetReadBatchSize(rowCount), rowOffset, rowCount);
    }

    /// <summary>
    /// Reads CLR array batches for a row range within this row group.
    /// The row range limits which input rows are read; read batch size still controls returned array chunking.
    /// </summary>
    public IEnumerable<T[]> ReadColumnBatches<T>(string columnName, long rowOffset, long rowCount)
    {
        TargetFrameworkCompat.ThrowIfDisposed(_disposed, this);
        return ReadColumnBatches<T>(_owner.GetColumnIndex(columnName), rowOffset, rowCount);
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

    private Field GetProjectedArrowField(int columnIndex)
    {
        var field = _owner.GetArrowField(columnIndex);
        if (_projectedSchemaOrdinalSet is not null && !_projectedSchemaOrdinalSet.Contains(columnIndex))
        {
            throw new InvalidOperationException($"Column '{field.Name}' at index {columnIndex} is not included in this row-group reader's column projection.");
        }

        return field;
    }

    private int GetReadBatchSize()
    {
        return GetReadBatchSize(RowCount);
    }

    private int GetReadBatchSize(long availableRows)
    {
        if (_owner.Options.BatchSize is int batchSize)
        {
            return batchSize;
        }

        if (availableRows <= 0)
        {
            return 1;
        }

        return availableRows > int.MaxValue ? int.MaxValue : (int)availableRows;
    }

    private void ValidateReadRange(long rowOffset, long rowCount)
    {
        if (rowOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowOffset), rowOffset, "Row offset must be greater than or equal to zero.");
        }

        if (rowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount), rowCount, "Row count must be greater than or equal to zero.");
        }

        if (rowOffset > RowCount || rowCount > RowCount - rowOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount), rowCount, $"Row range must be within the row-group row count of {RowCount}.");
        }
    }

    private IEnumerable<IArrowArray> ReadColumnBatchesInternal(Field field, int batchSize, long rowOffset, long? rowCount)
    {
        var batchReader = NativeParquetBridge.OpenColumnBatchReader(_nativeRowGroupReader, field, batchSize, rowOffset, rowCount);
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

    private IEnumerable<T[]> ReadColumnBatchesInternal<T>(Field field, int batchSize, long rowOffset, long? rowCount)
    {
        foreach (var batch in ReadColumnBatchesInternal(field, batchSize, rowOffset, rowCount))
        {
            using (batch)
            {
                yield return (T[])s_clrMaterializer.Materialize(batch, field);
            }
        }
    }
}
