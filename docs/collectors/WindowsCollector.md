---
title: Windows Collector
version: 1.6.0
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

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.6.0 | 2026-07-27 | Recorded completed WP-004 final review and delivered boundary |
| 1.5.0 | 2026-07-27 | Recorded completed WP-004.5 state persistence and deterministic backoff |
| 1.4.0 | 2026-07-27 | Recorded completed WP-004.4 read-only WinRM probe |
| 1.3.0 | 2026-07-27 | Recorded completed WP-004.3 target provider and eligibility read path |
| 1.2.0 | 2026-07-27 | Recorded completed WP-004.2 host foundation |
| 1.1.1 | 2026-07-27 | Clarified proposed, read-only, no-target-change scope |
| 1.0.0 | 2026-07-26 | Initial collector summary |
| 1.1.0 | 2026-07-27 | Added WP-004 connectivity and security boundaries |
