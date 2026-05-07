namespace ParquetRsForDotnet;

/// <summary>
/// Represents the ordered public schema used by <c>ParquetFileWriter</c> and other batch-based APIs.
/// </summary>
public sealed class ParquetSchema
{
    /// <summary>
    /// Initializes a new schema from the ordered set of top-level columns.
    /// </summary>
    /// <param name="columns">The top-level columns in strict write order.</param>
    public ParquetSchema(IEnumerable<ParquetColumn> columns)
    {
        TargetFrameworkCompat.ThrowIfNull(columns);

        var materialized = columns.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("Schema must contain at least one column.", nameof(columns));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in materialized)
        {
            TargetFrameworkCompat.ThrowIfNull(column);

            if (!names.Add(column.Name))
            {
                throw new ArgumentException($"Schema contains duplicate column name '{column.Name}'.", nameof(columns));
            }
        }

        Columns = Array.AsReadOnly(materialized);
    }

    /// <summary>
    /// Gets the ordered top-level columns for the schema.
    /// </summary>
    public IReadOnlyList<ParquetColumn> Columns { get; }
}
