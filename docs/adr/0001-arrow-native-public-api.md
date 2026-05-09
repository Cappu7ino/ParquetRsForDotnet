# ADR 0001: Arrow-Native Public API

## Context

The SDK bridges managed .NET and Rust parquet code. Both sides can exchange Arrow data efficiently through Arrow C Data.

## Problem

A row-object API would be convenient but would add schema inference, per-row allocations, and ambiguous type mapping.

## Decision

Expose Arrow-native and columnar batch APIs as the primary public surface.

## Rationale

- Preserves columnar data shape.
- Keeps interop with Rust efficient.
- Avoids implicit schema inference.
- Aligns reads with parquet row-group and column projection.

## Consequences

- Consumers must provide explicit schemas.
- Consumers must batch data into column arrays.
- AI agents must not invent row-object convenience APIs.
