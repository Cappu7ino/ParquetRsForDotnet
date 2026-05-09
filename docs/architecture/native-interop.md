# Native Interop

## Boundary

- Managed .NET calls Rust through P/Invoke in `NativeParquetBridge`.
- Arrow schemas, record batches, and arrays cross through Arrow C Data.
- Managed streams cross the boundary as callback tables, not as raw file handles.

## Write Callbacks

- `ManagedParquetSink` adapts a writable `Stream` to native write/flush/close callbacks.
- Native Rust writes encoded parquet bytes through these callbacks.
- Managed stream failures are captured and projected as native error codes.

## Read Callbacks

- `ManagedParquetSource` adapts a seekable `Stream` to native random-access reads.
- Native Rust asks for byte ranges at arbitrary offsets.
- Non-seekable streams are rejected before native reader construction.

## Target Framework Differences

- `net8.0` uses `NativeLibrary.SetDllImportResolver`, function pointers, and `UnmanagedCallersOnly`.
- `netstandard2.0` uses delegate-backed callbacks and Windows native DLL preloading.
- Delegate instances must remain rooted for as long as native code can invoke them.

## Native Assets

- NuGet runtime asset path: `runtimes/<rid>/native/`.
- Windows file name: `parquet_rs_for_dotnet.dll`.
- Linux file name: `libparquet_rs_for_dotnet.so`.
