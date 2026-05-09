# Limitations and Guardrails

This file exists to prevent AI coding agents from inventing unsupported patterns.

## Unsupported Scenarios

- No async read/write API.
- No schema inference API.
- No row-object writer or reader API.
- No append-to-existing-parquet-file API.
- No public write row-group API.
- No public nested/list/map schema model.
- No non-seekable read stream support.
- No documented thread-safe instance usage.

## Dangerous Usage Patterns

- Writing one row per `WriteBatch(...)`: causes avoidable allocations and poor throughput.
- Passing columns out of schema order: fails with schema mismatch.
- Relying on `Dispose()` instead of `Finish()`: write failures may surface during cleanup.
- Reading all row groups and all columns by default: defeats projection and can over-allocate.
- Reading decimal columns as `decimal`: CLR read API expects `SqlDecimal`.

## Memory Assumptions

- Public writes receive complete column arrays for one batch.
- CLR reads allocate a full managed array for the requested row-group column.
- Arrow reads return Arrow arrays for the requested row-group column.
- The read path should not introduce whole-file buffering.

## Concurrency Assumptions

- Treat writer, file reader, and row-group reader instances as not thread-safe.
- Use separate instances or external synchronization for concurrent workflows.
- Keep stream access coordinated; native callbacks seek and read the stream during reads.

## Deployment Assumptions

- Native runtime assets must be present for the current RID.
- Packaged assets currently target `win-x64` and `linux-x64` when cross-built.
- .NET Framework consumers use the `netstandard2.0` assembly and Windows native asset.

## Date and Decimal Assumptions

- `net8.0` date CLR materialization uses `DateOnly`.
- `netstandard2.0` date CLR materialization uses `DateTime`.
- Decimal writes use `decimal`; decimal CLR reads use `SqlDecimal`.
