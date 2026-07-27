---
title: WP-005 — Windows Inventory Framework
version: 1.7.0
status: Completed
owner: Engineering
last_updated: 2026-07-27
work_package_id: WP-005
reviewers:
  - Product Owner
  - Chief Software Architect
product: PSM Operations Platform
---
# WP-005 — Windows Inventory Framework

## Status

WP-005.1 through WP-005.7 are complete. Computer, Operating System, Memory,
Processor, Disk, Volume and the atomic Adapter+IPv4 Network Snapshot are
operational. The final architecture review passed on 2026-07-27.

## Purpose

Add an explicit, compile-time-extensible Windows inventory framework to the
Windows Collector. Inventory runs only after an authenticated WinRM probe and
reuses the successful target session whenever it remains usable.

## Repository analysis

WP-004 already supplies the Generic Host/Windows Service, enabled-and-due target
provider, bounded target concurrency, HTTPS-first fallback, safe classification,
connectivity persistence, rowversion/backoff, `TimeProvider` and structured
logging.

At the WP-004 baseline the session was probe-only: `IWinRmSession` exposed only
`OpenAsync`, and `WinRmTransportClient` disposed the runspace immediately.
WP-005.2 moved successful-session ownership to target orchestration.

WP-002 `CollectorRun` and JSON `InventorySnapshot` represent execution/history
and versioned payloads. They do not implement normalized current-state
update/replace-all. The existing generic repository is too narrow for aggregate
replacement and will not be expanded into an inventory framework.

## Binding decisions

1. Inventory remains inside the Windows Collector security boundary.
2. It runs only after successful authenticated session open.
3. One target orchestration owns one successful session through inventory and
   disposes it once.
4. Modules are explicit compile-time components in deterministic order. There
   is no reflection, dynamic loading, plugin system or public SDK.
5. Initial kinds are Computer, Operating System, Memory, Processor, Disk,
   Volume and Network Snapshot. Network Snapshot owns Network Adapter and IPv4
   Address together under ADR-006.
6. Current state is authoritative: singular success updates one row; plural
   success replaces its complete target set transactionally.
7. Failure never becomes empty success and never erases prior good state.
   Successful modules may commit independently.
8. Commands and persisted fields are read-only and allowlisted.
9. DNS Alias Discovery is a separate future Work Package.
10. History, IIS, services, software, events, metrics, certificates, actions,
    alerts, dashboard and reporting remain out of scope.
11. ADR-006 defines ownership by logical snapshot. Network Adapter and IPv4
    Address form one atomic Network Snapshot, not separate persistence modules.
12. WP-005 network inventory is IPv4-only. IPv6 collection, entities, columns
    and validation are excluded.
13. Inventory uses application-owned `CapturedAt` in repository-standard
    Türkiye local time; WP-005 adds no UTC-specific timestamp field.

## Proposed architecture

```text
WindowsCollectorCycle
  -> bounded target scope
     -> open authenticated WinRM session (probe/fallback)
     -> persist connectivity result
     -> ordered inventory orchestrator
        -> module collect
        -> validate/normalize
        -> module-specific current-state persistence
     -> dispose session
```

The extension point is a narrow `IWindowsInventoryModule` contract owned by the
Windows Collector. It has a stable module kind and collects through an internal
session command abstraction. An explicit ordered catalog is composed through
DI; duplicate kinds fail startup. Modules neither own sessions nor access EF.

Session and result contracts remain collector implementation details.
PowerShell does not enter Domain and EF does not enter collection modules.
See
[`../collectors/WP-005-Windows-Inventory-Architecture.md`](../collectors/WP-005-Windows-Inventory-Architecture.md).

## Data model

`ManagedServer` stays the aggregate root. Add current-state siblings in the
`inventory` schema:

- singular: `WindowsComputerInventory`,
  `WindowsOperatingSystemInventory`, `WindowsMemoryInventory`;
