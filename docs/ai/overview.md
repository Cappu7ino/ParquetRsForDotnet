# AI Overview

## Purpose

`ParquetRsForDotnet` is a .NET SDK for Arrow-native parquet read/write workflows. It exposes a small managed API while delegating parquet encoding and decoding to Rust `parquet-rs` through Arrow C Data interop.

## Architectural Philosophy

- Prefer explicit schema over inference.
- Prefer columnar batches over row objects.
- Prefer Arrow-native data movement over redundant conversions.
- Keep managed/native boundaries narrow and ownership rules visible.
- Optimize for bounded memory and projected reads rather than whole-file buffering.

## Core Abstractions

| Abstraction | Role |
| --- | --- |
| `ParquetSchema` | Ordered public schema contract for writes and imported reader schema. |
| `ParquetColumn` | One top-level column with logical type and nullability. |
| `ParquetFileWriter` | Stateful synchronous writer for a new parquet file. |
| `ParquetFileReader` | Metadata reader over a seekable parquet stream. |
| `ParquetRowGroupReader` | Row-group scoped projected column reader. |
| `ParquetWriteOptions` | Writer configuration for row groups, compression, statistics, and metadata. |

## Dominant Execution Model

- Synchronous API surface.
- Caller-provided streams.
- Managed code validates schema and columns.
- Rust code encodes/decodes parquet.
- Reads are explicit: file -> row group -> column.
- Writes are explicit: schema -> one or more full-width column batches -> finish.

## Lifecycle Expectations

- Always dispose `ParquetFileWriter`, `ParquetFileReader`, and `ParquetRowGroupReader`.
- Prefer explicit `ParquetFileWriter.Finish()` before dispose so write failures are not hidden in cleanup.
- Keep input/output streams alive for the lifetime of the SDK object using them.
- Do not use SDK objects concurrently unless the consuming application provides external synchronization.

## Major Tradeoffs

- No row-object API: avoids allocations and schema ambiguity.
- No async API: simplifies native callback model and ownership.
- No schema inference: avoids accidental parquet type mismatches.
- `netstandard2.0` uses `DateTime` for date columns because `DateOnly` is unavailable.

## Glossary

- Arrow C Data: cross-language Arrow memory interchange format used at the managed/native boundary.
- CLR materialization: converting managed arrays into Arrow arrays or Arrow arrays back into managed arrays.
- Row group: parquet unit of scan/projection and the scope used by `ParquetRowGroupReader`.
- Projection: reading only selected row-group columns rather than all data.
- Sink callback: managed output stream adapter invoked by native Rust writer.
- Source callback: managed seek/read adapter invoked by native Rust reader.
