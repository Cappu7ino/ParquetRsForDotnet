# Common Integration Patterns

## Write A Parquet File From CLR Arrays

1. Define `ParquetSchema` explicitly.
2. Open a writable stream.
3. Create `ParquetFileWriter`.
4. Write full-width column batches.
5. Call `Finish()`.

Use this when the consuming app already has columnar arrays or can cheaply group rows into arrays.

## Write A Parquet File From Arrow Arrays

1. Build Arrow arrays with Apache.Arrow builders or existing Arrow buffers.
2. Define matching `ParquetSchema`.
3. Pass `IArrowArray` columns to `WriteBatch(...)`.

Use this when the consuming app already operates on Arrow data.

## Read Selected Columns

1. Open a seekable stream.
2. Create `ParquetFileReader`.
3. Iterate row groups by index.
4. Read only required columns by name or index.

Use this for projection-friendly pipelines.

## Dependency Injection Wrapper

Prefer an application-specific wrapper over injecting SDK objects directly:

```csharp
public interface IParquetExportService
{
    void WriteOrders(Stream output, OrderColumnBatch batch);
}
```

The wrapper should own schema construction, batch ordering, and error translation.

## Async Application Boundary

The SDK is synchronous. In async applications, keep async I/O outside the SDK boundary or offload complete synchronous work units intentionally. Do not invent `WriteBatchAsync` wrappers that imply native async behavior.
