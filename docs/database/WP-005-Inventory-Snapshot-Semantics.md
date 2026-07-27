---
title: WP-005 - Inventory Snapshot Semantics
version: 1.0.0
status: Approved
owner: Database
last_updated: 2026-07-27
product: PSM Operations Platform
---
# WP-005 - Inventory Snapshot Semantics

Inventory stores only the last successful current state. History is out of
scope.

Computer, Operating System and Memory explicitly insert or update one row keyed
by `ManagedServerId`, refresh `CapturedAt`, and save once.

Processor, Disk and Volume independently materialize and validate the complete
result, reject duplicate target-scoped stable keys, validate the enabled
target, then replace only their owned rows in one transaction. Successful empty
results clear old rows. Invalid input fails before deletion. Exceptions and
cancellation roll back and preserve the old snapshot.

Network Adapter and IPv4 Address are one Network Snapshot. Validation covers
unique adapters, adapter ownership, canonical IPv4, prefix `0..32`, and
duplicate IPv4 identity. One transaction deletes IPv4 then adapters, inserts
adapters then IPv4, and commits only the complete aggregate. IPv6 and
IPv4-mapped IPv6 are rejected. Persistence does not infer filtering for
loopback, APIPA or private IPv4 addresses.

Transactions open only during persistence, never during WinRM collection.
Unrelated ownership boundaries never share a transaction.

Replace-all transactions execute through the repository's configured EF Core
execution strategy. This is required for compatibility with the existing SQL
Server `EnableRetryOnFailure` policy and does not introduce an inventory-owned
retry policy.
