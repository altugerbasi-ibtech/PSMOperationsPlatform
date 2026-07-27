---
title: WP-004 — WinRM Connectivity
version: 1.3.0
status: Implemented
owner: Collector
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Security
product: PSM Operations Platform
---
# WP-004 — WinRM Connectivity

## Purpose

Define the implemented read-only connectivity probe, transport policy and
failure semantics for WP-004.4.

## Probe definition

The probe opens and immediately closes an authenticated WinRM/PowerShell
remoting session with the collector process identity. It invokes no pipeline,
script or remote command. Success verifies endpoint reachability, WSMan
negotiation, process-identity authentication and permission to open the
configured remoting endpoint. It does not verify inventory cmdlet permissions,
OS/IIS/.NET/service state or authorization for future collection.

## Technology options

| Option | Benefit | Limitation | Decision |
|---|---|---|---|
| TCP socket | Simple port check | Does not prove WSMan/authentication | Rejected as final probe |
| WSMan native/COM API | Direct protocol access | Weaker managed cancellation/test seam | Fallback option |
| In-process PowerShell runspace over WSMan | Managed session semantics and testable boundary | Package/.NET 10 and service compatibility must be proven | Recommended |
| `powershell.exe` process | Familiar tool | Localized output, process management and unsafe expansion path | Rejected |

The recommended technology is an in-process
`System.Management.Automation` runspace with WSMan connection information.
`Microsoft.PowerShell.SDK` 7.6.4 is referenced only by the Windows Collector.
.NET 10 Release build and framework-dependent publish are verified. Process
invocation is not a fallback.

Targets are Windows Server 2016, 2019, 2022 and 2025 using their available
Windows PowerShell remoting endpoint. PowerShell 7 is not required on targets.

## Identity and transport modes

Windows Integrated Authentication uses the Windows Collector gMSA/process
identity. No username, password or `PSCredential` is supplied.

| Mode | Behavior |
|---|---|
| `Auto` | HTTPS first; one HTTP attempt only for an eligible HTTPS result |
| `HttpsOnly` | HTTPS only |
| `HttpOnly` | HTTP only |

Defaults are HTTPS 5986 and HTTP 5985. Approved target configuration may set
custom ports. Every normal `Auto` cycle starts with HTTPS even after an earlier
HTTP success.

HTTPS retains normal Windows certificate chain, name and revocation policy.
Certificate bypass and TrustedHosts mutation are prohibited. HTTP provides no
TLS server authentication or transport encryption; enabling it is an explicit
security and operational decision, not a generally safe default.

## Fallback decision matrix

| HTTPS result category | HTTP fallback | Reason | Final target state if HTTP not attempted |
|---|---:|---|---|
| Success | No | Probe complete | Reachable |
| Cancellation | No | Shutdown control flow | Unchanged |
| DNS failure | No | Host name is shared | Unreachable |
| Connection refused | Yes | Listener/port may differ | Unreachable |
| Timeout | Yes | HTTP path may differ; budgets remain bounded | Unreachable |
| TLS failure | Yes | HTTPS-specific trust/handshake failure | Unreachable |
| Authentication failure | No | Transport does not repair identity | Unreachable |
| Authorization failure | No | Transport does not grant permission | Unreachable |
| WinRM endpoint unavailable | Yes | HTTP listener may exist | Unreachable |
| Protocol failure | Yes | Listener-specific negotiation may differ | Unreachable |
| Unexpected failure | No | Fail closed | Unreachable |

If HTTP is attempted, its result is final. HTTPS failure followed by HTTP
success is `Reachable` over HTTP; the intermediate failure is not persisted.
WP-004.5 persists only that final result and does not change configured mode or
ports.

## Timeout, cancellation and disposal

Each transport attempt has explicit connection/operation timeout plus host
cancellation. `Auto` also has a combined bounded budget. Cancellation wins over
timeout and stops fallback. Runspaces, registrations and native resources are
disposed in `finally`; timeout/cancellation requests bounded cleanup. No
abandoned session may continue into another cycle and no live resource is
returned.

Each transport attempt defaults to the target-specific 10-second timeout.
`Auto` has a 20-second combined budget and maximum target probe parallelism is
20. External host cancellation preempts both budgets.

## Failure mapping

Stable categories are `None`, `DnsFailure`, `ConnectionRefused`, `Timeout`,
`TlsFailure`, `AuthenticationFailure`, `AuthorizationFailure`,
`WinRmUnavailable`, `ProtocolFailure` and `Unexpected`.
`DatabaseUnavailable` and `Cancelled` are collector control outcomes and are
not target failure state. Raw exceptions, native messages, certificate,
Kerberos and remote output are neither persisted nor logged.

## Optional infrastructure testing

Default CI uses fake transport behavior. Real tests are Windows-only,
environment-gated and require an explicitly controlled target. They contain no
repository hostname, credential or secret and are not required by the default
suite. HTTPS scenarios require controlled certificate infrastructure.

## Known limitations and operational risks

- Opening a session proves endpoint use, not future inventory authorization.
- Kerberos/NTLM negotiation is controlled by deployment policy; WP-004 provides
  no guarantee of a specific negotiated mechanism.
- HTTP materially weakens transport assurances.
- Real Windows Service installation and real-target WinRM remain
  environment-gated operational smoke tests.
- One slow endpoint consumes bounded concurrency until timeout/cleanup.

## References

- [`WP-004-Windows-Collector-Architecture.md`](WP-004-Windows-Collector-Architecture.md)
- [`WP-004-Target-State-and-Backoff.md`](WP-004-Target-State-and-Backoff.md)
- [`../security/WP-004-Windows-Collector-Security.md`](../security/WP-004-Windows-Collector-Security.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.3.0 | 2026-07-27 | Clarified WP-004.5 final-result persistence boundary |
| 1.2.0 | 2026-07-27 | Recorded implemented WP-004.4 probe, verified SDK build/publish and bounded orchestration |
| 1.1.0 | 2026-07-27 | Recorded WP-004.3A projection readiness and approved WP-004.4 timeout, parallelism and SDK inputs |
| 1.0.0 | 2026-07-27 | Proposed WP-004 connectivity and fallback design |
