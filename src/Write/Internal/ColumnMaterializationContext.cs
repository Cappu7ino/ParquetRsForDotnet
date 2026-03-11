using Apache.Arrow.Types;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Carries the schema and runtime information needed to materialize a single column vector.
/// </summary>
/// <param name="ColumnName">The logical column name.</param>
/// <param name="ClrType">The resolved CLR carrier type for the vector.</param>
/// <param name="ArrowType">The Arrow type locked by the mapped schema.</param>
/// <param name="RowCount">The number of rows represented by the current batch.</param>
/// <param name="IsNullable">Indicates whether the schema allows nulls for the column.</param>
internal readonly record struct ColumnMaterializationContext(
    string ColumnName,
    Type ClrType,
    IArrowType ArrowType,
    int RowCount,
    bool IsNullable);
