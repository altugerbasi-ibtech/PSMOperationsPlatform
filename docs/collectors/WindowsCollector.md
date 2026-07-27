---
title: Windows Collector
version: 1.15.0
status: Approved
owner: Collector
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Security
product: PSM Operations Platform
---
# Windows Collector

Responsibilities: WinRM connection tests, OS discovery, performance counters, Windows Service discovery, IIS discovery and monitoring, event logs and certificates.

Prefer WinRM HTTPS and optionally fall back to HTTP when policy permits. Record the protocol used. Use bounded timeout and cancellation. Unreachable targets must not be retried forever at the normal frequency.

WP-004 delivers only service foundation and target connectivity. Its
authoritative design is
[`../tasks/WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md`](../tasks/WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md).

WP-004.2 implements Generic Host/Windows Service composition, WP-003
OperationsDatabase validation, scoped EF Core registration and a cancellable,
non-overlapping polling loop. WP-004.3 adds a scoped, no-tracking query that
loads only enabled targets whose nullable next-attempt time is due, projected
to target ID and host name. The service name is
`PSM Operations Platform Windows Collector`. WP-004.4 opens a minimal
authenticated WSMan runspace and immediately disposes it; it invokes no remote
pipeline or command and performs no target-side mutation.

Modes are `Auto`, `HttpsOnly` and `HttpOnly`. `Auto` starts with HTTPS on every
normal cycle and uses HTTP only after an approved failure. Earlier HTTP success
never becomes a persistent preference. Default ports are HTTPS 5986 and HTTP
5985; per-target backoff is capped at 60 minutes.

The collector uses its deployment identity and Windows Integrated
Authentication. It never accepts target credentials, bypasses certificate
validation, modifies TrustedHosts or exposes remote action capability.
It installs no agent/software, changes no target configuration and creates no
target-side database or table.

The target-specific mode, ports and 10-second timeout come from the database
projection. `Auto` is capped at 20 seconds and a cycle runs at most 20 probes
concurrently. OS, .NET, IIS and Windows Service inventory remain outside
WP-004. WP-004.5 persists last-known reachability, attempt/success timestamps,
safe failure state and deterministic backoff. It creates no attempt history,
alert, inventory or remote-action capability.

Detailed probe, remoting technology, failure, loop and persistence decisions
are in
[`WP-004-WinRM-Connectivity.md`](WP-004-WinRM-Connectivity.md).
Security controls are in
[`../security/WP-004-Windows-Collector-Security.md`](../security/WP-004-Windows-Collector-Security.md).

## WP-005 inventory direction

WP-005.2 session ownership and orchestration foundation is implemented.
WP-005.3 added normalized persistence and a controlled migration. Successful WinRM session
ownership moves from transport to probe to the target cycle, which passes the
identical session to orchestration and disposes it once. Each parallel target
resolves its orchestrator, modules, stores and EF context from its own target
scope. The seven modules are Computer, Operating System, Memory, Processor,
Disk, Volume and the combined Network Snapshot.

Network Adapter and IPv4 Address are one atomic IPv4-only Network Snapshot.
`CapturedAt` remains application-owned Türkiye local time. WP-005.2 ordering,
timeout, cancellation and session ownership remain unchanged.

WP-005.4 registers the first three scoped inventory modules in deterministic
order: Computer, Operating System and Memory. They reuse the successful session,
run allowlisted `Get-CimInstance` object projections, normalize and validate
the complete result, then call their explicit WP-005.3 store. Invalid or failed
results are not persisted.

WP-005.5 adds Processor as the fourth ordered module. `Win32_Processor.DeviceID`
is the target-scoped stable source key. A fully normalized collection is passed
once to the existing processor replace-all store; successful empty collection
clears prior state and failure preserves it.

WP-005.6 adds Disk and Volume as the fifth and sixth ordered modules.
`MSFT_StorageObject.UniqueId` is the stable key for both. They use independent
replace-all stores because the model has no Disk–Volume relationship.

WP-005.7 adds one Network module as the seventh ordered module. It collects
Adapter and IPv4 objects, correlates them through transient InterfaceIndex,
uses canonical InterfaceGuid as adapter identity and submits one ADR-006
Network Snapshot transaction. IPv6 is excluded at the CIM query.

Modules are explicitly registered and ordered. Reflection, dynamic loading and
a runtime plugin SDK remain prohibited. Singular snapshots update; plural
snapshots use validated transactional replace-all. Failed modules preserve
their prior current state.

Authoritative documents:

- [`WP-005-Windows-Inventory-Architecture.md`](WP-005-Windows-Inventory-Architecture.md)
- [`WP-005-WinRM-Inventory-Orchestration.md`](WP-005-WinRM-Inventory-Orchestration.md)
- [`WP-005-Inventory-Orchestration.md`](WP-005-Inventory-Orchestration.md)
- [`WP-005-Session-Lifecycle.md`](WP-005-Session-Lifecycle.md)
- [`../database/WP-005-Inventory-Data-Model.md`](../database/WP-005-Inventory-Data-Model.md)

DNS Alias Discovery is not inventory and requires a separate future Work
Package.

WP-005.S1 is the post-implementation controlled lab validation package. Its
prepared runbook does not authorize live access, target changes, migration
execution, or production deployment. Execution requires the documented
non-production topology, identity, database, migration, network, and explicit
approval gate.

WP-005.S2 adds post-implementation PowerShell readiness tooling under
`tools/readiness`. It validates environment prerequisites without changing the
collector, target, service, AD, or database. The only public entry point is
`Invoke-CollectorReadiness.ps1`.

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.15.0 | 2026-07-27 | Added completed WP-005.S2 read-only readiness tooling |
| 1.14.0 | 2026-07-27 | Linked the WP-005.S1 controlled lab validation gate |
| 1.13.0 | 2026-07-27 | Implemented atomic Network Adapter and IPv4 inventory |
| 1.12.0 | 2026-07-27 | Implemented independent Disk and Volume inventory modules |
| 1.11.0 | 2026-07-27 | Implemented Processor inventory with DeviceID identity |
| 1.10.0 | 2026-07-27 | Implemented Computer, Operating System and Memory inventory modules |
| 1.9.0 | 2026-07-27 | Recorded completed WP-005.3 persistence foundation |
| 1.8.0 | 2026-07-27 | Recorded implemented WP-005.2 session ownership and empty orchestration |
| 1.7.0 | 2026-07-27 | Added approved WP-005.1 inventory direction and DNS scope boundary |
| 1.6.0 | 2026-07-27 | Recorded completed WP-004 final review and delivered boundary |
| 1.5.0 | 2026-07-27 | Recorded completed WP-004.5 state persistence and deterministic backoff |
| 1.4.0 | 2026-07-27 | Recorded completed WP-004.4 read-only WinRM probe |
| 1.3.0 | 2026-07-27 | Recorded completed WP-004.3 target provider and eligibility read path |
| 1.2.0 | 2026-07-27 | Recorded completed WP-004.2 host foundation |
| 1.1.1 | 2026-07-27 | Clarified proposed, read-only, no-target-change scope |
| 1.0.0 | 2026-07-26 | Initial collector summary |
| 1.1.0 | 2026-07-27 | Added WP-004 connectivity and security boundaries |
