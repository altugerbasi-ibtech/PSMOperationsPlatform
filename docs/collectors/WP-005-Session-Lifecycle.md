---
title: WP-005.2 — Session Lifecycle
version: 1.0.0
status: Implemented
owner: Collector
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Security
product: PSM Operations Platform
---
# WP-005.2 — Session Lifecycle

## Purpose

Define the implemented single-owner lifecycle for a successful reusable WinRM
session without changing WP-004 transport behavior.

## Ownership

`WinRmTransportClient` creates a session and owns it until open completes.

- Failed, timed-out or cancelled open: the transport client disposes the
  attempted session before returning failure.
- Successful open: the transport client transfers the session in
  `WinRmAttemptResult`.
- `WindowsConnectivityProbe` transfers the selected successful session in its
  final result.
- `WindowsCollectorCycle` becomes the sole owner, passes the identical instance
  to inventory and disposes it through one `await using` boundary.

`PowerShellWinRmSession` also guards its underlying runspace against repeated
dispose. Ownership tests prove the normal cycle calls dispose exactly once.
Its command boundary serializes invocation with a per-session gate and reports
usability from deterministic runspace state.

## Lifecycle

```text
create candidate
  -> open candidate (the connectivity probe)
  -> failure: dispose candidate
  -> success: transfer candidate
     -> persist connectivity
     -> if AppliedSuccess: execute inventory with same instance
     -> dispose once
```

In `Auto`, a failed HTTPS candidate is disposed before HTTP is attempted.
Therefore a target may create two candidates, but only the one successful
session is reusable and reaches inventory.

## Leak prevention

Cycle ownership begins immediately when the probe result is received. The
`await using` declaration precedes logging, persistence and orchestration, so
success, persistence skip/failure, module failure, cancellation and unexpected
exceptions all leave through deterministic disposal.

Failed sessions never leave `WinRmTransportClient`. A successful probe without
a session is a contract violation classified safely by the target boundary.

## Timeout and cancellation

Existing WP-004 timeout sources remain unchanged:

- target-specific timeout for each open attempt;
- combined 20-second `Auto` probe budget;
- host cancellation linked to both.

No new timeout or retry exists. Cancellation is passed unchanged into
orchestration and modules. Host cancellation remains control flow and wins over
timeout.

## Thread safety

One session is used only by its single target task. Modules run sequentially,
and the command boundary also prevents overlapping invocation. The session is
never shared between targets. Outer target concurrency remains bounded at 20.

## References

- [`WP-004-WinRM-Connectivity.md`](WP-004-WinRM-Connectivity.md)
- [`WP-005-Inventory-Orchestration.md`](WP-005-Inventory-Orchestration.md)
- [`../tasks/WP-005.2-Implementation.md`](../tasks/WP-005.2-Implementation.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.0.0 | 2026-07-27 | Recorded implemented session ownership lifecycle |
