namespace ParquetRsForDotnet;

/// <summary>
/// Represents the supported logical column kinds for the public schema API.
/// </summary>
public enum ParquetColumnType
{
    /// <summary>
    /// Boolean values.
    /// </summary>
    Boolean = 0,

    /// <summary>
    /// Signed 8-bit integer values.
    /// </summary>
    Int8 = 1,

    /// <summary>
    /// Unsigned 8-bit integer values.
    /// </summary>
    UInt8 = 2,

    /// <summary>
    /// Signed 16-bit integer values.
    /// </summary>
    Int16 = 3,

    /// <summary>
    /// Unsigned 16-bit integer values.
    /// </summary>
    UInt16 = 4,

    /// <summary>
    /// Signed 32-bit integer values.
    /// </summary>
    Int32 = 5,

    /// <summary>
    /// Unsigned 32-bit integer values.
    /// </summary>
    UInt32 = 6,

    /// <summary>
    /// Signed 64-bit integer values.
    /// </summary>
    Int64 = 7,

    /// <summary>
    /// Unsigned 64-bit integer values.
    /// </summary>
    UInt64 = 8,

    /// <summary>
    /// Single-precision floating-point values.
    /// </summary>
    Float32 = 9,

    /// <summary>
    /// Double-precision floating-point values.
    /// </summary>
    Float64 = 10,

    /// <summary>
    /// UTF-8 string values.
    /// </summary>
    String = 11,

    /// <summary>
    /// Variable-length binary values.
    /// </summary>
    Binary = 12,

    /// <summary>
    /// GUID values encoded as fixed-size binary.
    /// </summary>
    Guid = 13,

    /// <summary>
    /// Date values encoded as Arrow date32.
    /// </summary>
    Date32 = 14,

    /// <summary>
    /// Date values encoded as Arrow date64.
    /// </summary>
    Date64 = 15,

    /// <summary>
    /// Timestamp values configured with <see cref="ParquetTimestampSettings"/>.
    /// </summary>
    Timestamp = 16,

    /// <summary>
    /// Decimal values configured with <see cref="ParquetDecimalSettings"/>.
    /// </summary>
    Decimal128 = 17,
}