- plural: `WindowsProcessorInventory`, `WindowsDiskInventory`,
  `WindowsVolumeInventory`, `WindowsNetworkAdapterInventory`,
  `WindowsIpv4AddressInventory`.

All have `ManagedServerId` and `CapturedAt`. Singular entities key on
`ManagedServerId`. Plural entities have an application GUID plus a unique
target-scoped source key. IPv4 rows have a required foreign key to adapter rows.
Inventory types do not form an inheritance chain. See
[`../database/WP-005-Inventory-Data-Model.md`](../database/WP-005-Inventory-Data-Model.md).

## Snapshot strategy

| Shape | Successful persistence | Failure/invalid result |
|---|---|---|
| Singular | Atomic insert/update | Preserve previous row |
| Plural | Validate complete result, then atomic replace-all | Preserve previous set |

A completed, validated empty plural result clears old rows. Cancellation,
timeout, command, mapping or validation failure does not. Transactions are
module-scoped and never span network work. Each committed module uses one
capture time from `TimeProvider`; cross-module atomicity is not claimed.

## WinRM strategy

The selected WP-004 transport opens a session once and passes it to inventory.
Modules execute sequentially because one runspace is not a parallel execution
boundary. WP-005.2 propagates the existing target timeout projection and host
cancellation without adding a command timeout, total budget or retry. Modules
cannot create, close or replace sessions.

Session-broken or indeterminate-after-timeout failures stop remaining modules.
A data/command failure may be isolated when session health is deterministically
Open. Disposal is attempted once; cleanup failure does not replace the primary
outcome. See
[`../collectors/WP-005-WinRM-Inventory-Orchestration.md`](../collectors/WP-005-WinRM-Inventory-Orchestration.md).

## Security and logging

- Use only the Windows Collector process identity/gMSA and Negotiate.
- Never accept credentials, `PSCredential`, SQL Authentication or identity
  sharing with SQL targets.
- Never bypass certificates or mutate TrustedHosts/WinRM.
- No arbitrary script, user command or remote action API is permitted.
- Logs allowlist correlation/target/module, outcome, count and duration.
  Raw commands, output and exception messages are excluded.

## Sprint breakdown

### WP-005.1 — Analysis and documentation

**Goal:** settle architecture, data, snapshot, session and scope decisions.

**Scope:** repository analysis, four design documents and requested baseline
updates.

**Dependencies:** completed WP-001 through WP-004.

**Acceptance criteria:** all decisions and future DNS scope are explicit; no
code, migration, package, test, commit or push exists.

### WP-005.2 — Session and orchestration foundation

**Goal:** refactor probe/session ownership and add an empty deterministic module
pipeline.

**Scope:** reusable session boundary, explicit catalog, cancellation,
isolation, events and tests; no inventory entity.

**Status:** completed.

**Dependencies:** accepted WP-005.1.

**Acceptance criteria:** successful probe session reaches the empty catalog;
fallback compatibility remains; duplicate kinds fail fast; all disposal,
timeout and cancellation paths pass.

### WP-005.3 — Current-state persistence foundation

**Goal:** implement the normalized schema and explicit stores.

**Status:** Completed on 2026-07-27. Network Adapter and IPv4 Address use one
ADR-006 ownership boundary. Disk and Volume remain separate because the model
has no normalized relationship.

**Scope:** eight entities, mappings, controlled migration, singular update and
plural replace-all persistence, tests.

**Dependencies:** WP-005.2.

**Acceptance criteria:** module transactions are atomic; failed/invalid results
preserve state; successful empty plural clears state; constraints/indexes and
no-auto-migration tests pass.

### WP-005.4 — Core system inventory

**Goal:** collect Computer, Operating System and Memory.

**Status:** Completed on 2026-07-27 using the existing reusable session,
deterministic orchestration and explicit single-state stores.

**Scope:** allowlisted CIM queries, immutable results, validation, persistence
and safe logs.

**Dependencies:** WP-005.3.

**Acceptance criteria:** all reuse the session, field/unit rules pass and a
failed module preserves its old snapshot.

### WP-005.5 — Processor inventory

