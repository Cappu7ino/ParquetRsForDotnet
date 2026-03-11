namespace ParquetRsForDotnet;

/// <summary>
/// Controls the level of parquet statistics written by the native writer.
/// </summary>
public enum ParquetStatisticsLevel
{
    /// <summary>
    /// Disables parquet statistics.
    /// </summary>
    None = 0,

    /// <summary>
    /// Writes row-group or column-chunk level statistics only.
    /// </summary>
    Chunk = 1,

    /// <summary>
    /// Writes page-level and row-group level statistics.
    /// </summary>
    Page = 2,
}
