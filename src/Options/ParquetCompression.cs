namespace ParquetRsForDotnet;

/// <summary>
/// Represents the parquet compression codecs supported by the V1 writer surface.
/// </summary>
public enum ParquetCompression
{
    /// <summary>
    /// Writes parquet data without compression.
    /// </summary>
    Uncompressed = 0,

    /// <summary>
    /// Uses Snappy compression.
    /// </summary>
    Snappy = 1,

    /// <summary>
    /// Uses GZip compression.
    /// </summary>
    Gzip = 2,

    /// <summary>
    /// Uses raw LZ4 compression.
    /// </summary>
    Lz4 = 3,

    /// <summary>
    /// Uses Brotli compression.
    /// </summary>
    Brotli = 4,

    /// <summary>
    /// Uses Zstandard compression.
    /// </summary>
    Zstd = 5,
}
