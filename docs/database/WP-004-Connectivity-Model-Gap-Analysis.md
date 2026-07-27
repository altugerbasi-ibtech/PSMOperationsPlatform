---
title: WP-004 — Connectivity Model Gap Analysis
version: 1.3.0
status: Implemented
owner: Database
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Engineering
product: PSM Operations Platform
---
# WP-004 — Connectivity Model Gap Analysis

## Purpose

Record the verified WP-002 persistence gap and its completed WP-004.3,
WP-004.3A and WP-004.5 additive closure.

## Existing model

`ManagedServer` already contains `Id`, normalized unique `Fqdn`, optional
`DisplayName` and `Environment`, `IsEnabled`, `CreatedAt` and `UpdatedAt`.
It is the correct target aggregate. WP-004.5 adds its rowversion. `CollectorNode` and
`CommandQueueItem` have rowversion but their semantics do not represent target
policy or reachability.

`CollectorRun` and `InventorySnapshot` are collection history,
`CollectorHeartbeat` is process health, `CommandQueueItem` is durable command
state and `AuditLog` is append-only audit evidence. None should be reused.

## Requirement mapping and missing fields

| Requirement | Existing support | Minimum addition |
|---|---|---|
| Enabled lifecycle | `IsEnabled` | None |
| Target identity/duplicates | normalized `Fqdn`, unique index | None |
| WinRM mode/ports/timeout | WP-004.3A fields and constraints | None |
| Current reachability | WP-004.5 state | None |
| Attempt/success separation | WP-004.5 nullable timestamps | None |
| Successful transport | WP-004.5 nullable transport | None |
| Backoff | WP-004.5 failure count and eligibility | None |
| Safe final failure | WP-004.5 nullable category | None |
| Stale-write protection | WP-004.5 rowversion | None |

The detailed proposed fields, nullability and defaults are authoritative in
[`../collectors/WP-004-Target-State-and-Backoff.md`](../collectors/WP-004-Target-State-and-Backoff.md).
Enums use readable strings, consistent with repository direction. Application
and domain construction own defaults. A controlled migration may use temporary
backfill defaults but should not leave SQL time defaults.

## Constraints and indexes

The existing `UX_ManagedServer_Fqdn` remains. Add port-range and non-negative
failure-count checks plus an eligibility index starting with
`IsEnabled, NextConnectivityAttemptAt`. No new unique identity constraint is
needed.

## Concurrency and migration

`ManagedServer.RowVersion` is required because target policy and probe state
share one aggregate and stale results must not overwrite changed policy.
Existing tokens on other entities are not reusable.

WP-004.3 adds `NextConnectivityAttemptAt` and
`IX_ManagedServer_Eligibility` through the controlled
`AddManagedServerConnectivityEligibility` migration. The migration is never
applied during host startup. WP-004.5 closes the remaining gap through the
controlled `AddManagedServerConnectivityState` migration. It backfills
`Unknown` and zero failures, creates no history table and is never run at host
startup.

WP-004.3A adds `WinRmTransportMode`, `WinRmHttpsPort`, `WinRmHttpPort` and
`WinRmProbeTimeoutSeconds`. Existing rows are backfilled as `Auto`, 5986, 5985
and 10 seconds before columns become required. The migration creates no history
table and is never applied during application startup.

## History decision and rejected alternatives

No attempt-history table is justified. Rejected alternatives are:

- placing connectivity in `CollectorRun` or `InventorySnapshot`;
- using `AuditLog` per attempt;
- storing state in a JSON bag;
- adding a separate target entity;
- adding a generic repository or connectivity framework;
- using SQL current time or automatic migration.

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.3.0 | 2026-07-27 | Closed the model gap with WP-004.5 state fields and rowversion migration |
| 1.2.0 | 2026-07-27 | Recorded completed WP-004.3A target-specific WinRM configuration model |
| 1.1.0 | 2026-07-27 | Recorded the implemented WP-004.3 eligibility field, index and controlled migration |
| 1.0.0 | 2026-07-27 | Recorded verified WP-002 connectivity gaps |
