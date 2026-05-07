using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ParquetRsForDotnet.Interop;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Bridges native parquet write callbacks onto a caller-owned managed <see cref="Stream"/>.
/// </summary>
internal sealed unsafe class ManagedParquetSink : IDisposable
{
    private readonly GCHandle _handle;
    private readonly Stream _destination;
#if !NET8_0_OR_GREATER
    // Keep delegate instances rooted for as long as the native sink can call them.
    // Marshal.GetFunctionPointerForDelegate does not keep the delegate alive.
    private readonly WriteCallbackDelegate _writeCallback;
    private readonly SimpleCallbackDelegate _flushCallback;
    private readonly SimpleCallbackDelegate _closeCallback;
    private readonly SimpleCallbackDelegate _abortCallback;
    private readonly GetLastErrorCallbackDelegate _getLastErrorCallback;
#endif
    private bool _disposed;
    private string? _lastError;
    private IntPtr _lastErrorPtr;

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
        _destination = destination;
        _handle = GCHandle.Alloc(this);
#if NET8_0_OR_GREATER
        NativeSink = new ParquetOutputSink
        {
            Write = &WriteCallback,
            Flush = &FlushCallback,
            Close = &CloseCallback,
            Abort = &AbortCallback,
            GetLastError = &GetLastErrorCallback,
            Context = GCHandle.ToIntPtr(_handle),
        };
#else
        _writeCallback = WriteCallback;
        _flushCallback = FlushCallback;
        _closeCallback = CloseCallback;
        _abortCallback = AbortCallback;
        _getLastErrorCallback = GetLastErrorCallback;
        NativeSink = new ParquetOutputSink
        {
            Write = Marshal.GetFunctionPointerForDelegate(_writeCallback),
            Flush = Marshal.GetFunctionPointerForDelegate(_flushCallback),
            Close = Marshal.GetFunctionPointerForDelegate(_closeCallback),
            Abort = Marshal.GetFunctionPointerForDelegate(_abortCallback),
            GetLastError = Marshal.GetFunctionPointerForDelegate(_getLastErrorCallback),
            Context = GCHandle.ToIntPtr(_handle),
        };
#endif
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }

        if (_lastErrorPtr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(_lastErrorPtr);
            _lastErrorPtr = IntPtr.Zero;
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
#if NET8_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WriteCallback(IntPtr context, byte* data, nuint length, nuint* written)
#else
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int WriteCallbackDelegate(IntPtr context, byte* data, UIntPtr length, UIntPtr* written);

    private static int WriteCallback(IntPtr context, byte* data, UIntPtr length, UIntPtr* written)
#endif
    {
        var sink = FromContext(context);
        try
        {
#if NET8_0_OR_GREATER
            sink._destination.Write(new ReadOnlySpan<byte>(data, checked((int)length)));
#else
            // Stream span overloads are unavailable on netstandard2.0, so copy the
            // native buffer through a managed byte[] before writing to the stream.
            var byteCount = checked((int)(ulong)length);
            byte[] buffer = new byte[byteCount];
            Marshal.Copy((IntPtr)data, buffer, 0, buffer.Length);
            sink._destination.Write(buffer, 0, buffer.Length);
#endif
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
#if NET8_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#else
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SimpleCallbackDelegate(IntPtr context);

#endif
    private static int FlushCallback(IntPtr context)
    {
        var sink = FromContext(context);
        try
        {
            sink._destination.Flush();
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
#if NET8_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#endif
    private static int CloseCallback(IntPtr context)
    {
        var sink = FromContext(context);
        try
        {
            sink._destination.Flush();
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
#if NET8_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#endif
    private static int AbortCallback(IntPtr context)
    {
        var sink = FromContext(context);
        sink._lastError ??= "Write aborted.";
        return (int)NativeErrorCode.SinkWriteFailed;
    }

    /// <summary>
    /// Returns the last sink error as a UTF-8 unmanaged string.
    /// </summary>
    /// <param name="context">The unmanaged callback context.</param>
    /// <returns>The pointer to the unmanaged UTF-8 error string.</returns>
#if NET8_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#else
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte* GetLastErrorCallbackDelegate(IntPtr context);

#endif
    private static byte* GetLastErrorCallback(IntPtr context)
    {
        var sink = FromContext(context);
        if (string.IsNullOrEmpty(sink._lastError))
        {
            return null;
        }

        if (sink._lastErrorPtr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(sink._lastErrorPtr);
        }

        sink._lastErrorPtr = TargetFrameworkCompat.StringToCoTaskMemUtf8(sink._lastError);
        return (byte*)sink._lastErrorPtr;
    }

    /// <summary>
    /// Updates the sink's last managed error message.
    /// </summary>
    /// <param name="message">The latest managed error message.</param>
    private void SetLastError(string message)
    {
        _lastError = message;
    }
}
