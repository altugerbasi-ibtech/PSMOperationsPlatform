---
title: ADR-006 — Inventory Ownership Boundaries
version: 2.0.0
status: Accepted
owner: Architecture
last_updated: 2026-07-27
decision_date: 2026-07-27
related_work_packages:
  - WP-005.1
  - WP-005.2
  - WP-005.3
supersedes: None
product: PSM Operations Platform
---
# ADR-006 — Inventory Ownership Boundaries

## Status

Accepted.

## Context

PSM Operations Platform stores Windows inventory as normalized current state.
The initial areas are Computer, Operating System, Memory, Processor, Storage
and Network. Some logical snapshots map to one table; others require multiple
related normalized tables.

Network inventory contains Network Adapter and IPv4 Address data. Every IPv4
address must relate to the adapter on which it was discovered. The model must
provide atomic current-snapshot replacement, preserve prior successful state
after failure/cancellation, enforce foreign keys without accidental cascade,
and isolate unrelated inventory.

“Module isolation” was ambiguous when interpreted as one module owning only one
table. That interpretation conflicts with a coherent normalized Network
Snapshot. The platform also already uses application-owned Türkiye local-time
timestamps; WP-005.3 must not introduce an inventory-only UTC convention.

## Decision

### Core inventory ownership boundary

WP-007.1 supersedes the independent persistence-boundary decision for core
Windows inventory. Computer, Operating System, Physical Memory, Processor,
Disk, Volume, Network Adapter and IPv4 Address form one Core Inventory
Snapshot. Modules collect and normalize only; they do not persist independently.

### Initial boundaries

| Ownership boundary | Owned normalized data |
|---|---|
| Computer Snapshot | Windows Computer Inventory |
| Operating System Snapshot | Windows Operating System Inventory |
| Physical Memory collection | Windows Memory Inventory |
| Processor Snapshot | Windows Processor Inventory collection |
| Storage Snapshot | Windows Disk Inventory and Windows Volume Inventory |
| Network Snapshot | Windows Network Adapter Inventory and Windows IPv4 Address Inventory |

Disk and Volume remain separate normalized collections without an artificial
relationship, but WP-007.1 replaces both inside the Core Inventory transaction.

### IPv4-only network inventory

WP-005 network inventory is IPv4-only.

- IPv6 is not queried, normalized, validated or persisted.
- No IPv6 entity or column is created.
- `AddressFamily` is omitted unless another explicit need is approved.
- IPv4 addresses use canonical IPv4 text.
- `PrefixLength` is `0..32`.
- IPv4-mapped IPv6 addresses are rejected.
- Persistence does not guess whether loopback, APIPA or private addresses are
  filtered; a collector Work Package decides those collection rules.

IPv6 requires a separate accepted ADR or explicit later Work Package scope,
including migration and validation impact.

### Network aggregate and atomic replacement

Network Adapter and IPv4 Address form one logical boundary:

```text
Network Snapshot
├── Network Adapters
└── IPv4 Addresses
```

They are not independent persistence modules. One fully materialized,
normalized and validated result is passed to one persistence operation.

A successful replacement uses one database transaction.

Delete order:

1. existing IPv4 Address rows for the target;
2. existing Network Adapter rows for the target.

Insert order:

1. new Network Adapter rows;
2. new IPv4 Address rows related to those adapters.

Commit occurs only after all rows persist. Validation, cancellation or
persistence failure rolls back and preserves both parts of the prior snapshot.
A successful empty snapshot deletes both target-owned sets. Failed, cancelled
or invalid input must not begin destructive replacement.

### Referential integrity and module isolation

Every IPv4 Address has a required explicit foreign key to its Network Adapter.
Orphan IPv4 rows are invalid. Delete behavior is explicitly `Restrict` or
`NoAction`; cascade delete is prohibited. The store explicitly deletes IPv4
dependents before adapters.

Adapter and IPv4 retain their ordered internal replacement and referential
integrity inside the wider Core Inventory transaction.

### Current-state semantics

Inventory retains only last successful current state. History is out of scope.

| Collection result | Durable effect |
|---|---|
| Successful, non-empty | Replace prior snapshot |
| Successful, empty | Clear prior snapshot |
| Failed | Preserve prior snapshot |
| Cancelled | Preserve prior snapshot |
| Invalid | Preserve prior snapshot |

Computer and Operating System insert when absent and explicitly update when
present. Every child collection is replaced as part of the complete Core
Inventory Snapshot.

### Timestamp standard

WP-005 preserves the repository timestamp standard:

- use `CapturedAt`;
- store application-owned Türkiye local time;
- obtain time through repository `TimeProvider`;
- use no database timestamp default;
- create no `CollectedAtUtc` or other UTC-specific field.

The UTC-only instruction in the initial WP-005.3 prompt is superseded by this
ADR. A platform-wide UTC transition requires a separate accepted ADR covering
impact analysis, migration strategy, existing semantics and an application-wide
transition plan. WP-005.3 does not perform that transition.

