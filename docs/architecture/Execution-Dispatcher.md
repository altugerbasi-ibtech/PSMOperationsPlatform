# Execution Dispatcher

Dispatcher continues to resolve immutable plugin and policy provenance. It
does not persist, query, or control Execution History.

Dispatcher remains event-based and has no dependency on monitoring. Dispatch
rejections carry bounded reason/provenance that Monitoring may project without
changing compatibility or eligibility.

## Status

Implemented by WP-008.5 under Architecture Freeze v1.0.

## Boundary

The Dispatcher is the frozen resolution boundary between immutable planning and mutable execution. It validates one committed plan request, resolves `StrategyCode` by exact ordinal lookup in an explicitly populated registry, validates the immutable handler descriptor, resolves all plan policy references into one immutable `ExecutionPolicy`, validates plugin-policy compatibility, creates an immutable `ExecutionContext` and `PreparedExecutionDispatch`, and submits it to Collector Runtime.

Dispatcher resolves. Runtime executes. Handler collects.

The Dispatcher never calls handler business logic, performs retry or timeout enforcement, transitions Execution State, scans assemblies, or accesses targets. An incompatibility rejects the entire preparation before Runtime invocation; no policy is silently downgraded.

## Compatibility

All handlers must be read-only and cancellation-aware. Timeout requires timeout and cancellation support; retry with more than one attempt requires retry support; parallel and batching policies require their corresponding declared capability.

WP-008.6 adds descriptor normalization, Runtime–SDK matrix validation and deterministic plugin validation before policy-compatible work is submitted. Rejection diagnostics include safe plugin, SDK, Runtime and policy provenance and failed checks.

## Events

The Dispatcher emits requested, handler-resolved, policy-resolved, prepared, rejected, and submitted lifecycle events using independent `ExecutionEventSchemaVersion = 1`. Events are safe in-process notifications; state/result remains authoritative and exactly-once delivery is not claimed.

## Deferrals

Production collectors, a complete Plugin SDK, third-party loading, monitoring, history, artifact storage, and integration are deferred to WP-008.6, WP-008.7, WP-008.8, and WP-007.Z as applicable.

See [ADR-0001](../../adr/ADR-0001-Architecture-Freeze.md) and the [Dispatcher contract](../contracts/Execution-Dispatcher-Contract.md).
