# How To Use From .NET Framework

## Target

.NET Framework 4.7.2 consumes the package through its `netstandard2.0` asset.

## Date Columns

Use `DateTime` / `DateTime?` for `Date32` and `Date64` columns.

```csharp
var schema = new ParquetSchema(new[]
{
    new ParquetColumn("businessDate", ParquetColumnType.Date32, isNullable: true),
});

writer.WriteBatch(new DateTime?[] { new DateTime(2024, 1, 1), null });
```

## Native Asset

The Windows native DLL must be available from the NuGet runtime asset output. If native loading fails, inspect the build output for `parquet_rs_for_dotnet.dll`.
