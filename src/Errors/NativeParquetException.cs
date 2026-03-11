namespace ParquetRsForDotnet;

/// <summary>
/// Represents a managed exception projected from the native parquet writer when the Rust layer or
/// sink bridge reports a stable failure.
/// </summary>
public sealed class NativeParquetException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativeParquetException"/> class.
    /// </summary>
    /// <param name="errorCode">The stable native error code.</param>
    /// <param name="message">The error message returned from the native layer.</param>
    public NativeParquetException(NativeErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public NativeErrorCode ErrorCode { get; }
}
