---
title: WP-004 — Windows Collector Security
version: 1.4.0
status: Implemented
owner: Security
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Engineering
product: PSM Operations Platform
---
# WP-004 — Windows Collector Security

## Purpose

Define the identity, transport, authentication, remote-action and diagnostic
boundaries for Windows target connectivity.

## Security boundary

The Windows Collector is a read-oriented connectivity process. It may read
eligible target policy, open and close an authenticated WinRM session, and
update safe connectivity state. It is not an inventory collector yet and never
becomes an action channel. Web, SQL Collector and future Windows Action
Executor identities remain separate.

## Collector identity

Production runs the Windows Collector as a Windows Service under its dedicated
gMSA. The identity requires:

- the local right to log on as a service;
- least-privilege access to read eligible targets and update their connectivity
  state in OperationsDatabase;
- approved WinRM access to Windows targets;
- no SQL target monitoring permission;
- no target credential, interactive-user impersonation or migration DDL right.

The database and WinRM connections use the service process identity through
Windows Integrated Authentication. Sharing one process identity for those two
approved Windows Collector capabilities does not authorize SQL target
monitoring.

The database role allows only the target reads and connectivity-state updates
required by WP-004. It has no migration DDL, SQL target monitoring or unrelated
application-data permission. Target access is the minimum remote permission
needed to open and close the configured endpoint; least privilege is an
identity grant as well as an application behavior.

## WinRM HTTP

HTTP is permitted only for targets configured as `HttpOnly` or when an `Auto`
HTTPS failure is fallback-eligible. HTTP does not provide TLS server
authentication or transport encryption. Windows remoting authentication may
provide message-level protections depending on negotiated protocol and policy,
but WP-004 does not claim that HTTP is equivalent to HTTPS or safe for every
network.

Operators must evaluate network segmentation, domain policy, endpoint policy
and threat model before enabling HTTP. The application must not automatically
enable HTTP, change firewall rules or alter target policy. Every normal `Auto`
cycle retries HTTPS first so a later HTTPS deployment is used automatically.

## WinRM HTTPS

HTTPS is preferred. WP-004 prohibits:

- certificate-validation callbacks that accept all certificates;
- `SkipCACheck`, `SkipCNCheck`, `SkipRevocationCheck` or equivalent bypasses;
- `TrustAllCerts` behavior;
- automatic certificate installation;
- automatic TrustedHosts changes.

The target name must match the certificate and the collector host must trust
its chain according to Windows policy. Logs expose only `TlsFailure`, never
certificate subject, thumbprint, chain, native error text or other certificate
detail.

## Authentication

- Target and OperationsDatabase access use Windows Integrated Authentication.
- SQL Authentication remains prohibited.
- The application does not accept username, domain or password settings.
- `PSCredential` and explicit credentials are prohibited.
- Authentication and authorization failures are separate safe categories.
- Kerberos tickets, SPNs, negotiation details and tokens are not persisted or
  logged.
- Authentication/authorization failure does not trigger HTTP fallback in
  `Auto`, avoiding repeated identity failures and logon storms.

WP-004 does not promise a specific Kerberos or NTLM negotiation result. That is
controlled by Windows, domain and endpoint policy.

Kerberos with correctly configured DNS/FQDN and SPNs is the deployment
preference. This is not a guarantee of negotiation outcome. There is no
application-managed secret, shared credential, explicit username/password,
credential options section or credential database column.

## Read-only and remote-action boundary

The connectivity probe opens and closes a remoting session without creating a
pipeline. “Read-only” is enforced by both the collector behavior and the
least-privilege target grants; it is not a WinRM session flag.

WP-004 exposes no operation for:

- service start, stop or restart;
- process start, stop or kill;
- registry modification;
- IIS modification;
- firewall or WinRM configuration;
- reboot;
- file transfer;
- script or arbitrary command execution.
- target-side software, file, agent, database or table installation.

Remote actions belong to the separate future Windows Action Executor identity.
No generic execution method, script payload or command text may be added as a
connectivity-test convenience.

## Session and resource safety

Every probe has bounded connection/operation timeouts and host cancellation.
Runspaces, event registrations and native resources are disposed in `finally`.
No session is pooled or returned to callers. Timeout/cancellation cleanup is
bounded so a target cannot indefinitely delay Windows Service shutdown.

WP-004.4 references Microsoft.PowerShell.SDK 7.6.4 only from the Windows
Collector, supplies no explicit credential and opens WSMan with process
identity. No certificate-validation switch or TrustedHosts mutation is exposed.

## Persistence

Persist only last-known state, safe category, timestamps, counts, successful
transport and optional allowlisted diagnostic code. Do not persist:

- raw exception or stack trace;
- credential or token material;
- Kerberos/SPN detail;
- certificate detail;
- connection strings;
- remote command/output;
- full runtime configuration.

`DatabaseUnavailable` is a collector-cycle concern and must not be written as a
target connectivity failure. Cancellation is expected shutdown and is not
persisted.

WP-004.5 implements this allowlist with independent per-result scopes.
Persistence failures and concurrency conflicts do not change target failure
state unless `SaveChanges` commits. Raw exceptions remain outside the entity
and cycle logging coalesces persistence failures.

