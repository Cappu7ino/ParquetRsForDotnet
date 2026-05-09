# Target Frameworks

## Supported Assets

- `net8.0`: modern .NET implementation path.
- `netstandard2.0`: compatibility asset for .NET Standard consumers and .NET Framework 4.7.2.

## Behavioral Differences

- Date columns materialize as `DateOnly` on `net8.0`.
- Date columns materialize as `DateTime` on `netstandard2.0`.
- Native callback implementation differs, but public read/write behavior should remain equivalent outside date CLR type shape.

## Test Coverage

- The xUnit test project targets `net8.0` and `net472`.
- `net472` exercises the `netstandard2.0` library asset.
- ParquetSharp and Parquet.Net are used by both target legs for compatibility coverage.
