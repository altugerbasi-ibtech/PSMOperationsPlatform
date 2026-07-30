---
title: ADR-005 — Türkiye Local Time Standard
version: 1.0.0
status: Accepted
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# ADR-005 — Türkiye Local Time Standard

## Status

Accepted.

## Context

The Architecture Baseline records ADR-005 as accepted for a Türkiye-only
deployment. WP-002, ADR-006, Engineering Standards, entity mappings, and
deterministic tests consistently preserve application-owned Türkiye local time
for durable operational timestamps. The original ADR source was missing; this
restoration records only the decision supported by those repository sources.

## Decision

Application-owned durable timestamps use Türkiye local time (UTC+3) and are
stored as timezone-less SQL `datetime2`, normally `datetime2(3)`. Application
code obtains wall time through `TimeProvider` and central, testable conversion.
Database defaults and scattered `DateTime.Now` or `DateTime.UtcNow` do not own
application time.

Inventory `CapturedAt`, execution state, monitoring projection time, and
execution history follow the same application time authority unless a specific
approved artifact contract explicitly requires UTC. Monitoring remains
non-durable; its use of `TimeProvider` does not create persistence authority.

Türkiye does not currently observe daylight-saving clock changes, but the
application must use its configured Türkiye time-zone authority rather than
assume machine-local time. A future DST, multi-region, or UTC-persistence
transition requires a separate accepted ADR and coordinated migration plan.

## Consequences

Durable values are consistent with the current Türkiye-only operational model
and deterministic tests. Timezone-less database values require consumers to
know this repository convention and are not portable multi-region timestamps.

## Security Impact

There is no identity or permission change. Central time handling avoids
environment-dependent timestamps in security and operational evidence.

## Migration/Compatibility Impact

No migration is created by this restoration. Existing `datetime2` values and
Türkiye-local semantics remain unchanged. UTC conversion is explicitly not
performed.

## Alternatives Considered

- UTC persistence was rejected for restoration because existing approved
  implementation and ADR-006 require Türkiye-local durable values.
- Direct machine-local time was rejected because it is environment-dependent.
- Database-generated current time was rejected because application
  `TimeProvider` owns deterministic time.

## Related Documents

- [Architecture Baseline](../architecture/Architecture-Baseline-v1.0.md)
- [Engineering Standards](../engineering/PSM-Engineering-Standards.md)
- [ADR-006 Inventory Ownership Boundaries](ADR-006-Inventory-Ownership-Boundaries.md)
- [EF Core Handbook](../handbook/EFCore.md)

## Supersession Rules

Only a later accepted ADR explicitly superseding ADR-005 may change the
application-owned Türkiye-local durable timestamp standard.
