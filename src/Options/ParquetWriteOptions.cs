namespace ParquetRsForDotnet;

/// <summary>
/// Configures batching and parquet writer behavior for a managed write operation.
/// </summary>
public sealed class ParquetWriteOptions
{
    /// <summary>
    /// Gets the optional maximum number of rows per parquet row group.
    /// </summary>
    public int? MaxRowGroupRows { get; init; }

    /// <summary>
    /// Gets the optional maximum estimated size in bytes per parquet row group.
    /// </summary>
    public long? MaxRowGroupBytes { get; init; }

    /// <summary>
    /// Gets the optional best-effort maximum number of rows per parquet data page.
    /// Smaller pages can reduce page-level buffering, but may increase metadata overhead.
    /// </summary>
    public int? DataPageRowCountLimit { get; init; }

    /// <summary>
    /// Gets the optional best-effort maximum parquet data page size in bytes.
    /// Smaller pages can reduce peak native buffers, but may increase file size.
    /// </summary>
    public int? DataPageSizeLimitBytes { get; init; }

    /// <summary>
    /// Gets the optional best-effort maximum dictionary page size in bytes.
    /// Smaller dictionary pages can reduce memory for high-cardinality columns, but may reduce compression efficiency.
    /// </summary>
    public int? DictionaryPageSizeLimitBytes { get; init; }

    /// <summary>
    /// Gets the optional native writer memory threshold in bytes that triggers an early row-group flush.
    /// This threshold is checked after each managed batch write and is not a hard process memory limit.
    /// </summary>
    public long? MaxNativeWriterMemoryBytes { get; init; }

    /// <summary>
    /// Gets the default parquet compression codec.
    /// </summary>
    public ParquetCompression Compression { get; init; } = ParquetCompression.Zstd;

    /// <summary>
    /// Gets a value indicating whether dictionary encoding is enabled by default.
    /// </summary>
    public bool EnableDictionaryEncoding { get; init; } = true;

    /// <summary>
    /// Gets the parquet statistics level to write into the file metadata.
    /// </summary>
    public ParquetStatisticsLevel StatisticsLevel { get; init; } = ParquetStatisticsLevel.Chunk;

    /// <summary>
    /// Gets the Arrow array materialization mode used for CLR array-backed columnar inputs.
    /// </summary>
    public ArrowMaterializationMode ArrowMaterializationMode { get; init; } = ArrowMaterializationMode.Default;

    /// <summary>
    /// Gets the optional parquet-rs encoder chunk size, or <see langword="null"/> to use the library default.
    /// This does not split managed write batches or control parquet row-group boundaries.
    /// </summary>
    public int? NativeWriteBatchSize { get; init; }

    /// <summary>
    /// Gets the optional created-by string written into parquet metadata.
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// Gets the optional file-level key/value metadata written into the parquet file.
    /// </summary>
    public IReadOnlyDictionary<string, string>? FileMetadata { get; init; }

    internal void Validate()
    {
        ThrowIfNotPositive(DataPageRowCountLimit, nameof(DataPageRowCountLimit));
        ThrowIfNotPositive(DataPageSizeLimitBytes, nameof(DataPageSizeLimitBytes));
        ThrowIfNotPositive(DictionaryPageSizeLimitBytes, nameof(DictionaryPageSizeLimitBytes));
        ThrowIfNotPositive(MaxNativeWriterMemoryBytes, nameof(MaxNativeWriterMemoryBytes));
    }

    private static void ThrowIfNotPositive(long? value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Write memory tuning options must be greater than zero when specified.");
        }
    }
}
