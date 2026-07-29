# ADR-0001 — Architecture Freeze v1.0

## Status

Accepted

## Date

2026-07-29

## Context

The repository has completed the inventory, capability, decision, execution-plan, and Collector Runtime foundations. Stable boundaries are required before integration.

## Decision

Freeze v1.0 as Inventory Pipeline → Capability Engine → Collector Decision Engine → Execution Plan Engine → Execution Dispatcher → Collector Runtime → explicitly registered Collector Handler boundary → mutable Execution State → future Monitoring → future History. The Execution Plan and resolved policy/context are immutable; Execution State is separate and mutable. Dispatcher owns handler and policy resolution. Runtime owns execution, dependencies, throttling, timeout, cancellation, retry, and state. Registration is explicit; reflection scanning and dynamic plugin loading are prohibited. Repository-complete behavior remains target read-only. New layers require an accepted Architecture Exception ADR. Real integration remains deferred to WP-007.Z.

## Consequences

Work extends these responsibilities instead of replacing them. Generic frameworks and responsibility leakage are rejected.

## Security Impact

The Windows and SQL Collector boundaries remain separate. No target-side mutation or weaker authentication is introduced.

## Migration/Compatibility Impact

Schema changes remain forward-only. WP-008.5 refines ownership without changing persisted Execution Plan semantics.

## Alternatives Considered

Reflection-discovered plugins, mutable plans, plan-owned retry, and an additional orchestration layer were rejected.

## Related Documents

- [Architecture Freeze](../docs/architecture/Architecture-Freeze-v1.0.md)
- [Execution Plan](../docs/architecture/Execution-Plan.md)
- [Collector Runtime](../docs/architecture/Collector-Runtime.md)

## Supersession Rules

Only a later accepted ADR explicitly superseding ADR-0001 may change this freeze.
