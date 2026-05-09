# How To Handle Errors

## Native Errors

Catch `NativeParquetException` around file-level operations:

```csharp
try
{
    writer.WriteBatch(columns);
    writer.Finish();
}
catch (NativeParquetException ex) when (ex.ErrorCode == NativeErrorCode.SchemaMismatch)
{
    // Translate to an application-level schema validation failure.
}
```

## Common Error Sources

- `SchemaMismatch`: wrong column order, count, or type.
- `SinkWriteFailed`: destination stream failed.
- `SourceReadFailed`: input stream read/seek failed.
- `ParquetEncodeFailed`: native parquet writer rejected data or failed during encode.

## Recommended Pattern

- Validate schema and batch lengths before calling SDK APIs when possible.
- Call `Finish()` explicitly to observe final write failures.
- Keep exception translation near the application boundary.
