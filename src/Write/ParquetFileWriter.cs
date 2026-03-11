using Apache.Arrow;
using ParquetRsForDotnet.Internal;

namespace ParquetRsForDotnet;

/// <summary>
/// Writes parquet data incrementally in schema order using explicit column batches.
/// </summary>
public sealed class ParquetFileWriter : IDisposable
{
    private readonly ArrowRecordBatchBuilder batchBuilder;
    private readonly ManagedParquetSink sink;
    private IntPtr nativeWriter;
    private bool finished;
    private bool disposed;

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
        batchBuilder = new ArrowRecordBatchBuilder(schema, effectiveOptions);
        sink = new ManagedParquetSink(output);
        nativeWriter = NativeParquetBridge.CreateFileWriter(batchBuilder.ArrowSchema, sink, effectiveOptions);
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
        ObjectDisposedException.ThrowIf(disposed, this);
        ThrowIfFinished();

        var recordBatch = batchBuilder.Build(columns);
        NativeParquetBridge.WriteBatch(nativeWriter, recordBatch);
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
        ObjectDisposedException.ThrowIf(disposed, this);
        ThrowIfFinished();

        var recordBatch = batchBuilder.Build(columns);
        NativeParquetBridge.WriteBatch(nativeWriter, recordBatch);
    }

    /// <summary>
    /// Finishes the parquet file and prevents further writes.
    /// </summary>
    public void Finish()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ThrowIfFinished();

        NativeParquetBridge.FinishFileWriter(nativeWriter);
        finished = true;
    }

    /// <summary>
    /// Finishes the parquet file if needed and releases native resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            if (nativeWriter != IntPtr.Zero)
            {
                if (!finished)
                {
                    NativeParquetBridge.FinishFileWriter(nativeWriter);
                    finished = true;
                }

                NativeParquetBridge.DisposeFileWriter(nativeWriter);
                nativeWriter = IntPtr.Zero;
            }
        }
        finally
        {
            sink.Dispose();
        }
    }

    private void ThrowIfFinished()
    {
        if (finished)
        {
            throw new InvalidOperationException("Parquet file writer has already been finished.");
        }
    }
}