**Goal:** collect Processor.

**Status:** Completed on 2026-07-27 using `Win32_Processor.DeviceID` as the
target-scoped stable key and the existing processor replace-all store.

**Scope:** one plural Processor module and its existing replacement store.

**Dependencies:** WP-005.4.

**Acceptance criteria:** DeviceID keys are required and unique; complete empty
and failure are distinguished; invalid results do not reach persistence.

### WP-005.6 — Disk and Volume inventory

**Goal:** collect Disk and Volume.

**Status:** Completed on 2026-07-27 using `MSFT_StorageObject.UniqueId` and two
independent replace-all stores.

**Scope:** two plural modules; no Disk–Volume relationship or shared
transaction.

**Dependencies:** WP-005.5.

**Acceptance criteria:** UniqueId keys are required and unique; successful
empty clears only the owned snapshot; failure preserves prior state.

### WP-005.7 — Network Adapter and IPv4 inventory

**Goal:** collect Network Adapter and IPv4 as one ADR-006 Network Snapshot.

**Status:** Completed on 2026-07-27 using InterfaceGuid identity, transient
InterfaceIndex correlation and the existing atomic Network store.

**Scope:** one orchestrated module, two allowlisted IPv4-only projections and
one persistence operation.

**Acceptance criteria:** Adapter-to-IPv4 references are valid; IPv6 is not
queried; successful empty clears both tables; failure preserves both.

## Future extensibility

TPM, Secure Boot, BIOS, BitLocker, Defender, Cluster, HBA, GPU, Windows
Features, Scheduled Tasks, Local Users and Local Groups may be added only as
explicit modules in approved later Work Packages. Their permissions, commands
and models are not predesigned here.

IPv6 requires a separate accepted ADR or explicit future Work Package. DNS
Alias Discovery affects identity/discovery, not Windows inventory. Its
separate Work Package must decide authoritative sources, normalization,
uniqueness, ownership, security and merge behavior.

## Risks

| Risk | Treatment |
|---|---|
| Probe refactor regresses fallback | Isolated WP-005.2 plus retained WP-004 tests |
| Slow inventory consumes target slots | Existing WSMan operation timeout and bounded outer concurrency |
| Partial output erases good data | Validate completion before replacement |
| Unstable identifiers churn rows | Defined target-scoped source keys |
| Extension becomes plugin platform | Compile-time catalog only |
| Mixed capture times appear atomic | Expose per-module `CapturedAt` |

## Open questions

None block WP-005.2. Exact command text, timeout defaults and final field
lengths require representative Windows evidence in their implementation sprint
without changing these boundaries.

## Definition of done for WP-005.1

- [x] Repository and architecture analyzed.
- [x] Extension point, model, snapshot and session strategies defined.
- [x] Initial/future module and DNS boundaries documented.
- [x] Roadmap and acceptance criteria defined.
- [x] No code, migration, package, test, commit or push created.

## References

- [`../index.md`](../index.md)
- [`../project/Principles.md`](../project/Principles.md)
- [`WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md`](WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md)
- [`../collectors/WP-005-Windows-Inventory-Architecture.md`](../collectors/WP-005-Windows-Inventory-Architecture.md)
- [`../database/WP-005-Inventory-Data-Model.md`](../database/WP-005-Inventory-Data-Model.md)
- [`../collectors/WP-005-WinRM-Inventory-Orchestration.md`](../collectors/WP-005-WinRM-Inventory-Orchestration.md)
- [`../adr/ADR-006-Inventory-Ownership-Boundaries.md`](../adr/ADR-006-Inventory-Ownership-Boundaries.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.7.0 | 2026-07-27 | Passed the WP-005 final architecture review |
| 1.2.0 | 2026-07-27 | Applied ADR-006 ownership, IPv4-only and timestamp decisions |
| 1.1.0 | 2026-07-27 | Recorded completed WP-005.2 foundation |
| 1.0.0 | 2026-07-27 | Completed WP-005.1 analysis and design |
