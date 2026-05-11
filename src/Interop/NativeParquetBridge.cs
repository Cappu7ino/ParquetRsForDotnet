using System.Runtime.InteropServices;
using System.Reflection;
using Apache.Arrow;
using Apache.Arrow.C;
using ParquetRsForDotnet.Interop;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Contains the managed P/Invoke boundary for the Rust parquet writer and the helper types needed
/// to marshal write options across that boundary.
/// </summary>
internal static unsafe class NativeParquetBridge
{
    private const string NativeLibraryBaseName = "parquet_rs_for_dotnet";

    static NativeParquetBridge()
    {
#if NET8_0_OR_GREATER
        NativeLibrary.SetDllImportResolver(typeof(NativeParquetBridge).Assembly, ResolveNativeLibrary);
#else
        PreloadNativeLibrary(typeof(NativeParquetBridge).Assembly);
#endif
    }

    internal static IntPtr CreateFileWriter(Apache.Arrow.Schema schema, ManagedParquetSink sink, ParquetWriteOptions options)
    {
        TargetFrameworkCompat.ThrowIfNull(schema);
        TargetFrameworkCompat.ThrowIfNull(sink);
        TargetFrameworkCompat.ThrowIfNull(options);

        using var exporter = new ArrowSchemaExporter(schema);
        using var nativeOptions = NativeOptionScope.Create(options);
        NativeError error = default;
        var sinkNative = sink.NativeSink;
        IntPtr writer = IntPtr.Zero;

        try
        {
            var nativeOptionsValue = nativeOptions.Options;
            var result = parquet_file_writer_create(exporter.NativeSchema, &nativeOptionsValue, &sinkNative, &writer, &error);
            if (result == 0)
            {
                return writer;
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static void WriteBatch(IntPtr writer, RecordBatch batch)
    {
        TargetFrameworkCompat.ThrowIfNull(batch);

        NativeError error = default;
        var nativeBatch = CArrowArray.Create();

        try
        {
            CArrowArrayExporter.ExportRecordBatch(batch, nativeBatch);

            var result = parquet_file_writer_write_batch(writer, nativeBatch, &error);
            if (result == 0)
            {
                return;
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
        finally
        {
            CArrowArray.Free(nativeBatch);
            batch.Dispose();
        }
    }

    internal static void FinishFileWriter(IntPtr writer)
    {
        NativeError error = default;

        try
        {
            var result = parquet_file_writer_finish(writer, &error);
            if (result == 0)
            {
                return;
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static void DisposeFileWriter(IntPtr writer)
    {
        try
        {
            parquet_file_writer_dispose(writer);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static IntPtr OpenFileReader(ManagedParquetSource source)
    {
        TargetFrameworkCompat.ThrowIfNull(source);

        NativeError error = default;
        var nativeSource = source.NativeSource;
        IntPtr reader = IntPtr.Zero;

        try
        {
            var result = parquet_file_reader_open(&nativeSource, &reader, &error);
            if (result == 0)
            {
                return reader;
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static Schema GetFileReaderSchema(IntPtr reader)
    {
        NativeError error = default;
        var nativeSchema = CArrowSchema.Create();

        try
        {
            var result = parquet_file_reader_get_schema(reader, nativeSchema, &error);
            if (result == 0)
            {
                return ArrowSchemaImporter.Import(nativeSchema);
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
        finally
        {
            CArrowSchema.Free(nativeSchema);
        }
    }

    internal static int GetFileReaderRowGroupCount(IntPtr reader)
    {
        NativeError error = default;
        var rowGroupCount = 0;

        try
        {
            var result = parquet_file_reader_get_row_group_count(reader, &rowGroupCount, &error);
            if (result == 0)
            {
                return rowGroupCount;
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static IntPtr OpenRowGroupReader(IntPtr reader, int rowGroupIndex)
    {
        NativeError error = default;
        IntPtr rowGroupReader = IntPtr.Zero;

        try
        {
            var result = parquet_file_reader_open_row_group(reader, rowGroupIndex, &rowGroupReader, &error);
            if (result == 0)
            {
                return rowGroupReader;
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static long GetRowGroupReaderRowCount(IntPtr rowGroupReader)
    {
        NativeError error = default;
        long rowCount = 0;

        try
        {
            var result = parquet_row_group_reader_get_row_count(rowGroupReader, &rowCount, &error);
            if (result == 0)
            {
                return rowCount;
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static IArrowArray ReadColumn(IntPtr rowGroupReader, Field field)
    {
        TargetFrameworkCompat.ThrowIfNull(field);

        NativeError error = default;
        var nativeArray = CArrowArray.Create();

        try
        {
            IntPtr columnName = TargetFrameworkCompat.StringToCoTaskMemUtf8(field.Name);
            try
            {
                var result = parquet_row_group_reader_read_column(rowGroupReader, columnName, nativeArray, &error);
                if (result == 0)
                {
                    return ArrowArrayImporter.Import(nativeArray, field.DataType);
                }

                throw CreateException(result, error.Message);
            }
            finally
            {
                Marshal.FreeCoTaskMem(columnName);
            }
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
        finally
        {
            CArrowArray.Free(nativeArray);
        }
    }

    internal static IntPtr OpenColumnBatchReader(IntPtr rowGroupReader, Field field, int batchSize)
    {
        TargetFrameworkCompat.ThrowIfNull(field);

        NativeError error = default;
        IntPtr batchReader = IntPtr.Zero;
        IntPtr columnName = TargetFrameworkCompat.StringToCoTaskMemUtf8(field.Name);

        try
        {
            var result = parquet_row_group_column_batch_reader_open(rowGroupReader, columnName, batchSize, &batchReader, &error);
            if (result == 0)
            {
                return batchReader;
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
        finally
        {
            Marshal.FreeCoTaskMem(columnName);
        }
    }

    internal static IArrowArray? ReadNextColumnBatch(IntPtr batchReader, Field field)
    {
        TargetFrameworkCompat.ThrowIfNull(field);

        NativeError error = default;
        var nativeArray = CArrowArray.Create();
        int hasBatch = 0;

        try
        {
            var result = parquet_row_group_column_batch_reader_next(batchReader, nativeArray, &hasBatch, &error);
            if (result == 0)
            {
                return hasBatch == 0 ? null : ArrowArrayImporter.Import(nativeArray, field.DataType);
            }

            throw CreateException(result, error.Message);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
        finally
        {
            CArrowArray.Free(nativeArray);
        }
    }

    internal static void DisposeColumnBatchReader(IntPtr batchReader)
    {
        try
        {
            parquet_row_group_column_batch_reader_dispose(batchReader);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static void DisposeRowGroupReader(IntPtr rowGroupReader)
    {
        try
        {
            parquet_row_group_reader_dispose(rowGroupReader);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

    internal static void DisposeFileReader(IntPtr reader)
    {
        try
        {
            parquet_file_reader_dispose(reader);
        }
        catch (DllNotFoundException ex)
        {
            throw new NativeParquetException(NativeErrorCode.NativeLibraryNotFound, ex.Message);
        }
    }

#if NET8_0_OR_GREATER
    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, NativeLibraryBaseName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in EnumerateNativeLibraryCandidates(assembly))
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }
#else
    private static void PreloadNativeLibrary(Assembly assembly)
    {
        if (!TargetFrameworkCompat.IsWindows())
        {
            return;
        }

        // netstandard2.0 has no NativeLibrary resolver hook. Preloading the exact
        // packaged DLL path lets subsequent DllImport calls bind through Windows.
        foreach (var candidate in EnumerateNativeLibraryCandidates(assembly))
        {
            if (File.Exists(candidate) && LoadLibrary(candidate) != IntPtr.Zero)
            {
                return;
            }
        }
    }
#endif

    private static IEnumerable<string> EnumerateNativeLibraryCandidates(Assembly assembly)
    {
        var fileName = GetPlatformLibraryFileName();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddRoot(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                roots.Add(path!);
            }
        }

        AddRoot(AppContext.BaseDirectory);
        AddRoot(Path.GetDirectoryName(assembly.Location));
        AddRoot(Environment.CurrentDirectory);

        // Useful for tests and host applications that extract native assets outside
        // the standard NuGet output layout.
        var environmentHint = Environment.GetEnvironmentVariable("PARQUET_RS_FOR_DOTNET_NATIVE_DIR");
        AddRoot(environmentHint);

        foreach (var root in roots.ToArray())
        {
            AddRoot(Path.Combine(root, "native"));
        }

        foreach (var root in roots)
        {
            yield return Path.Combine(root, fileName);
        }

        foreach (var root in roots)
        {
            var current = new DirectoryInfo(root);
            for (var depth = 0; current is not null && depth < 8; depth++, current = current.Parent)
            {
                yield return Path.Combine(current.FullName, "native", "target", "debug", fileName);
                yield return Path.Combine(current.FullName, "native", "target", "release", fileName);
                yield return Path.Combine(current.FullName, "src", "bin", "Debug", "net8.0", "native", fileName);
                yield return Path.Combine(current.FullName, "src", "bin", "Release", "net8.0", "native", fileName);
                yield return Path.Combine(current.FullName, "src", "bin", "Debug", "netstandard2.0", "native", fileName);
                yield return Path.Combine(current.FullName, "src", "bin", "Release", "netstandard2.0", "native", fileName);
            }
        }
    }

    private static string GetPlatformLibraryFileName()
    {
        if (TargetFrameworkCompat.IsWindows())
        {
            return NativeLibraryBaseName + ".dll";
        }

        if (TargetFrameworkCompat.IsMacOS())
        {
            return "lib" + NativeLibraryBaseName + ".dylib";
        }

        return "lib" + NativeLibraryBaseName + ".so";
    }

    /// <summary>
    /// Converts a native error payload into a managed exception.
    /// </summary>
    /// <param name="code">The native error code.</param>
    /// <param name="message">The native message pointer.</param>
    /// <returns>The managed exception to throw.</returns>
    private static NativeParquetException CreateException(int code, IntPtr message)
    {
        var text = message == IntPtr.Zero
            ? "Native parquet writer failed."
            : TargetFrameworkCompat.PtrToStringUtf8(message) ?? "Native parquet writer failed.";

        if (message != IntPtr.Zero)
        {
            // Error messages are allocated by Rust and must be released through the matching
            // native helper to avoid leaking on repeated failures.
            parquet_free_string(message);
        }

        return new NativeParquetException((NativeErrorCode)code, text);
    }

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parquet_free_string(IntPtr value);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_file_writer_create(
        Apache.Arrow.C.CArrowSchema* schema,
        ParquetWriteOptionsNative* options,
        ParquetOutputSink* sink,
        IntPtr* writer,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_file_writer_write_batch(
        IntPtr writer,
        Apache.Arrow.C.CArrowArray* batch,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_file_writer_finish(
        IntPtr writer,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parquet_file_writer_dispose(IntPtr writer);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_file_reader_open(
        ParquetInputSource* source,
        IntPtr* reader,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_file_reader_get_schema(
        IntPtr reader,
        CArrowSchema* schema,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_file_reader_get_row_group_count(
        IntPtr reader,
        int* rowGroupCount,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_file_reader_open_row_group(
        IntPtr reader,
        int rowGroupIndex,
        IntPtr* rowGroupReader,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_row_group_reader_get_row_count(
        IntPtr rowGroupReader,
        long* rowCount,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_row_group_reader_read_column(
        IntPtr rowGroupReader,
        IntPtr columnName,
        CArrowArray* array,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_row_group_column_batch_reader_open(
        IntPtr rowGroupReader,
        IntPtr columnName,
        int batchSize,
        IntPtr* batchReader,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern int parquet_row_group_column_batch_reader_next(
        IntPtr batchReader,
        CArrowArray* array,
        int* hasBatch,
        NativeError* error);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parquet_row_group_column_batch_reader_dispose(IntPtr batchReader);

#if !NET8_0_OR_GREATER
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string fileName);
#endif

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parquet_row_group_reader_dispose(IntPtr rowGroupReader);

    [DllImport("parquet_rs_for_dotnet", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parquet_file_reader_dispose(IntPtr reader);

    internal static NativeOptionScope CreateNativeOptionScope(ParquetWriteOptions options)
    {
        return NativeOptionScope.Create(options);
    }

    /// <summary>
    /// Owns unmanaged strings and metadata buffers for a single native write invocation.
    /// </summary>
    internal sealed class NativeOptionScope : IDisposable
    {
        private readonly List<IntPtr> allocated = [];

        private NativeOptionScope(ParquetWriteOptionsNative options)
        {
            Options = options;
        }

        internal ParquetWriteOptionsNative Options;

        public static NativeOptionScope Create(ParquetWriteOptions options)
        {
            TargetFrameworkCompat.ThrowIfNull(options);

            var scope = new NativeOptionScope(new ParquetWriteOptionsNative
            {
                Compression = (int)options.Compression,
                EnableDictionaryEncoding = options.EnableDictionaryEncoding ? 1 : 0,
                StatisticsLevel = (int)options.StatisticsLevel,
                NativeWriteBatchSize = options.NativeWriteBatchSize ?? -1,
                MaxRowGroupRows = options.MaxRowGroupRows ?? -1,
                MaxRowGroupBytes = options.MaxRowGroupBytes ?? -1,
            });

            scope.Options.CreatedBy = scope.AllocUtf8(options.CreatedBy);

            if (options.FileMetadata is { Count: > 0 } metadata)
            {
                var pairs = new NativeKeyValuePair[metadata.Count];
                var index = 0;
                foreach (var pair in metadata)
                {
                    pairs[index++] = new NativeKeyValuePair
                    {
                        Key = scope.AllocUtf8(pair.Key),
                        Value = scope.AllocUtf8(pair.Value),
                    };
                }

                var size = Marshal.SizeOf<NativeKeyValuePair>() * pairs.Length;
                var block = Marshal.AllocCoTaskMem(size);
                scope.allocated.Add(block);
                for (var i = 0; i < pairs.Length; i++)
                {
                    Marshal.StructureToPtr(pairs[i], block + i * Marshal.SizeOf<NativeKeyValuePair>(), fDeleteOld: false);
                }

                scope.Options.Metadata = block;
                scope.Options.MetadataCount = pairs.Length;
            }

            return scope;
        }

        public void Dispose()
        {
            foreach (var pointer in allocated)
            {
                Marshal.FreeCoTaskMem(pointer);
            }

            allocated.Clear();
        }

        private IntPtr AllocUtf8(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return IntPtr.Zero;
            }

            var pointer = TargetFrameworkCompat.StringToCoTaskMemUtf8(value);
            allocated.Add(pointer);
            return pointer;
        }
    }
}
