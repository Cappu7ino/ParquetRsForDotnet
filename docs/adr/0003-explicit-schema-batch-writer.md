# ADR 0003: Explicit Schema Batch Writer

## Context

The writer accepts CLR arrays and Arrow arrays.

## Problem

Schema inference can produce surprising parquet logical types, especially for decimals, timestamps, dates, GUIDs, and nullability.

## Decision

Require an explicit `ParquetSchema` at writer construction.

## Rationale

- Makes file contract clear before data is written.
- Keeps validation deterministic.
- Avoids accidental logical type changes across batches.

## Consequences

- Column order is part of the contract.
- Callers must provide all columns for every batch.
- Integrators need type mapping guidance.
