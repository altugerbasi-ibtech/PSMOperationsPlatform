---
title: WinRM Standards
version: 1.3.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Security
product: PSM Operations Platform
---
# WinRM Standards

Prefer HTTPS; optionally fall back to HTTP only when configured. Record protocol, use bounded timeout and cancellation, classify DNS/firewall/authentication/authorization failures separately and reduce probe frequency for unreachable targets.

## WP-004 policy

The following transport behavior is implemented by WP-004.4.

- `Auto` starts with HTTPS and uses HTTP only for an eligible failure.
- `HttpsOnly` never falls back; `HttpOnly` never attempts HTTPS.
- Every normal `Auto` cycle starts with HTTPS, even after HTTP success.
- Default ports are HTTPS 5986 and HTTP 5985.
- Cancellation, DNS, authentication, authorization, invalid configuration and
  unexpected failures do not fall back.
- TLS/certificate, listener, refused, timeout, port-specific network and
  protocol failures may fall back in `Auto`.

Certificate validation must not be bypassed. The collector must not modify
TrustedHosts or receive explicit credentials. Probes use the Windows Collector
service identity and remain read-only.

The delivered collector runs on Windows Server 2022 or later and targets Windows
Server 2016, 2019, 2022 and 2025. Custom ports are deployment-specific and
replace the relevant 5986/5985 default. Each attempt and combined `Auto` probe
uses the target-specific 10-second default timeout. `Auto` has a 20-second
combined budget, and at most 20 targets are probed concurrently. Host
cancellation stops fallback and triggers deterministic resource cleanup.
The in-process Microsoft.PowerShell.SDK 7.6.4 client opens a WSMan runspace
with process identity; it does not invoke `powershell.exe`, and PowerShell 7 is
not required on targets.

The complete matrix is in
[`../tasks/WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md`](../tasks/WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md).
Exact probe semantics and cleanup are in
[`../collectors/WP-004-WinRM-Connectivity.md`](../collectors/WP-004-WinRM-Connectivity.md).

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.3.0 | 2026-07-27 | Synchronized handbook wording with completed WP-004 |
| 1.2.0 | 2026-07-27 | Recorded implemented WP-004.4 SDK, budgets and transport behavior |
| 1.1.1 | 2026-07-27 | Marked WP-004 proposed and added compatibility/timeout direction |
| 1.0.0 | 2026-07-26 | Initial WinRM standards |
| 1.1.0 | 2026-07-27 | Defined WP-004 transport and fallback rules |
