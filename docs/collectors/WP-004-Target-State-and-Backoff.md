---
title: WP-004 — Target State and Backoff
version: 1.3.0
status: Implemented
owner: Engineering
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Database
product: PSM Operations Platform
---
# WP-004 — Target State and Backoff

## Purpose

Define the minimum target lifecycle, last-known connectivity state, eligibility
and deterministic backoff implemented for WP-004.

## Lifecycle and identity

`ManagedServer.IsEnabled` is sufficient for WP-004. Enabled targets may become
eligible; disabled targets are never probed and retain last-known state.
Re-enablement makes the target immediately eligible. Hard delete and a new
decommission state are excluded; operators disable decommissioned targets.

The existing domain requires FQDN, trims whitespace, removes trailing dots and
lower-cases invariantly. `UX_ManagedServer_Fqdn` rejects duplicates after that
normalization. FQDN is preferred for certificate identity and Kerberos.
Short/NetBIOS names are not resolved or merged, and IP targets are not added.

## Connectivity fields and semantics

WP-004.4 produces the immutable probe result. WP-004.5 applies the transitions
below through a per-result DbContext scope and independent save.

| Field | Meaning |
|---|---|
| State | `Unknown`, `Reachable` or `Unreachable` current probe state |
| LastAttempt | Last completed, durably committed real probe |
| LastSuccess | Last committed success; preserved on failure |
| LastSuccessfulTransport | HTTPS/HTTP used by last success |
| ConsecutiveFailureCount | Committed consecutive final failures |
| NextEligibleAttempt | Earliest normal selection time; null means eligible |
| LastFailureCategory | Safe category for current failure; cleared on success |

`Unknown` means no current probe result or endpoint policy changed.
`Reachable` and `Unreachable` describe the last completed committed probe, not a
permanent fact.

## Eligibility rules

A target is eligible when enabled and
`NextEligibleAttempt` is null or not later than `TimeProvider` current time.
Disabled state overrides the timestamp. Selection and delay use the same built-in
`TimeProvider`; direct system time and custom clock abstractions are prohibited.

## State transitions

| Current state | Event | New state | Failure count | LastAttempt | LastSuccess | NextEligibleAttempt |
|---|---|---|---:|---|---|---|
| Any | Success | Reachable | 0 | Set now | Set now | now + 60 seconds |
| Any | Final failure | Unreachable | Increment | Set now | Preserve | now + backoff |
| Any | Cancellation | Unchanged | Preserve | Preserve | Preserve | Preserve |
| Any | Disable | Preserve | Preserve | Preserve | Preserve | Ignored while disabled |
| Any | Disabled to enabled | Unknown | 0 | Preserve | Preserve | Clear |
| Any | FQDN/mode/port change | Unknown | 0 | Preserve | Preserve | Clear |

Success clears the failure category and records successful transport. Failure
preserves last successful transport. An HTTPS intermediate failure followed by
HTTP success applies only the success row. A database persistence failure
applies no transition.

An OS upgrade has no direct transition because WP-004 does not inventory OS
state; the next probe naturally evaluates reachability. A long outage remains
eligible at the capped rate. Decommissioning is represented by disablement.

## Backoff

| Consecutive committed failures | Delay |
|---:|---:|
| 1 | 60 seconds |
| 2 | 5 minutes |
| 3 | 15 minutes |
| 4 | 30 minutes |
| 5 or more | 60 minutes |

The maximum is always 60 minutes. Success, endpoint-policy change and
re-enablement reset the count. Cancellation, disabled skips and database
failure do not increment it.

## Concurrency

WP-004.5 adds `ManagedServer.RowVersion`. A probe projection carries the
policy and rowversion used. On conflict, persistence reloads once. It discards
the result if enablement, FQDN, mode or relevant port changed; otherwise it may
reapply once. A second conflict is logged and deferred. There is no infinite
retry and rowversion is not a multi-instance lease.

## History decision

No connectivity-attempt history table is proposed. Last-known fields plus safe
structured logs meet the current operational need without creating retention
scope. `CollectorRun`, `InventorySnapshot`, `CollectorHeartbeat`,
`CommandQueueItem` and `AuditLog` are not repurposed.

## Persistence field proposal

| Property | SQL mapping | Null/default |
|---|---|---|
| `WinRmTransportMode` | `nvarchar(20)` enum string | required / `Auto` |
| `WinRmHttpsPort` | `int` | required / `5986` |
| `WinRmHttpPort` | `int` | required / `5985` |
| `LastConnectivityState` | `nvarchar(20)` enum string | required / `Unknown` |
| `LastConnectivityAttemptAt` | `datetime2(3)` | nullable |
| `LastConnectivitySuccessAt` | `datetime2(3)` | nullable |
| `LastSuccessfulTransport` | `nvarchar(10)` enum string | nullable |
| `ConsecutiveConnectivityFailures` | `int` | required / `0` |
| `NextConnectivityAttemptAt` | `datetime2(3)` | nullable |
| `LastConnectivityFailureCategory` | `nvarchar(40)` enum string | nullable |
| `RowVersion` | `rowversion` | generated |

Ports require range constraints, failure count is non-negative and an index
begins with `IsEnabled, NextConnectivityAttemptAt`.

WP-004.3A implements the four target-specific WinRM configuration fields,
readable enum mapping, port/timeout constraints and safe existing-row backfill.
WP-004.5 implements the remaining state, rowversion and mutation fields through
the controlled `AddManagedServerConnectivityState` migration.

## References

- [`../database/WP-004-Connectivity-Model-Gap-Analysis.md`](../database/WP-004-Connectivity-Model-Gap-Analysis.md)
- [`WP-004-WinRM-Connectivity.md`](WP-004-WinRM-Connectivity.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.3.0 | 2026-07-27 | Recorded implemented WP-004.5 state transitions, capped backoff and rowversion policy |
| 1.2.0 | 2026-07-27 | Clarified WP-004.4 in-memory result and WP-004.5 mutation boundary |
| 1.1.0 | 2026-07-27 | Recorded implemented WP-004.3A target connectivity configuration fields |
| 1.0.0 | 2026-07-27 | Proposed lifecycle, state and backoff model |
