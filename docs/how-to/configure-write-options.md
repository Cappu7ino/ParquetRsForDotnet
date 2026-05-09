# How To Configure Write Options

## Row Group Sizing

Use row-group limits to control parquet file layout and memory behavior:

```csharp
var options = new ParquetWriteOptions
{
    MaxRowGroupRows = 100_000,
    MaxRowGroupBytes = 128 * 1024 * 1024,
};
```

## Compression and Statistics

```csharp
var options = new ParquetWriteOptions
{
    Compression = ParquetCompression.Zstd,
    EnableDictionaryEncoding = true,
    StatisticsLevel = ParquetStatisticsLevel.Chunk,
};
```

## Native Encoder Tuning

`NativeWriteBatchSize` maps to parquet-rs `set_write_batch_size`. It does not split managed `WriteBatch(...)` calls and does not define row-group boundaries.

Use it only after measuring native writer behavior.
