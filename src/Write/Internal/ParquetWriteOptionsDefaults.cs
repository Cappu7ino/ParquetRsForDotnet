namespace ParquetRsForDotnet.Internal;

internal static class ParquetWriteOptionsDefaults
{
    public static ParquetWriteOptions ApplyForBatchWriter(ParquetWriteOptions options)
    {
        TargetFrameworkCompat.ThrowIfNull(options);

        if (options.NativeWriteBatchSize is not null)
        {
            return options;
        }

        return new ParquetWriteOptions
        {
            TargetBatchRows = options.TargetBatchRows,
            TargetBatchBytes = options.TargetBatchBytes,
            MaxRowGroupRows = options.MaxRowGroupRows,
            MaxRowGroupBytes = options.MaxRowGroupBytes,
            Compression = options.Compression,
            EnableDictionaryEncoding = options.EnableDictionaryEncoding,
            StatisticsLevel = options.StatisticsLevel,
            ArrowMaterializationMode = options.ArrowMaterializationMode,
            NativeWriteBatchSize = 8192,
            CreatedBy = options.CreatedBy,
            FileMetadata = options.FileMetadata,
        };
    }
}
