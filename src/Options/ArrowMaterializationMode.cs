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
    /// Enables low-level fixed-width array materialization for supported non-nullable numeric CLR arrays.
    /// Temporal and nullable CLR arrays continue to use Apache.Arrow builder-based materialization.
    /// </summary>
    LowLevelFixedWidth = 2,
}
