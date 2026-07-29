# Inventory Pipeline

The frozen pipeline now exposes non-controlling Monitoring after Runtime event
publication. Monitoring does not reevaluate inventory, capabilities, decisions
or plans and does not introduce history.

The frozen downstream handoff is: committed Execution Plan → Execution Dispatcher preparation → Collector Runtime execution. Dispatcher and Runtime do not reevaluate inventory, capabilities, decisions, or planning.

Plugin SDK validation consumes only fixed execution provenance and resolved policy. It never reads raw inventory/capability facts or contacts a target.

Current-state semantics are recorded by [ADR-0005](../../adr/ADR-0005-Current-State-Inventory.md).

Status: **IMPLEMENTED — INTEGRATION PENDING**

The pipeline collects, normalizes, and validates all inventory before opening
the inventory transaction. It atomically replaces current inventory, assigns
one `InventoryRunId`, increments `InventoryVersion` once, and commits.

Only after commit does it evaluate capabilities from that coherent snapshot.
Capability persistence uses a separate atomic transaction and never increments
`InventoryVersion`. Capability failure cannot roll back committed inventory
and must preserve the previous valid capability snapshot.

After capability persistence commits, decision evaluation consumes that
committed snapshot and atomically replaces the current decision plan in a third
transaction. Capability failure prevents decision evaluation. Decision failure
preserves inventory, capability state, and the prior valid plan. No transaction
spans remote collection and no decision stage performs discovery or execution.

After Decision Plan persistence commits, execution planning builds and
atomically persists a separate immutable current Execution Plan. Planning
failure or cancellation preserves inventory, capabilities, decisions, and the
prior Execution Plan. No stage reruns an earlier engine.

After Execution Plan persistence, an explicitly invoked runtime boundary may
load that committed plan and create current Execution State. Production
automatic activation remains disabled until the integration gate. Runtime
failure cannot roll back an earlier committed stage.
