# ADR 0005: netstandard2.0 Compatibility

## Context

The package supports `net8.0` and `netstandard2.0` to reach modern .NET and .NET Framework consumers.

## Problem

Some modern APIs and types are unavailable on `netstandard2.0`, including `DateOnly`, `NativeLibrary` resolver hooks, and unmanaged-callers-only callbacks.

## Decision

Keep the `net8.0` fast path and add compatibility paths for `netstandard2.0`.

## Rationale

- Maintains modern performance-oriented interop on `net8.0`.
- Enables .NET Framework 4.7.2 consumers through the `netstandard2.0` asset.
- Keeps one public package.

## Consequences

- Date columns use `DateTime` on `netstandard2.0`.
- Delegate callback lifetimes must be rooted for native calls.
- Test project targets `net472` to exercise compatibility.
