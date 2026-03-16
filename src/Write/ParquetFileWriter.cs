using Apache.Arrow;
using ParquetRsForDotnet.Internal;

namespace ParquetRsForDotnet;

/// <summary>
/// Writes parquet data incrementally in schema order using explicit column batches.
/// </summary>
public sealed class ParquetFileWriter : IDisposable
{
    private readonly ArrowRecordBatchBuilder _batchBuilder;
    private readonly ManagedParquetSink _sink;
    private IntPtr _nativeWriter;
    private bool _finished;
    private bool _disposed;

    /// <summary>
    /// Initializes a new appendable parquet file writer.
    /// </summary>
    /// <param name="output">The destination stream that receives parquet bytes.</param>
    /// <param name="schema">The explicit ordered schema for all written batches.</param>
    /// <param name="options">Optional parquet write settings.</param>
    public ParquetFileWriter(Stream output, ParquetSchema schema, ParquetWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(schema);

        var effectiveOptions = ParquetWriteOptionsDefaults.ApplyForBatchWriter(options ?? new ParquetWriteOptions());
        _batchBuilder = new ArrowRecordBatchBuilder(schema, effectiveOptions);
        _sink = new ManagedParquetSink(output);
        _nativeWriter = NativeParquetBridge.CreateFileWriter(_batchBuilder.ArrowSchema, _sink, effectiveOptions);
    }

    /// <summary>
    /// Writes one batch from CLR arrays in strict schema order.
    /// </summary>
    /// <param name="columns">The batch columns in schema order.</param>
    public void WriteBatch(params System.Array[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        WriteBatch((IReadOnlyList<System.Array>)columns);
    }

    /// <summary>
    /// Writes one batch from CLR arrays in strict schema order.
    /// </summary>
    /// <param name="columns">The batch columns in schema order.</param>
    public void WriteBatch(IReadOnlyList<System.Array> columns)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfFinished();

        var recordBatch = _batchBuilder.Build(columns);
        NativeParquetBridge.WriteBatch(_nativeWriter, recordBatch);
    }

    /// <summary>
    /// Writes one batch from Arrow arrays in strict schema order.
    /// </summary>
    /// <param name="columns">The batch columns in schema order.</param>
    public void WriteBatch(params IArrowArray[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        WriteBatch((IReadOnlyList<IArrowArray>)columns);
    }

    /// <summary>
    /// Writes one batch from Arrow arrays in strict schema order.
    /// </summary>
    /// <param name="columns">The batch columns in schema order.</param>
    public void WriteBatch(IReadOnlyList<IArrowArray> columns)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfFinished();

        var recordBatch = _batchBuilder.Build(columns);
        NativeParquetBridge.WriteBatch(_nativeWriter, recordBatch);
    }

    /// <summary>
    /// Finishes the parquet file and prevents further writes.
    /// </summary>
    public void Finish()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfFinished();

        NativeParquetBridge.FinishFileWriter(_nativeWriter);
        _finished = true;
    }

    /// <summary>
    /// Finishes the parquet file if needed and releases native resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_nativeWriter != IntPtr.Zero)
            {
                if (!_finished)
                {
                    NativeParquetBridge.FinishFileWriter(_nativeWriter);
                    _finished = true;
                }

                NativeParquetBridge.DisposeFileWriter(_nativeWriter);
                _nativeWriter = IntPtr.Zero;
            }
        }
        finally
        {
            _sink.Dispose();
        }
    }

    private void ThrowIfFinished()
    {
        if (_finished)
        {
            throw new InvalidOperationException("Parquet file writer has already been finished.");
        }
    }
}
