# Examples

These examples are small console projects that demonstrate canonical consumption patterns.

| Example | Purpose |
| --- | --- |
| `WriteClrBatches` | Write a parquet file from managed column arrays. |
| `WriteArrowArrays` | Write a parquet file from Apache.Arrow arrays. |
| `ReadSelectedColumns` | Read selected row-group columns as CLR arrays. |

Run an example with:

```powershell
dotnet run --project examples/WriteClrBatches/WriteClrBatches.csproj
```
