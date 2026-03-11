using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using ParquetRsForDotnet.Interop;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Bridges native parquet write callbacks onto a caller-owned managed <see cref="Stream"/>.
/// </summary>
internal sealed unsafe class ManagedParquetSink : IDisposable
{
    private readonly GCHandle handle;
    private readonly Stream destination;
    private bool disposed;
    private string? lastError;
    private IntPtr lastErrorPtr;

    /// <summary>
    /// Initializes a managed sink wrapper around the destination stream.
    /// </summary>
    /// <param name="destination">The destination stream that receives parquet bytes.</param>
    /// <remarks>
    /// The sink pins a callback context for the duration of the native write so Rust can stream
    /// parquet bytes directly into managed code without staging the whole file in memory.
    /// </remarks>
    public ManagedParquetSink(Stream destination)
    {
        this.destination = destination;
        handle = GCHandle.Alloc(this);
        NativeSink = new ParquetOutputSink
        {
            Write = &WriteCallback,
            Flush = &FlushCallback,
            Close = &CloseCallback,
            Abort = &AbortCallback,
            GetLastError = &GetLastErrorCallback,
            Context = GCHandle.ToIntPtr(handle),
        };
    }

    public ParquetOutputSink NativeSink { get; }

    /// <summary>
    /// Releases the callback handle and any cached unmanaged error message.
    /// </summary>
    /// <remarks>
    /// The destination stream itself is still owned by the caller; disposing the sink only tears
    /// down the callback bridge and any temporary unmanaged error buffer.
    /// </remarks>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (handle.IsAllocated)
        {
            handle.Free();
        }

        if (lastErrorPtr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(lastErrorPtr);
            lastErrorPtr = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Resolves the managed sink instance from the unmanaged callback context.
    /// </summary>
    /// <param name="context">The unmanaged callback context.</param>
    /// <returns>The managed sink instance.</returns>
    private static ManagedParquetSink FromContext(IntPtr context)
    {
        return (ManagedParquetSink)GCHandle.FromIntPtr(context).Target!;
    }

    /// <summary>
    /// Writes native parquet bytes into the managed destination stream.
    /// </summary>
    /// <param name="context">The unmanaged callback context.</param>
    /// <param name="data">The pointer to the source bytes.</param>
    /// <param name="length">The number of bytes to write.</param>
    /// <param name="written">The number of bytes successfully written.</param>
    /// <returns>A native error code.</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WriteCallback(IntPtr context, byte* data, nuint length, nuint* written)
    {
        var sink = FromContext(context);
        try
        {
            sink.destination.Write(new ReadOnlySpan<byte>(data, checked((int)length)));
            if (written != null)
            {
                *written = length;
            }

            return 0;
        }
        catch (Exception ex)
        {
            sink.SetLastError(ex.Message);
            return (int)NativeErrorCode.SinkWriteFailed;
        }
    }

    /// <summary>
    /// Flushes the managed destination stream.
    /// </summary>
    /// <param name="context">The unmanaged callback context.</param>
    /// <returns>A native error code.</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int FlushCallback(IntPtr context)
    {
        var sink = FromContext(context);
        try
        {
            sink.destination.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            sink.SetLastError(ex.Message);
            return (int)NativeErrorCode.SinkWriteFailed;
        }
    }

    /// <summary>
    /// Completes the sink from the native side.
    /// </summary>
    /// <param name="context">The unmanaged callback context.</param>
    /// <returns>A native error code.</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CloseCallback(IntPtr context)
    {
        var sink = FromContext(context);
        try
        {
            sink.destination.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            sink.SetLastError(ex.Message);
            return (int)NativeErrorCode.SinkWriteFailed;
        }
    }

    /// <summary>
    /// Records an abort on the managed sink.
    /// </summary>
    /// <param name="context">The unmanaged callback context.</param>
    /// <returns>A native error code.</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AbortCallback(IntPtr context)
    {
        var sink = FromContext(context);
        sink.lastError ??= "Write aborted.";
        return (int)NativeErrorCode.SinkWriteFailed;
    }

    /// <summary>
    /// Returns the last sink error as a UTF-8 unmanaged string.
    /// </summary>
    /// <param name="context">The unmanaged callback context.</param>
    /// <returns>The pointer to the unmanaged UTF-8 error string.</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte* GetLastErrorCallback(IntPtr context)
    {
        var sink = FromContext(context);
        if (string.IsNullOrEmpty(sink.lastError))
        {
            return null;
        }

        if (sink.lastErrorPtr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(sink.lastErrorPtr);
        }

        sink.lastErrorPtr = Marshal.StringToCoTaskMemUTF8(sink.lastError);
        return (byte*)sink.lastErrorPtr;
    }

    /// <summary>
    /// Updates the sink's last managed error message.
    /// </summary>
    /// <param name="message">The latest managed error message.</param>
    private void SetLastError(string message)
    {
        lastError = message;
    }
}
