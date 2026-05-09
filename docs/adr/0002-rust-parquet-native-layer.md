# ADR 0002: Rust Parquet Native Layer

## Context

The SDK needs parquet read/write support with strong Arrow integration.

## Problem

Implementing parquet encoding and decoding fully in managed code would duplicate mature native functionality and increase maintenance cost.

## Decision

Use Rust `parquet-rs` and `arrow-rs` for parquet encoding, decoding, metadata, and projection.

## Rationale

- Reuses mature parquet/Arrow implementation.
- Keeps managed API small.
- Uses Arrow C Data for efficient boundary crossing.

## Consequences

- NuGet package must include native runtime assets.
- Native loading must be tested on supported RIDs.
- Interop ownership rules are critical.
