---
title: WP-005 Controlled Lab Cleanup
version: 1.0.0
status: Prepared
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# WP-005 Controlled Lab Cleanup

1. Gracefully stop the collector service if started solely for this test.
2. Close operator-created local PowerShell sessions.
3. Confirm no process kill or target service/configuration change occurred.
4. Re-run read-only database integrity checks.
5. Compare the target baseline and record “target state unchanged”.
6. Secure and redact evidence.

No target-side artifact should exist. Inventory/connectivity rows remain as
evidence by default. Deletion is destructive SQL and requires separate explicit
approval identifying database and `ManagedServerId`; no delete script is
provided here.

Prefer disposal/restore of the dedicated test database under its existing lab
procedure. If migration was separately approved, use the pre-approved
disposable-database or backup strategy; never automatically run migration
`Down`.

On schema mismatch, partial snapshot, orphan IPv4, isolation failure, session
leak, or suspected production scope, stop cycles, preserve evidence, and make
no cleanup mutation until reviewed. Record service final state, target
unchanged confirmation, database disposition, evidence location, and that no
production access, commit, push, or tag occurred.
