---
title: WP-005.3 - Inventory Migration
version: 1.0.0
status: Reviewed
owner: Database
last_updated: 2026-07-27
product: PSM Operations Platform
---
# WP-005.3 - Inventory Migration

Migration `20260727230000_AddWindowsInventoryCurrentState` is controlled and
non-automatic. It creates the Computer, Operating System, Memory, Processor,
Disk, Volume, Network Adapter and IPv4 Address current-state tables in the
`inventory` schema.

Primary keys, foreign keys, nullability, maximum lengths, `datetime2(3)`,
unique target/stable-key indexes, check constraints and restrictive deletes
are explicit. IPv4 references Network Adapter. No new `nvarchar(max)`, cascade
delete, JSON, trigger, procedure, temporal/history object or database timestamp
default exists.

Down drops IPv4 before Network Adapter and removes all added objects. Runtime
model and snapshot have zero pending differences. The migration was inspected
but not applied to an operational database.

