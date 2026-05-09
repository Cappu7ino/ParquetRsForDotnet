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
    /// Gets the optional parquet-rs internal write batch size, or <see langword="null"/> to use the library default.
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
}
