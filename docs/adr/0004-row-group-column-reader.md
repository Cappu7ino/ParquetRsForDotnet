# ADR 0004: Row-Group Column Reader

## Context

Parquet is organized around row groups and columns.

## Problem

High-level row readers can hide expensive full-file or full-row-group reads.

## Decision

Expose `ParquetFileReader.OpenRowGroupReader(...)` and column-level `ReadColumn(...)` APIs.

## Rationale

- Makes projection explicit.
- Avoids whole-file buffering.
- Aligns public API with parquet layout.

## Consequences

- Consumers must orchestrate row-group iteration.
- There is no row-object reader in v1.
- Agents should generate projection-aware reads.
