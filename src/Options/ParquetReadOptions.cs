namespace ParquetRsForDotnet;

/// <summary>
/// Configures parquet read behavior.
/// </summary>
public sealed class ParquetReadOptions
{
    /// <summary>
    /// Gets the maximum number of rows returned by each column batch when using batched read APIs.
    /// When unset, batched read APIs use the row-group row count and therefore return one batch per row-group column.
    /// </summary>
    public int? BatchSize { get; init; }

    internal void Validate()
    {
        if (BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "Read batch size must be greater than zero when specified.");
        }
    }
}
