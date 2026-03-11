namespace ParquetRsForDotnet;

/// <summary>
/// Represents the supported timestamp resolutions for public schema definitions.
/// </summary>
public enum ParquetTimestampUnit
{
    /// <summary>
    /// Seconds.
    /// </summary>
    Second = 0,

    /// <summary>
    /// Milliseconds.
    /// </summary>
    Millisecond = 1,

    /// <summary>
    /// Microseconds.
    /// </summary>
    Microsecond = 2,

    /// <summary>
    /// Nanoseconds.
    /// </summary>
    Nanosecond = 3,
}