### Transaction and store boundaries

All core modules collect, normalize and validate before persistence begins.
The database transaction opens only after all remote work succeeds. One
transaction updates Computer and Operating System and replaces Physical Memory,
Processor, Disk, Volume, Network Adapter and IPv4 Address.

```text
collect all core modules
  -> validate complete Core Inventory Snapshot
  -> begin transaction
  -> replace all core current state
  -> commit once
```

Any collection, parsing or validation failure prevents persistence. Any
persistence failure rolls back all core changes. Connectivity success remains
separate from inventory success. The transaction never spans WinRM collection.

Use one narrow core-inventory store accepting one fully materialized,
normalized and validated Core Inventory Snapshot. This is not a generic
repository, Unit of Work or runtime snapshot framework.

### Validation boundary

All replace-all input is materialized and validated before deletion. Network
validation includes:

- unique target-scoped adapter stable source keys;
- valid adapter ownership and rejection of unknown adapter references;
- canonical IPv4 text and prefix `0..32`;
- rejection of IPv6 and IPv4-mapped IPv6;
- rejection of duplicate target-scoped IPv4 identity;
- required-string and maximum-length checks;
- non-negative numeric checks;
- `CapturedAt` consistent with Türkiye local time.

## Consequences

### Positive

- Adapter and IPv4 data remains relationally consistent and atomic.
- Failed collection preserves last successful state.
- Module isolation is explicit and testable.
- Foreign-key integrity needs no cascade.
- Transactions exclude unrelated modules.
- IPv4-only scope keeps the initial model bounded.
- WP-005.3 can proceed without WP-005.2 redesign.
- Timestamp semantics remain platform-consistent.

### Negative

- Network persistence is more specialized than a table-level store.
- Replacement requires ordered deletes and inserts.
- Future IPv6 needs a new decision and migration.
- Türkiye local time requires work if the product becomes multi-region.

### Accepted trade-offs

A specialized store is accepted for integrity and atomicity. IPv6 is deferred
because current need is IPv4. UTC transition is deferred to a platform-wide
decision.

## Rejected alternatives

1. **Separate Adapter and IP modules:** rejected because atomic Network Snapshot
   and foreign-key consistency cannot be safely guaranteed.
2. **Remove Adapter-to-IP foreign key:** rejected because integrity weakens and
   orphan rows become possible.
3. **Cascade delete:** rejected because repository delete behavior must be
   explicit and destructive effects must not be hidden.
4. **Generic cross-module transaction coordinator:** rejected because Adapter
   and IPv4 are one boundary, not separate modules.
5. **JSON/serialized Adapter and IP:** rejected because inventory must be
   normalized and queryable.
6. **IPv6 in WP-005:** rejected because it adds unrequired schema, validation
   and collection complexity.
7. **UTC only for WP-005:** rejected because it creates inconsistent platform
   timestamp semantics.

## WP-005.3 implementation guidance

1. Use `CapturedAt` in Türkiye local time.
2. Create IPv4 inventory only; add no IPv6 entity or column.
3. Treat Adapter and IPv4 Address as one Network Snapshot.
4. Preserve the required Adapter-to-IPv4 foreign key.
5. Use `Restrict` or `NoAction`.
6. Replace both tables in one ordered transaction.
7. Validate the complete snapshot before deletion.
8. Preserve prior state after failure or cancellation.
9. Do not change WP-005.2 ownership, ordering, timeout or cancellation.
10. Add no generic repository, snapshot framework or inventory history.

## Architecture debt

Architecture Baseline identifies ADR-005 — Türkiye Local Time Standard as
accepted, but its source document is absent from `docs/adr`. This is governance
documentation debt.

The missing ADR-005 file does not block WP-005.3. This ADR explicitly confirms
the inventory timestamp decision. Governance should restore ADR-005 or formally
replace it in a separate documentation task.

## Compliance

An implementation complies only when all core collection completes before one
Core Inventory transaction begins, Adapter and IPv4 retain their explicit
same-target foreign key and ordered replacement, partial core state cannot
commit, only IPv4 is collected/persisted, and `CapturedAt` retains Türkiye
local-time semantics.

## Related documents

- [`../architecture/Architecture-Baseline-v1.0.md`](../architecture/Architecture-Baseline-v1.0.md)
- [`../tasks/WP-005-Windows-Inventory-Framework.md`](../tasks/WP-005-Windows-Inventory-Framework.md)
- [`../database/WP-005-Inventory-Data-Model.md`](../database/WP-005-Inventory-Data-Model.md)
- [`../collectors/WP-005-Inventory-Orchestration.md`](../collectors/WP-005-Inventory-Orchestration.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 2.0.0 | 2026-07-28 | Superseded independent core persistence with one collect-first atomic Core Inventory Snapshot |
| 1.0.0 | 2026-07-27 | Accepted inventory ownership, IPv4-only and timestamp boundaries |
