namespace ParquetRsForDotnet.Internal.Materialization;

internal sealed class ArrowMaterializationOptions
{
    public ArrowMaterializationMode Mode { get; init; } = ArrowMaterializationMode.Default;

    public bool UseLowLevelFixedWidth => Mode == ArrowMaterializationMode.LowLevelFixedWidth;
}
