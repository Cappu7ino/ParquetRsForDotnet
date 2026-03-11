namespace ParquetRsForDotnet;

/// <summary>
/// Describes the resolution and optional timezone for a timestamp column in the public schema API.
/// </summary>
public readonly record struct ParquetTimestampSettings
{
    /// <summary>
    /// Initializes timestamp settings.
    /// </summary>
    /// <param name="unit">The timestamp resolution.</param>
    /// <param name="timezone">The optional timezone identifier.</param>
    public ParquetTimestampSettings(ParquetTimestampUnit unit, string? timezone = null)
    {
        if (timezone is not null && string.IsNullOrWhiteSpace(timezone))
        {
            throw new ArgumentException("Timezone cannot be empty or whitespace when provided.", nameof(timezone));
        }

        Unit = unit;
        Timezone = timezone;
    }

    /// <summary>
    /// Gets the timestamp resolution.
    /// </summary>
    public ParquetTimestampUnit Unit { get; }

    /// <summary>
    /// Gets the optional timezone identifier.
    /// </summary>
    public string? Timezone { get; }
}
