---
title: WP-005 — WinRM Inventory Orchestration
version: 1.1.0
status: Approved
owner: Architecture
last_updated: 2026-07-27
reviewers:
  - Engineering
  - Security
product: PSM Operations Platform
---
# WP-005 — WinRM Inventory Orchestration

## Purpose

Define how probe and inventory share one successful target WinRM session while
preserving timeout, cancellation, security and module isolation.

## Repository constraint

Current flow:

```text
WindowsConnectivityProbe
  -> WinRmTransportClient.AttemptAsync
     -> create session
     -> open session
     -> dispose session
```

`IWinRmSession` cannot invoke commands. The underlying runspace can be reused,
but the current abstraction/owner cannot. WP-005.2 must refactor that boundary
instead of opening a second session.

## Selected ownership

```text
target session orchestrator
  create/open HTTPS attempt
  if eligible failure: dispose it; create/open HTTP attempt
  retain successful session
  persist connectivity
  execute ordered inventory modules
  dispose successful session
```

“One session” means the one successful session is reused. `Auto` fallback may
necessarily create a failed HTTPS session plus the successful HTTP session.

## Session contract

The revised internal boundary supports asynchronous open, one allowlisted
command at a time, deterministic state/health, cancellation, bounded completion
and asynchronous disposal.

Commands are structured definitions owned by product code, never user-supplied
strings. The API returns bounded neutral/typed records needed for mapping.
Modules never receive `Runspace`, `PowerShell`, connection info or a service
provider. The abstraction remains Windows Collector-specific.

## Lifecycle

Opening the runspace is the probe. Authentication remains Negotiate with the
process identity; ports, HTTPS validation, fallback and redirection rules remain
WP-004 behavior.

After successful connectivity persistence, modules execute sequentially. Each
command owns/disposes its pipeline but retains the runspace. Every failed
attempt session is disposed before fallback, and the successful session is
disposed once in `finally`. Cleanup is bounded; its safe exception type may be
logged but never replaces the primary result.

## Timeouts and budgets

WP-005.2 preserves the existing per-transport open/operation timeout, combined
`Auto` budget and host cancellation. It introduces no per-command timeout,
target-total inventory budget or retry. The execution context propagates the
existing target timeout projection and cancellation token. Any additional
inventory timeout requires an explicit later decision when real commands exist.

## Isolation matrix

| Outcome | Session | Later modules | State effect |
|---|---|---|---|
| Open failure | Dispose attempt | None | No inventory |
| Module success | Retain | Continue | Update/replace |
| Command/mapping/validation failure; state Opened | Retain | Continue | Preserve failed module |
| Timeout; state unknown | Dispose | Stop | Preserve failed/remaining |
| Runspace Broken/Closed | Dispose | Stop | Preserve failed/remaining |
| Persistence failure | Retain | Continue if independent | Roll back module |
| Host cancellation | Dispose | Stop | No replacement |

Only deterministic runspace state permits reuse; exception text is not a health
signal. Adapter and IP may use separate commands, but replacement occurs only
after both complete and validate.

## Partial failure and connectivity

Successful modules commit independently. Failed modules retain older
`CapturedAt`, so consumers must not present all modules as one atomic server
snapshot. The orchestration result reports per-module outcomes; WP-005 does
not create a `CollectorRun`.

Inventory failure does not change connectivity: the authenticated session
already proved reachability. Permission/query/schema failures are module
outcomes. Inventory does not start if successful connectivity cannot persist
because the target is disabled, stale or the database is unavailable.

WP-005 adds no separate inventory backoff; existing target eligibility controls
the next attempt.

## Command security

- Fixed, code-owned read-only commands and typed allowlisted parameters only.
- No interpolation of target/user data into script text.
- No `Invoke-Expression`, downloaded file, arbitrary script block or user
  command.
- Consume stable properties, not localized formatted output.
- Project only the minimum properties required by each bounded machine-local
  inventory class.
- Never log/persist raw command, output or exception message.
- No Start, Stop, Restart, Recycle, Kill, Reboot or configuration mutation.

## Testing requirements for WP-005.2

- HTTPS success reuses the identical session instance.
- Fallback disposes failed HTTPS and reuses successful HTTP.
- Every session is disposed exactly once on every path.
- Commands do not overlap.
- Host cancellation wins over timeout.
- Fake time proves the unchanged WP-004 attempt and `Auto` budgets.
- broken/closed/unknown-after-timeout stops later modules;
- isolated mapping/command failure can continue on proven healthy session;
- redaction sentinels and no-credential/no-action architecture tests pass.

Real WinRM tests remain Windows-only, environment-gated and read-only.

## References

- [`WP-004-WinRM-Connectivity.md`](WP-004-WinRM-Connectivity.md)
- [`WP-005-Windows-Inventory-Architecture.md`](WP-005-Windows-Inventory-Architecture.md)
- [`../database/WP-005-Inventory-Data-Model.md`](../database/WP-005-Inventory-Data-Model.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.1.0 | 2026-07-27 | Reconciled implemented WP-005.2 with no-new-timeout decision |
| 1.0.0 | 2026-07-27 | Defined session reuse and orchestration |
