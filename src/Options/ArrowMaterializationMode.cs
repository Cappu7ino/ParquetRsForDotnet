namespace ParquetRsForDotnet;

/// <summary>
/// Controls how CLR array-backed columns are materialized into Arrow arrays before they are streamed
/// to the native writer.
/// </summary>
public enum ArrowMaterializationMode
{
    /// <summary>
    /// Uses the library's default Arrow array materialization behavior.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Uses only Apache.Arrow builder-based array materialization.
    /// </summary>
    BuilderOnly = 1,

    /// <summary>
    /// Enables low-level fixed-width array materialization for supported non-null CLR arrays.
    /// </summary>
    LowLevelFixedWidth = 2,
}
