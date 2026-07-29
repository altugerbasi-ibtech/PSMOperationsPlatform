# ADR-0008 — Durable Execution History Projection

## Status

Accepted

## Date

2026-07-29

## Context

Execution State is mutable current authority and Monitoring is non-durable.
Completed execution facts require durable bounded queries without turning
lifecycle events into event sourcing or Audit.

## Decision

Add normalized, terminal-write-preferred Execution History inside the frozen
History boundary. Explicit typed mapping combines terminal state, prepared
plugin/policy provenance, typed events, and safe artifact metadata. Logical
uniqueness provides idempotency. Partial projection is explicit. History uses
bounded queries and repository-owned retention without production scheduling.

Runtime and Dispatcher do not depend on the Infrastructure writer. No outbox,
bus, broker, event sourcing, replay, generic repository, Unit of Work, JSON
event table, or Audit capability is introduced.

## Consequences

Historical queries survive current-state replacement, but History can lag and
process loss before projection can omit a terminal run. Exactly-once delivery
is unavailable. SQL constraints and short transactions handle duplicates.

## Security Impact

Only bounded operational metadata is durable. Raw artifacts, commands,
exceptions, secrets, authentication material, paths, and unapproved user
identity are excluded.

## Migration/Compatibility Impact

A forward-only migration adds six `history` schema tables. Startup migration
behavior is unchanged. Production schema and performance validation remain
WP-007.Z work.

## Alternatives Considered

Event sourcing, generic JSON events, outbox/broker delivery, current State,
Monitoring storage, and Audit were rejected as outside the frozen boundary.

## Related Documents

- [Architecture Freeze](../docs/architecture/Architecture-Freeze-v1.0.md)
- [Execution History](../docs/architecture/Execution-History.md)
- [History versus Audit](../docs/architecture/Execution-History-vs-Audit.md)
- [Execution State](../docs/architecture/Execution-State.md)

## Supersession Rules

Changes to state authority, delivery infrastructure, event sourcing, Audit,
retention ownership, or terminal-write semantics require a later accepted ADR
or approved Architecture Exception.
