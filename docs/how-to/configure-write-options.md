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

`NativeWriteBatchSize` maps to parquet-rs `set_write_batch_size`. It controls the row chunk size parquet-rs uses while encoding Arrow batches into parquet pages.

This is an advanced throughput and temporary native-memory tuning knob. It does not split managed `WriteBatch(...)` calls, does not set parquet data page size, and does not define row-group boundaries.

When unset, this library uses `8_192` rows. The upstream parquet-rs default is `1_024` rows.

Use it only after measuring native writer behavior. Smaller values can reduce some transient encoder working set, but may reduce throughput. Larger values can improve throughput for some workloads, but may increase temporary native memory pressure.

## Page and Memory Pressure Tuning

For string-heavy, dictionary-heavy, or deeply nested data, parquet-rs may hold non-trivial native buffers while encoding the current row group. These options trade compression and metadata efficiency for lower peak native memory:

```csharp
var options = new ParquetWriteOptions
{
    DataPageRowCountLimit = 8_192,
    DataPageSizeLimitBytes = 256 * 1024,
    DictionaryPageSizeLimitBytes = 256 * 1024,
    MaxNativeWriterMemoryBytes = 64 * 1024 * 1024,
};
```

`MaxNativeWriterMemoryBytes` is checked after each managed `WriteBatch(...)` call. If the active parquet row group has buffered rows and the estimated native writer memory exceeds the threshold, the writer flushes the row group early.

This is not a hard process memory cap. Very large managed batches can still create high transient memory usage before the threshold is checked, so keep caller-provided `WriteBatch(...)` sizes bounded when optimizing for peak memory.
