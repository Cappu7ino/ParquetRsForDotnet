namespace ParquetRsForDotnet.Internal;

internal static class ParquetWriteOptionsDefaults
{
    public static ParquetWriteOptions ApplyForBatchWriter(ParquetWriteOptions options)
    {
        TargetFrameworkCompat.ThrowIfNull(options);
        options.Validate();

        if (options.NativeWriteBatchSize is not null)
        {
            return options;
        }

        return new ParquetWriteOptions
        {
            MaxRowGroupRows = options.MaxRowGroupRows,
            MaxRowGroupBytes = options.MaxRowGroupBytes,
            DataPageRowCountLimit = options.DataPageRowCountLimit,
            DataPageSizeLimitBytes = options.DataPageSizeLimitBytes,
            DictionaryPageSizeLimitBytes = options.DictionaryPageSizeLimitBytes,
            MaxNativeWriterMemoryBytes = options.MaxNativeWriterMemoryBytes,
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
