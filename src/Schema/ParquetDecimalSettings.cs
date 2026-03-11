namespace ParquetRsForDotnet;

/// <summary>
/// Describes the precision and scale for a decimal column in the public schema API.
/// </summary>
public readonly record struct ParquetDecimalSettings
{
    /// <summary>
    /// Initializes decimal settings.
    /// </summary>
    /// <param name="precision">The decimal precision in the range 1..38.</param>
    /// <param name="scale">The decimal scale in the range 0..precision.</param>
    public ParquetDecimalSettings(int precision, int scale)
    {
        if (precision < 1 || precision > 38)
        {
            throw new ArgumentOutOfRangeException(nameof(precision), precision, "Decimal precision must be in the range 1..38.");
        }

        if (scale < 0 || scale > precision)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Decimal scale must be in the range 0..precision.");
        }

        Precision = precision;
        Scale = scale;
    }

    /// <summary>
    /// Gets the decimal precision.
    /// </summary>
    public int Precision { get; }

    /// <summary>
    /// Gets the decimal scale.
    /// </summary>
    public int Scale { get; }
}
