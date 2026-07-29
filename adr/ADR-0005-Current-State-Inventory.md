# ADR-0005 — Current-State Inventory

## Status

Accepted

## Date

2026-07-29

## Context

WP-007 requires one coherent authoritative inventory without prematurely introducing history semantics.

## Decision

Persist current state, not history. Collection completes before the database transaction. A successful complete-core collection atomically replaces current state in one transaction; failure preserves the previous valid state. `InventoryRunId` supplies provenance and `InventoryVersion` increments only after a successful complete commit. Each module owns valid-empty semantics. Historical diff and retention require separately approved future scope.

## Consequences

Readers see either the previous complete state or the new complete state, never a partial merge.

## Security Impact

Database transactions do not remain open during target collection, reducing lock duration and preventing target sessions from crossing persistence boundaries.

## Migration/Compatibility Impact

Current-state keys and provenance remain authoritative; no history tables are introduced by this decision.

## Alternatives Considered

Append-only history, partial module commits, and transactions spanning remote collection were rejected.

## Related Documents

- [Inventory Pipeline](../docs/architecture/Inventory-Pipeline.md)
- [Inventory Module Contract](../docs/contracts/Inventory-Module-Contract.md)

## Supersession Rules

History or changed replacement semantics require a later accepted ADR explicitly superseding ADR-0005.
