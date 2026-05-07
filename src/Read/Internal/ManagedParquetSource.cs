using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ParquetRsForDotnet.Interop;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Bridges native parquet read callbacks onto a caller-owned managed seekable <see cref="Stream"/>.
/// </summary>
internal sealed unsafe class ManagedParquetSource : IDisposable
{
    private readonly GCHandle _handle;
    private readonly Stream _source;
    private readonly object _sync = new();
#if !NET8_0_OR_GREATER
    // Keep delegate instances rooted for as long as the native reader can call them.
    // Marshal.GetFunctionPointerForDelegate does not keep the delegate alive.
    private readonly ReadAtCallbackDelegate _readAtCallback;
    private readonly GetLengthCallbackDelegate _getLengthCallback;
    private readonly GetLastErrorCallbackDelegate _getLastErrorCallback;
#endif
    private bool _disposed;
    private string? _lastError;
    private IntPtr _lastErrorPtr;

    public ManagedParquetSource(Stream source)
    {
        TargetFrameworkCompat.ThrowIfNull(source);

        if (!source.CanSeek)
        {
            throw new ArgumentException("Input stream must support seeking.", nameof(source));
        }

        _source = source;
        _handle = GCHandle.Alloc(this);
#if NET8_0_OR_GREATER
        NativeSource = new ParquetInputSource
        {
            ReadAt = &ReadAtCallback,
            GetLength = &GetLengthCallback,
            GetLastError = &GetLastErrorCallback,
            Context = GCHandle.ToIntPtr(_handle),
        };
#else
        _readAtCallback = ReadAtCallback;
        _getLengthCallback = GetLengthCallback;
        _getLastErrorCallback = GetLastErrorCallback;
        NativeSource = new ParquetInputSource
        {
            ReadAt = Marshal.GetFunctionPointerForDelegate(_readAtCallback),
            GetLength = Marshal.GetFunctionPointerForDelegate(_getLengthCallback),
            GetLastError = Marshal.GetFunctionPointerForDelegate(_getLastErrorCallback),
            Context = GCHandle.ToIntPtr(_handle),
        };
#endif
    }

    public ParquetInputSource NativeSource { get; }

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

    private static ManagedParquetSource FromContext(IntPtr context)
    {
        return (ManagedParquetSource)GCHandle.FromIntPtr(context).Target!;
    }

#if NET8_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ReadAtCallback(IntPtr context, long offset, byte* buffer, nuint length, nuint* bytesRead)
#else
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ReadAtCallbackDelegate(IntPtr context, long offset, byte* buffer, UIntPtr length, UIntPtr* bytesRead);

    private static int ReadAtCallback(IntPtr context, long offset, byte* buffer, UIntPtr length, UIntPtr* bytesRead)
#endif
    {
        var source = FromContext(context);
        try
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "Read offset cannot be negative.");
            }

            lock (source._sync)
            {
                source._source.Position = offset;
#if NET8_0_OR_GREATER
                var read = source._source.Read(new Span<byte>(buffer, checked((int)length)));
#else
                // Stream span overloads are unavailable on netstandard2.0, so read
                // into a managed buffer and copy only the bytes actually read.
                byte[] managedBuffer = new byte[checked((int)(ulong)length)];
                var read = source._source.Read(managedBuffer, 0, managedBuffer.Length);
                Marshal.Copy(managedBuffer, 0, (IntPtr)buffer, read);
#endif
                if (bytesRead != null)
                {
#if NET8_0_OR_GREATER
                    *bytesRead = (nuint)read;
#else
                    *bytesRead = (UIntPtr)read;
#endif
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            source.SetLastError(ex.Message);
            return (int)NativeErrorCode.SourceReadFailed;
        }
    }

#if NET8_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#else
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetLengthCallbackDelegate(IntPtr context, long* length);

#endif
    private static int GetLengthCallback(IntPtr context, long* length)
    {
        var source = FromContext(context);
        try
        {
            if (length is null)
            {
                throw new ArgumentNullException(nameof(length));
            }

            lock (source._sync)
            {
                *length = source._source.Length;
            }

            return 0;
        }
        catch (Exception ex)
        {
            source.SetLastError(ex.Message);
            return (int)NativeErrorCode.SourceReadFailed;
        }
    }

#if NET8_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#else
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte* GetLastErrorCallbackDelegate(IntPtr context);

#endif
    private static byte* GetLastErrorCallback(IntPtr context)
    {
        var source = FromContext(context);
        if (string.IsNullOrEmpty(source._lastError))
        {
            return null;
        }

        if (source._lastErrorPtr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(source._lastErrorPtr);
        }

        source._lastErrorPtr = TargetFrameworkCompat.StringToCoTaskMemUtf8(source._lastError);
        return (byte*)source._lastErrorPtr;
    }

    private void SetLastError(string message)
    {
        _lastError = message;
    }
}
