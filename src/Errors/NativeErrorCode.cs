namespace ParquetRsForDotnet;

/// <summary>
/// Represents stable error codes returned from the native parquet writer.
/// </summary>
public enum NativeErrorCode
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The caller supplied invalid input or configuration.
    /// </summary>
    InvalidArgument = 1,

    /// <summary>
    /// The source type or schema is not supported.
    /// </summary>
    UnsupportedType = 2,

    /// <summary>
    /// The runtime batch shape does not match the locked schema.
    /// </summary>
    SchemaMismatch = 3,

    /// <summary>
    /// The managed side failed while exporting Arrow data.
    /// </summary>
    ArrowExportFailed = 4,

    /// <summary>
    /// The native side failed while importing Arrow data.
    /// </summary>
    ArrowImportFailed = 5,

    /// <summary>
    /// The managed output sink failed while accepting parquet bytes.
    /// </summary>
    SinkWriteFailed = 6,

    /// <summary>
    /// The native parquet writer failed while encoding parquet data.
    /// </summary>
    ParquetEncodeFailed = 7,

    /// <summary>
    /// The native writer trapped an unexpected panic.
    /// </summary>
    InternalPanic = 8,

    /// <summary>
    /// The managed runtime could not locate the native parquet library.
    /// </summary>
    NativeLibraryNotFound = 9,
}
