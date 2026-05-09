# Type System

## Public Schema Mapping

The public schema intentionally exposes a small set of top-level logical column types. It does not expose the full Arrow or parquet type systems.

| Public type | Arrow/parquet shape | Notes |
| --- | --- | --- |
| `Boolean` | Boolean | nullable supported |
| integer types | matching Arrow integer | signed and unsigned supported |
| `Float32` / `Float64` | floating point | nullable supported |
| `String` | UTF-8 | nullable strings may return nulls in `string[]` |
| `Binary` | variable binary | CLR shape is `byte[][]` |
| `Guid` | fixed-size binary 16 / UUID where imported | ambiguity can exist when external files use fixed-size binary |
| `Date32` / `Date64` | date logical types | CLR shape differs by target framework |
| `Timestamp` | Arrow timestamp | configured with `ParquetTimestampSettings` |
| `Decimal128` | Arrow decimal128 | configured with `ParquetDecimalSettings` |

## Target Framework Date Shape

- `net8.0`: `DateOnly` / `DateOnly?`.
- `netstandard2.0`: `DateTime` / `DateTime?`.

## Decimal Shape

- Write inputs use `decimal` / `decimal?`.
- CLR read outputs use `SqlDecimal` / `SqlDecimal?` to preserve Arrow decimal precision behavior.
