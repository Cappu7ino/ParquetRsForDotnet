using System.Runtime.InteropServices;

namespace ParquetRsForDotnet.Interop;

[StructLayout(LayoutKind.Sequential)]
/// <summary>
/// Represents one file-level metadata entry projected into the native ABI.
/// </summary>
internal struct NativeKeyValuePair
{
    public IntPtr Key;
    public IntPtr Value;
}

[StructLayout(LayoutKind.Sequential)]
/// <summary>
/// Represents the managed sink callback table projected into the native ABI.
/// </summary>
internal unsafe struct ParquetOutputSink
{
    /// <summary>
    /// Gets or sets the native callback used to write bytes into the managed sink.
    /// </summary>
#if NET8_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, byte*, nuint, nuint*, int> Write;
#else
    public IntPtr Write;
#endif

    /// <summary>
    /// Gets or sets the native callback used to flush the managed sink.
    /// </summary>
#if NET8_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, int> Flush;
#else
    public IntPtr Flush;
#endif

    /// <summary>
    /// Gets or sets the native callback used to complete the managed sink.
    /// </summary>
#if NET8_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, int> Close;
#else
    public IntPtr Close;
#endif

    /// <summary>
    /// Gets or sets the native callback used to abort the managed sink.
    /// </summary>
#if NET8_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, int> Abort;
#else
    public IntPtr Abort;
#endif

    /// <summary>
    /// Gets or sets the native callback used to fetch the last sink error.
    /// </summary>
#if NET8_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, byte*> GetLastError;
#else
    public IntPtr GetLastError;
#endif

    /// <summary>
    /// Gets or sets the opaque callback context for the sink.
    /// </summary>
    public IntPtr Context;
}

[StructLayout(LayoutKind.Sequential)]
/// <summary>
/// Represents the managed source callback table projected into the native ABI.
/// </summary>
internal unsafe struct ParquetInputSource
{
    /// <summary>
    /// Gets or sets the native callback used to read bytes at a given offset from the managed source.
    /// </summary>
#if NET8_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, long, byte*, nuint, nuint*, int> ReadAt;
#else
    public IntPtr ReadAt;
#endif

    /// <summary>
    /// Gets or sets the native callback used to retrieve the total source length.
    /// </summary>
#if NET8_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, long*, int> GetLength;
#else
    public IntPtr GetLength;
#endif

    /// <summary>
    /// Gets or sets the native callback used to fetch the last source error.
    /// </summary>
#if NET8_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, byte*> GetLastError;
#else
    public IntPtr GetLastError;
#endif

    /// <summary>
    /// Gets or sets the opaque callback context for the source.
    /// </summary>
    public IntPtr Context;
}

[StructLayout(LayoutKind.Sequential)]
/// <summary>
/// Represents the managed write options projected into the native ABI.
/// </summary>
internal struct ParquetWriteOptionsNative
{
    /// <summary>
    /// Gets or sets the integer compression code expected by the Rust writer.
    /// </summary>
    public int Compression;

    /// <summary>
    /// Gets or sets a value indicating whether dictionary encoding is enabled.
    /// </summary>
    public int EnableDictionaryEncoding;

    /// <summary>
    /// Gets or sets the parquet statistics level expected by the Rust writer.
    /// </summary>
    public int StatisticsLevel;

    /// <summary>
    /// Gets or sets the parquet-rs encoder chunk size, or -1 when unset.
    /// This is not a managed batch size or row-group limit.
    /// </summary>
    public int NativeWriteBatchSize;

    /// <summary>
    /// Gets or sets the maximum row-group row count, or -1 when unset.
    /// </summary>
    public long MaxRowGroupRows;

    /// <summary>
    /// Gets or sets the maximum row-group size in bytes, or -1 when unset.
    /// </summary>
    public long MaxRowGroupBytes;

    /// <summary>
    /// Gets or sets the maximum data page row count, or -1 when unset.
    /// </summary>
    public int DataPageRowCountLimit;

    /// <summary>
    /// Gets or sets the maximum data page size in bytes, or -1 when unset.
    /// </summary>
    public int DataPageSizeLimitBytes;

    /// <summary>
    /// Gets or sets the maximum dictionary page size in bytes, or -1 when unset.
    /// </summary>
    public int DictionaryPageSizeLimitBytes;

    /// <summary>
    /// Gets or sets the native writer memory threshold in bytes, or -1 when unset.
    /// </summary>
    public long MaxNativeWriterMemoryBytes;

    /// <summary>
    /// Gets or sets the optional created-by string pointer.
    /// </summary>
    public IntPtr CreatedBy;

    /// <summary>
    /// Gets or sets the optional file metadata pointer.
    /// </summary>
    public IntPtr Metadata;

    /// <summary>
    /// Gets or sets the number of key/value metadata pairs.
    /// </summary>
    public int MetadataCount;
}

[StructLayout(LayoutKind.Sequential)]
/// <summary>
/// Represents the native error payload returned from Rust.
/// </summary>
internal struct NativeError
{
    /// <summary>
    /// Gets or sets the native error code.
    /// </summary>
    public int Code;

    /// <summary>
    /// Gets or sets the pointer to the native UTF-8 error message.
    /// </summary>
    public IntPtr Message;
}
