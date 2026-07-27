---
title: WP-004 — Windows Collector Prerequisites
version: 1.4.0
status: Approved
owner: Operations
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Security
product: PSM Operations Platform
---
# WP-004 — Windows Collector Prerequisites

## Purpose

Define deployment prerequisites and verification checks for WP-004.
This dedicated Deployment-category document separates operator evidence from
the topology rules in the architecture document.

## Collector prerequisites

- Windows Server 2022 or later.
- Supported .NET 10 runtime and Windows Service hosting prerequisites.
- Windows Service name: `PSM Operations Platform Windows Collector`.
- Dedicated Windows Collector gMSA installed on the host and granted
  `Log on as a service`.
- Windows Integrated access to the required OperationsDatabase objects.
- DNS, deployment-required AD/Kerberos services and synchronized time.
- Trust chain and name validation for target HTTPS certificates.
- Explicit outbound allowlists to SQL, DNS, AD/DC and approved WinRM endpoints.
- Local logging permission only when the approved logging sink requires it; no
  local administrator permission is assumed.

## Target prerequisites

- Windows Server 2016, 2019, 2022 or 2025.
- WinRM enabled before collector deployment.
- HTTPS listener and valid certificate for `Auto`/`HttpsOnly`.
- HTTP listener only for approved `HttpOnly` or fallback use.
- Inbound firewall allowlist from collector source hosts to configured port.
- DNS/FQDN, SPN and Kerberos deployment configuration compatible with Windows
  Integrated Authentication.
- Least-privilege remote permission to open/close the probe session.
- No collector agent, file/software deployment, local database or target-side
  table.

## Network matrix

| Source | Destination | Protocol | Port | Direction | Purpose | Required mode |
|---|---|---|---|---|---|---|
| Windows Collector | Operations SQL Server | TCP | deployment-defined | Outbound | Central persistence | All |
| Windows Collector | Windows target | WinRM HTTPS | 5986 or configured | Outbound | Preferred probe | `Auto`, `HttpsOnly` |
| Windows Collector | Windows target | WinRM HTTP | 5985 or configured | Outbound | Approved fallback/HTTP probe | Conditional, `HttpOnly` |
| Windows Collector | DNS | DNS | 53 | Outbound | Name resolution | All |
| Windows Collector | AD/DC | Kerberos/directory | deployment-defined | Outbound | Integrated authentication | All |

Rules are deployment-specific allowlists. Do not add broad AD/DC ranges, a
hard-coded SQL port, SMB, RDP or general RPC merely for WP-004.

## Operational checklist

- [ ] DNS resolves each configured FQDN from the collector host.
- [ ] The gMSA can run the Windows Service without interactive credentials.
- [ ] The gMSA can read targets and update connectivity state in
      OperationsDatabase.
- [ ] WinRM HTTPS is tested where required.
- [ ] WinRM HTTP is tested only where explicitly permitted.
- [ ] Certificate chain and target-name trust are verified.
- [ ] Source/destination firewall allowlists match actual custom/default ports.
- [ ] No TrustedHosts dependency or certificate bypass exists.
- [ ] Collector, target and domain clocks are synchronized.
- [ ] Windows Collector has no SQL Collector or Action Executor permissions.
- [ ] Controlled WP-004.5 connectivity-state migration was applied by the
      deployment identity before collector rollout.

## Windows Service smoke-test procedure

This environment procedure was documented but not executed during WP-004.4.

1. Publish the Release Windows Collector on Windows Server 2022 or later.
2. Install `PSM Operations Platform Windows Collector` under a controlled test
   service identity; production uses the dedicated gMSA.
3. Verify integrated-authentication database access and start with no eligible
   targets or one explicitly controlled test target.
4. Confirm startup and cycle logs contain no connection string, SQL endpoint,
   credential, certificate detail or raw remoting error.
5. Where approved, verify the configured listener without changing
   TrustedHosts or certificate validation.
6. Stop through Service Control Manager and verify graceful cancellation,
   runspace disposal and no orphan collector process.
7. Remove the test registration and publish output under the deployment change
   record.

The collector hosts PowerShell in process, does not launch `powershell.exe`,
and does not require PowerShell 7 on a target.

## References

- [`../architecture/10-Deployment-Architecture.md`](../architecture/10-Deployment-Architecture.md)
- [`../security/WP-004-Windows-Collector-Security.md`](../security/WP-004-Windows-Collector-Security.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.4.0 | 2026-07-27 | Approved prerequisites after WP-004.6 final review |
| 1.3.0 | 2026-07-27 | Added controlled WP-004.5 connectivity-state migration prerequisite |
| 1.2.0 | 2026-07-27 | Added WP-004.4 SDK prerequisites and Windows Service smoke-test procedure |
| 1.1.0 | 2026-07-27 | Recorded WP-004.2 service name and host foundation status |
| 1.0.0 | 2026-07-27 | Proposed WP-004 deployment prerequisites |
