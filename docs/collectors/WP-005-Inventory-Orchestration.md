---
title: WP-005.2 — Inventory Orchestration
version: 1.0.0
status: Implemented
owner: Collector
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Engineering
product: PSM Operations Platform
---
# WP-005.2 — Inventory Orchestration

## Purpose

Record the WP-005.2 orchestration foundation that follows a successful WP-004
WinRM probe. Later WP-005 sprints populate the same pipeline without changing
its ordering, failure or cancellation semantics.

## Implemented flow

```text
enabled/due WindowsTarget
  -> WP-004 transport probe
  -> successful session ownership transfer
  -> connectivity result persistence
  -> WindowsInventoryExecutionContext
  -> deterministic module pipeline
  -> ordered module results
  -> session disposal
```

Inventory starts only when connectivity persistence returns
`AppliedSuccess`. Unreachable, cancelled, disabled, stale or persistence-failed
targets never enter inventory.

## Module contract

`IWindowsInventoryModule` contains:

- one stable `WindowsInventoryModuleKind`;
- `ExecuteAsync(WindowsInventoryExecutionContext)`.

The orchestrator materializes registrations once, orders by the explicit enum
values and rejects duplicate kinds. It uses no reflection, assembly scanning,
dynamic loading, service locator or runtime plugin mechanism.

No concrete module was registered in WP-005.2. An empty catalog remains a
valid successful orchestration result; WP-005.4 through WP-005.7 register seven
concrete modules.

## Execution context

The immutable context supplies:

- the `WindowsTarget` managed-server projection;
- the same successful `IWinRmCommandSession`;
- the host/target cancellation token;
- the injected `TimeProvider`;
- the orchestration logger;
- a non-empty per-target inventory correlation ID.

The context exposes no EF Core context, session factory or service provider.
Modules therefore cannot create sessions or reach persistence implicitly.

## Ordering and isolation

Kinds have explicit stable order:

1. Computer
2. Operating System
3. Memory
4. Processor
5. Disk
6. Volume
7. Network Snapshot (Network Adapter + IPv4 Address)

A non-cancellation module exception maps to its safe exception type name,
produces a failed module result and does not stop later independent modules.
Raw messages are excluded. Host cancellation propagates immediately and stops
later modules. If the shared command session is no longer usable, later modules
are stopped. No retry is introduced.

## Time and timeout

WP-005.2 adds no timeout. Existing target `ProbeTimeout` continues to configure
WSMan open and operation timeout. The same target projection and cancellation
token reach every module context. New per-command or total-inventory budgets
require an explicit later Work Package decision.

## Logging

Event IDs `2350`–`2354` cover inventory start, module start, module completion,
safe module failure and orchestration completion. Fields are target ID,
correlation ID, module kind, safe category, count, outcome and elapsed duration.
No command, output or raw exception message is logged.

## Scope proof

WP-005.2 adds no entity, DbSet, EF mapping, migration, repository, inventory
module, concrete inventory command, database write or package. The reusable
session has a serialized command invocation boundary, but the empty pipeline
invokes no remote command.

## References

- [`WP-005-Session-Lifecycle.md`](WP-005-Session-Lifecycle.md)
- [`WP-005-Windows-Inventory-Architecture.md`](WP-005-Windows-Inventory-Architecture.md)
- [`../tasks/WP-005.2-Implementation.md`](../tasks/WP-005.2-Implementation.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.0.0 | 2026-07-27 | Recorded implemented empty orchestration foundation |
