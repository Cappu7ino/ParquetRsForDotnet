using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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
    private bool _disposed;
    private string? _lastError;
    private IntPtr _lastErrorPtr;

    public ManagedParquetSource(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanSeek)
        {
            throw new ArgumentException("Input stream must support seeking.", nameof(source));
        }

        _source = source;
        _handle = GCHandle.Alloc(this);
        NativeSource = new ParquetInputSource
        {
            ReadAt = &ReadAtCallback,
            GetLength = &GetLengthCallback,
            GetLastError = &GetLastErrorCallback,
            Context = GCHandle.ToIntPtr(_handle),
        };
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ReadAtCallback(IntPtr context, long offset, byte* buffer, nuint length, nuint* bytesRead)
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
                var read = source._source.Read(new Span<byte>(buffer, checked((int)length)));
                if (bytesRead != null)
                {
                    *bytesRead = (nuint)read;
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
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

        source._lastErrorPtr = Marshal.StringToCoTaskMemUTF8(source._lastError);
        return (byte*)source._lastErrorPtr;
    }

    private void SetLastError(string message)
    {
        _lastError = message;
    }
}
