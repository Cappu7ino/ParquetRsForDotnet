namespace ParquetRsForDotnet;

/// <summary>
/// Describes one top-level column in a <see cref="ParquetSchema"/>.
/// </summary>
public sealed class ParquetColumn
{
    /// <summary>
    /// Initializes a non-decimal, non-timestamp column.
    /// </summary>
    /// <param name="name">The logical column name.</param>
    /// <param name="columnType">The logical column type.</param>
    /// <param name="isNullable">Whether the column allows null values.</param>
    public ParquetColumn(string name, ParquetColumnType columnType, bool isNullable = false)
        : this(name, columnType, isNullable, decimalSettings: null, timestampSettings: null)
    {
        if (columnType is ParquetColumnType.Decimal128 or ParquetColumnType.Timestamp)
        {
            throw new ArgumentException($"Column type '{columnType}' requires the matching specialized settings constructor.", nameof(columnType));
        }
    }

    /// <summary>
    /// Initializes a decimal column.
    /// </summary>
    /// <param name="name">The logical column name.</param>
    /// <param name="decimalSettings">The decimal precision and scale.</param>
    /// <param name="isNullable">Whether the column allows null values.</param>
    public ParquetColumn(string name, ParquetDecimalSettings decimalSettings, bool isNullable = false)
        : this(name, ParquetColumnType.Decimal128, isNullable, decimalSettings, timestampSettings: null)
    {
    }

    /// <summary>
    /// Initializes a timestamp column.
    /// </summary>
    /// <param name="name">The logical column name.</param>
    /// <param name="timestampSettings">The timestamp resolution and optional timezone.</param>
    /// <param name="isNullable">Whether the column allows null values.</param>
    public ParquetColumn(string name, ParquetTimestampSettings timestampSettings, bool isNullable = false)
        : this(name, ParquetColumnType.Timestamp, isNullable, decimalSettings: null, timestampSettings)
    {
    }

    private ParquetColumn(
        string name,
        ParquetColumnType columnType,
        bool isNullable,
        ParquetDecimalSettings? decimalSettings,
        ParquetTimestampSettings? timestampSettings)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Column name cannot be empty or whitespace.", nameof(name));
        }

        Name = name;
        ColumnType = columnType;
        IsNullable = isNullable;
        DecimalSettings = decimalSettings;
        TimestampSettings = timestampSettings;
    }

    /// <summary>
    /// Gets the logical column name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the logical column type.
    /// </summary>
    public ParquetColumnType ColumnType { get; }

    /// <summary>
    /// Gets a value indicating whether the column allows null values.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets the decimal settings when <see cref="ColumnType"/> is <see cref="ParquetColumnType.Decimal128"/>.
    /// </summary>
    public ParquetDecimalSettings? DecimalSettings { get; }

    /// <summary>
    /// Gets the timestamp settings when <see cref="ColumnType"/> is <see cref="ParquetColumnType.Timestamp"/>.
    /// </summary>
    public ParquetTimestampSettings? TimestampSettings { get; }
}