## Logging and redaction

Logging uses an allowlist:

- polling/correlation ID;
- target ID and normalized host name;
- attempted/successful transport;
- safe failure category;
- failure count;
- duration and aggregate cycle counts.

Raw exception messages are excluded because platform, authentication and
certificate exceptions can reveal topology or security configuration.
Exceptions may be classified internally; logs record the stable category and
exception type only when the type itself is approved and useful.

The logging allowlist is cycle/correlation ID, target ID, normalized FQDN,
transport, safe category, failure count, duration, outcome and aggregate
counts. The denylist is credentials/passwords/tokens, connection strings,
Kerberos/SPN/ticket details, certificate subject/thumbprint/chain detail, raw
exception/native text, remote commands/output and full configuration.

Probe success is Debug. Final target failure is Warning. Database/load or
result-persistence failure is Error. Successful HTTP fallback is not a target
Warning. Backoff skips are aggregated rather than logged per target.

## Threat summary

| Threat | Control |
|---|---|
| Credential theft from configuration | No credential parameters or storage |
| Collector becomes an action channel | No pipeline/command API; separate action executor |
| HTTPS trust bypass | Explicit prohibition and architecture tests |
| HTTP used silently forever | Per-target policy and HTTPS-first every `Auto` cycle |
| Authentication logon storm | No auth/authorization fallback; target backoff |
| Sensitive diagnostic leakage | Stable categories and allowlisted structured fields |
| Windows/SQL permission convergence | Dedicated identities and ADR-003 boundary |
| Stale result overwrites new policy | `rowversion`, one controlled reload and no infinite retry |
| Session/resource leak | Bounded timeout, cancellation and deterministic disposal |

## Deployment review checklist

- Windows Collector gMSA installed and permitted to run the service.
- Database role grants only required target-read/state-update permissions.
- Target WinRM access is read-only and separately reviewed.
- Outbound 5986 allowed; 5985 allowed only where policy approves HTTP.
- Target HTTPS certificate chain and name validation succeed.
- No TrustedHosts or certificate bypass deployment step exists.
- Service and application logs contain no prohibited data.
- Windows and SQL target permissions remain under separate identities.

## Network allowlist

Permit only collector-to-OperationsDatabase, DNS, deployment-required AD/DC and
approved target WinRM paths. SQL and AD/DC ports are deployment-defined. HTTPS
5986 is preferred; HTTP 5985/custom HTTP is allowed only for an explicitly
approved mode or fallback. Do not add SMB, RDP or broad RPC access.

HTTP fallback is a conscious security and operational decision. HTTP has no TLS
server authentication or transport encryption, and this document does not
generalize it as safe. Operators own segmentation, domain policy, listener
configuration and approval of every HTTP exception.

## Security acceptance criteria

1. The service uses the dedicated gMSA/process identity and Windows Integrated
   Authentication without explicit credentials or SQL Authentication.
2. Database and target permissions are least-privilege and remain separate from
   SQL Collector and Action Executor permissions.
3. No `PSCredential`, username/password, application secret, certificate bypass,
   `SkipCACheck`, `SkipCNCheck`, trust-all callback or TrustedHosts/WinRM
   mutation exists.
4. The probe executes no remote command/action and installs nothing.
5. Logs/persistence expose only approved safe fields and pass sentinel
   redaction tests.
6. Network access is an explicit deployment allowlist and HTTP use is recorded
   as an approved exception.

## Residual risks and operational responsibilities

- HTTP retains materially weaker transport assurances.
- Kerberos success depends on DNS, SPN, domain, clock and endpoint policy.
- Opening a session does not prove future inventory permissions are safe.
- Certificate lifecycle and revocation availability remain operator concerns.
- A highly privileged target grant could defeat the intended read-only boundary
  even though WP-004 does not invoke commands.

Operations must install the gMSA, maintain service-logon rights, provision
least-privilege database/target roles, manage DNS/SPN/time, configure listeners
and firewall rules, maintain certificate trust, review HTTP exceptions and
verify that no credential or bypass deployment step is introduced.

## References

- [`../adr/ADR-003-Collector-Separation-by-Security-Boundary.md`](../adr/ADR-003-Collector-Separation-by-Security-Boundary.md)
- [`../collectors/WP-004-WinRM-Connectivity.md`](../collectors/WP-004-WinRM-Connectivity.md)
- [`../tasks/WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md`](../tasks/WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md)
- [`gMSA.md`](gMSA.md)
- [`../handbook/WinRM.md`](../handbook/WinRM.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.4.0 | 2026-07-27 | Closed WP-004.6 security and redaction review |
| 1.3.0 | 2026-07-27 | Recorded WP-004.5 persistence isolation, redaction and concurrency boundary |
| 1.2.0 | 2026-07-27 | Recorded implemented WP-004.4 identity, cleanup and SDK boundary |
| 1.1.0 | 2026-07-27 | Added explicit boundary, allow/deny lists, acceptance and residual risk |
| 1.0.0 | 2026-07-27 | Defined WP-004 identity, transport, action and logging security boundaries |
