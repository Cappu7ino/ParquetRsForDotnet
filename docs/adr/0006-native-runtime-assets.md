# ADR 0006: Native Runtime Asset Packaging

## Context

The managed assembly requires a native Rust library at runtime.

## Problem

Consumers need native binaries resolved automatically by standard NuGet runtime asset behavior.

## Decision

Pack native libraries under `runtimes/<rid>/native/`.

## Rationale

- Matches NuGet conventions.
- Lets SDK-style projects copy or resolve native assets normally.
- Supports separate Windows and Linux binaries.

## Consequences

- Package generation must build or include native artifacts.
- Linux asset requires opt-in cross-build on Windows.
- Unsupported RIDs require future packaging work.
