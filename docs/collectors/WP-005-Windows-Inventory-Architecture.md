---
title: WP-005 — Windows Inventory Architecture
version: 1.1.0
status: Approved
owner: Architecture
last_updated: 2026-07-27
reviewers:
  - Engineering
  - Security
product: PSM Operations Platform
---
# WP-005 — Windows Inventory Architecture

## Purpose

Define the extension point, lifecycle and failure boundaries for integrating
Windows inventory with the completed WP-004 connectivity pipeline.

## Existing architecture evidence

`WindowsCollectorCycle` loads enabled/due targets and executes at most 20 in
parallel. `IWindowsConnectivityProbe` selects transport through WP-004 policy.
At the WP-004 baseline, `WinRmTransportClient` created, opened and immediately
disposed an `IWinRmSession` that supported no command invocation. WP-005.2
moved successful-session ownership outward. Connectivity and inventory
dependencies now resolve in one fresh scope per target so parallel targets
never share scoped stores or an EF Core context.

These host, concurrency, persistence, time and security boundaries remain.
Only successful session ownership must move outward to enable reuse.

## Selected extension point

The extension point is an internal, narrow `IWindowsInventoryModule`:

```text
Kind: WindowsInventoryKind
CollectAsync(IWinRmCommandSession, CancellationToken)
```

Concrete implementation may use typed module result contracts. It must retain:

- a stable enum kind rather than runtime type strings;
- compile-time DI registration and explicit deterministic order;
- exactly one module per kind, validated at startup;
- immutable execution inputs and normalized persistence inputs;
- explicit ownership-focused store dependency per module;
- no module-created session;
- no reflection, scanning, dynamic loading or runtime plugins.

Each module collects, normalizes and validates its complete result before
calling its explicit ownership-focused store. The orchestrator owns ordering
and failure isolation. This keeps PowerShell out of Domain and EF Core out of
remote collection. Seven initial modules justify the small
contract; a plugin SDK, universal generic pipeline, base-class hierarchy,
service locator and generic inventory repository are rejected.

## Ownership

| Concern | Owner |
|---|---|
| Inventory semantics/entity invariants | Domain |
| Cross-host use-case contracts, only if later needed | Application |
| EF mappings, migration and current-state stores | Infrastructure |
| WinRM session, commands, modules and target orchestration | Windows Collector |
| Catalog/order/composition | Windows Collector host |

Under ADR-001, collector-specific sequencing may remain in the collector until
a concrete second consumer justifies moving it.

## Target lifecycle

1. Load an enabled, due target.
2. Open a session using unchanged WP-004 transport/fallback rules.
3. Persist final connectivity.
4. Stop if unreachable, disabled, policy-stale or connectivity cannot persist.
5. Retain the successful session.
6. Execute modules sequentially in fixed order.
7. Validate and persist each result independently.
8. dispose the session once.

The existing due cycle remains the only scheduler. WP-005 adds no separate
inventory cadence or backoff.

## Initial order

1. Computer
2. Operating System
3. Memory
4. Processor
5. Disk
6. Volume
7. Network Snapshot (Network Adapter + IPv4 Address)

Adapter and IPv4 are collected and validated by one module and committed by
one atomic Network Snapshot store. No other module may depend on a prior
database commit.

## Failure isolation

| Failure | Effect |
|---|---|
| Session open/fallback failure | Run no inventory |
| Host cancellation | Stop, dispose, no later module |
| Broken/closed/indeterminate session | Stop remaining modules |
| Command/mapping/validation failure with proven open session | Preserve module state and continue |
| Module persistence failure | Roll back its transaction; continue independent modules |
| Adapter/IP dependency failure | Replace neither network set |

No network work occurs in a database transaction. A timeout with indeterminate
session health is conservatively terminal; no speculative health command is
introduced.

## Snapshot evidence

WP-005 does not create `CollectorRun` or write `InventorySnapshot`. Those
existing entities are execution/history-oriented contracts outside this
current-state delivery. `InventorySnapshot` is a versioned,
run-linked historical JSON contract; history is explicitly later. Current
state resides in the normalized entities defined by the data-model document.

## Time

All wall time, elapsed duration and timeout behavior uses injected
`TimeProvider`. Wall timestamps use `GetLocalNow()` under the accepted Türkiye
local-time baseline; elapsed values use timestamp APIs. Each module commit has
one `CapturedAt`. Cross-module atomic capture is never claimed.

## Logging

WP-004 occupies event IDs `2300`–`2345` inside its documented `2300`–`2399`
range. WP-005.2 must allocate non-overlapping IDs through architecture review,
either from the unused portion or a new repository-wide range.

Required event concepts: target inventory start/completion, module
start/completion/failure, persistence outcome, session-aborted remainder and
cycle summary. Allowlisted fields are correlation ID, target ID, module kind,
safe category/outcome, item count and duration. Never log command text, output,
exception message, domain/user detail, IP configuration or serial numbers.

## Future extensibility

Approved later Work Packages may add explicit modules for TPM, Secure Boot,
BIOS, BitLocker, Defender, Cluster, HBA, GPU, Windows Features, Scheduled Tasks,
Local Users and Local Groups. Compile-time addition is the supported
extensibility model.

DNS Alias Discovery is not a module. It affects identity/discovery and requires
separate decisions before relating or modifying `ManagedServer`.

## Rejected alternatives

- putting all inventory in the connectivity probe;
- reopening a session per module;
- parallel commands on one runspace;
- reflection/assembly discovery;
- module-owned EF/generic repository access;
- a target-wide transaction around WinRM;
- JSON-only current state;
- clearing old data on failure;
- treating DNS aliases as Computer inventory.

## References

- [`WP-004-Windows-Collector-Architecture.md`](WP-004-Windows-Collector-Architecture.md)
- [`WP-004-WinRM-Connectivity.md`](WP-004-WinRM-Connectivity.md)
- [`../database/WP-005-Inventory-Data-Model.md`](../database/WP-005-Inventory-Data-Model.md)
- [`WP-005-WinRM-Inventory-Orchestration.md`](WP-005-WinRM-Inventory-Orchestration.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.1.0 | 2026-07-27 | Recorded implemented WP-005.2 empty orchestration foundation |
| 1.0.0 | 2026-07-27 | Defined WP-005.1 inventory architecture |
