---
title: WP-004 — Windows Collector Foundation and Target Connectivity
version: 1.7.0
status: Completed
owner: Engineering
last_updated: 2026-07-27
work_package_id: WP-004
reviewers:
  - Product Owner
  - Chief Software Architect
product: PSM Operations Platform
---
# WP-004 — Windows Collector Foundation and Target Connectivity

## Status

WP-004.1 analysis/documentation, WP-004.2 host foundation, WP-004.3 target
provider/eligibility read path, WP-004.3A target connectivity configuration
model, WP-004.4 WinRM connectivity probing, WP-004.5 result persistence and
WP-004.6 final review are complete.

## Purpose

Turn the Windows Collector skeleton into a reliable Windows Service that loads
enabled targets from OperationsDatabase, performs bounded read-only WinRM
connectivity probes, applies the approved HTTPS-to-HTTP policy, persists
last-known state and survives transient target or database failures.

## Business outcome

Operations receives a bounded, observable answer to whether each enabled
Windows target currently accepts an authenticated WinRM session. The result
supports later inventory Work Packages without adding inventory or action
capability now.

## Context

The repository contains the Windows Collector Generic/Windows Service host,
WP-003 provider composition, WP-002 persistence foundations and the WP-004.3
enabled/due target read path and the Windows Collector now contains the
in-process PowerShell/WSMan probe. WP-004.5 adds last-known state mutation,
deterministic backoff and `ManagedServer` rowversion protection.

## Dependencies

- WP-002 domain/persistence foundation and controlled migration policy.
- WP-003 configuration composition and OperationsDatabase validation.
- ADR-001 pragmatic dependency direction.
- ADR-003 separate Windows and SQL Collector identities.
- Approved time and Windows-only product constraints in the architecture
  baseline.

## Assumptions

- One active Windows Collector instance is deployed for WP-004.
- Targets are domain-reachable by FQDN and preconfigured for WinRM.
- Production uses a dedicated gMSA and Windows Integrated Authentication.
- Per-transport timeout is 10 seconds, the combined `Auto` budget is 20 seconds
  and maximum parallel target probes is 20.
- `Microsoft.PowerShell.SDK` 7.6.4 is approved for WP-004.4 compatibility
  validation and implementation.

## Architecture reconciliation

Architecture Baseline v1.0.9 records WP-004 as the completed Windows Collector
foundation and target-connectivity package. Durable command behavior remains a
separate future Work Package.

The baseline also lists ADR-005 as accepted, but the ADR file is absent.
Existing approved text still binds this work to Türkiye local time and
`TimeProvider.System.GetLocalNow()`. The missing ADR is a documentation gap,
not permission to choose a different time standard.

No new ADR is required: this design specializes existing collector,
persistence, security and time decisions without changing a security boundary.

## Architecture constraints

- Preserve Domain/Application/Infrastructure/host dependency direction.
- Use explicit query/update services; add no generic repository, CQRS,
  scheduler, plugin or multi-instance framework.
- Use `TimeProvider`; add no custom clock or direct system current time.
- Keep polling non-overlapping and concurrency bounded.
- Never run network work inside a database transaction.
- Never run automatic migrations.

## Security constraints

- Use the dedicated Windows Collector gMSA/process identity and Windows
  Integrated Authentication.
- Keep Windows and SQL target permissions separated.
- Accept/store no credential and use no `PSCredential` or SQL Authentication.
- Never bypass certificate validation or modify TrustedHosts/WinRM.
- Execute no remote command/action and install nothing on a target.
- Persist and log only allowlisted safe fields.

## Existing implementation summary

`PSMOperationsPlatform.WindowsCollector` now composes WP-003 configuration,
selects OperationsDatabase validation and scoped SQL Server persistence, runs
with standard Windows Service hosting or interactively, and executes a
non-overlapping polling lifecycle. Its scoped WP-004.3/3A read path queries
enabled targets whose next attempt is null or due and returns an immutable
projection containing identity, transport mode, ports and timeout. WP-004.4
opens and disposes a read-only WSMan runspace, applies HTTPS-first fallback,
enforces 10-second target attempt limits, a 20-second Auto budget and at most
20 concurrent target probes. It does not mutate target state.

`ManagedServer` supplies normalized unique FQDN, display/environment,
enablement, timestamps and the nullable next-attempt eligibility field. It has
no transport policy, reachability state or rowversion.
The existing collection entities have different semantics and are not reused.

## Persistence model gap analysis

The existing target aggregate is sufficient. WP-004.3/3A add next eligibility
and target-specific mode/ports/timeout. Current state, attempt/success
timestamps, last successful transport, failure count/category and rowversion
were added by WP-004.5. The verified gap, nullability, constraints, index
and rejected alternatives are in
[`../database/WP-004-Connectivity-Model-Gap-Analysis.md`](../database/WP-004-Connectivity-Model-Gap-Analysis.md).

## In scope

- Windows Service host integration and graceful shutdown.
- Windows Collector-only OperationsDatabase capability and DbContext
  registration.
- Enabled and eligible target loading.
- Bounded scheduling and cancellation.
- Read-only WinRM connectivity.
- `Auto`, `HttpsOnly` and `HttpOnly` modes.
- Safe failure classification and fallback.
- Per-target deterministic graduated backoff capped at 60 minutes.
- Last-known-state persistence with optimistic concurrency.
- Correlated structured logging and transient-failure recovery.

## Out of scope

- Web target CRUD or management UI.
- OS, Windows Service, IIS, event log, certificate or performance inventory.
- Connectivity attempt history.
- Command leasing or execution.
- Start, Stop, Restart, Recycle, Kill or Reboot.
- Credentials, impersonation or credential distribution.
- Certificate-validation bypass or TrustedHosts changes.
- Automatic migration and SQL Collector changes.

## Host and scheduling design

Startup shall compose WP-003 configuration, select the OperationsDatabase
capability, register SQL Server persistence and validate runtime options before
background work begins. It shall run as a Windows Service in deployment while
retaining console execution for development and tests.

The initial polling interval is 60 seconds. Each cycle queries only
`IsEnabled = true` targets whose `NextConnectivityAttemptAt` is null or due.
Infrastructure shall use `AsNoTracking`, a probe-specific projection and a
query-specific API; it shall not expose `IQueryable` or use an unbounded generic
repository list.

Concurrency shall be bounded at 20 target probes. Each transport attempt has a
10-second target-specific timeout and the combined `Auto` sequence has a
20-second budget. One target failure must not stop other targets. Shutdown
starts no new probe.

All current timestamps and time-aware delays use `TimeProvider`. New code must
not use `DateTime.Now`, `DateTime.UtcNow` or a custom clock abstraction.

## Collector host lifecycle

Startup composes and validates before the worker starts. Each cycle creates a
correlation scope, loads targets in a short scope, probes with bounded
concurrency, persists results independently, then awaits one cancellable delay.
Shutdown stops new work, propagates cancellation and performs bounded cleanup.
See
[`../collectors/WP-004-Windows-Collector-Architecture.md`](../collectors/WP-004-Windows-Collector-Architecture.md).

## Target loading and eligibility

Only enabled targets whose next eligible timestamp is absent or due are
projected with `AsNoTracking`. Disabled and not-due targets are not probed.
Configuration changes and re-enablement make the target eligible for the next
cycle under the state rules.

## Probe semantics

A probe verifies that an authenticated WinRM session can be opened with the
Windows Collector process identity. It executes no inventory or mutation
command. The adapter belongs in the Windows Collector behind a narrow
technology-neutral contract.

The selected approach is an in-process `System.Management.Automation` runspace
opened through WSMan and immediately disposed without invoking a pipeline.
Spawning `powershell.exe` and parsing localized output is rejected. Exact .NET
10 package/API compatibility must be verified before any package change; if it
fails, the WSMan COM/API alternative requires Architecture review.

The input is normalized FQDN, transport, port, timeout and cancellation. The
result contains outcome, transport, duration and safe category only. HTTPS
certificate validation remains enabled; credentials and TrustedHosts are never
managed by the application.

Detailed technology alternatives, package status, OS compatibility and
operational limitations are in
[`../collectors/WP-004-WinRM-Connectivity.md`](../collectors/WP-004-WinRM-Connectivity.md).

## WinRM transport modes

| Mode | Attempts | Rule |
|---|---|---|
| `Auto` | HTTPS, then eligible HTTP | Every normal cycle starts with HTTPS |
| `HttpsOnly` | HTTPS only | Never falls back |
| `HttpOnly` | HTTP only | Never attempts HTTPS |

Default ports are HTTPS 5986 and HTTP 5985. HTTPS success stops the probe.
Earlier HTTP success is recorded but never becomes a persistent preference.

## HTTPS-first behavior

Every normal `Auto` cycle starts with HTTPS. A previous HTTP success never
becomes a persistent transport preference. HTTPS success completes the probe.

## HTTP fallback behavior

`Auto` permits at most one HTTP attempt and only after a completed,
fallback-eligible HTTPS failure. HTTP is an explicit security/operations
decision; `HttpsOnly` never falls back and `HttpOnly` starts directly with HTTP.

## Fallback eligibility matrix

| HTTPS outcome/category | HTTP fallback in `Auto` | Reason |
|---|---:|---|
| Success | No | Complete |
| Host cancellation | No | Prompt shutdown |
| DNS/name resolution | No | Host name is common to both transports |
| Authentication failure | No | Avoid identity-wide logon storms |
| Authorization/access denied | No | HTTP cannot grant missing permission |
| TLS/certificate validation | Yes | HTTPS-specific failure |
| HTTPS listener unavailable | Yes | HTTP listener may exist |
| Connection refused | Yes | Port-specific failure |
| Timeout | Yes | HTTP may be reachable; attempts remain bounded |
| Network/firewall failure | Yes | Port rules may differ |
| Protocol/WSMan negotiation | Yes | Listener-specific possibility |
| Invalid target configuration | No | Local policy must be corrected |
| Unexpected/internal failure | No | Fail closed |

An HTTPS timeout may be followed immediately by HTTP if cancellation is not
requested. Each attempt and the combined probe must have bounded budgets.

## Timeout and cancellation

Each attempt and combined `Auto` sequence has a bounded timeout. Host
cancellation wins over timeout, prevents fallback and stops scheduling new
targets. Cleanup is bounded and deterministic; cancellation is neither target
failure nor an Error log.

## Failure classification

Persist only stable categories:

- `None`
- `DnsFailure`
- `AuthenticationFailure`
- `AuthorizationFailure`
- `TlsFailure`
- `WinRmUnavailable`
- `ConnectionRefused`
- `Timeout`
- `ProtocolFailure`
- `DatabaseUnavailable`
- `Cancelled`
- `Unexpected`

`DatabaseUnavailable` is a cycle/persistence category, not target state.
`Cancelled` is shutdown control flow. Neither is persisted on a target. Raw
exceptions, native messages and remote output are not persisted. In a failed
fallback sequence, the target row stores the final HTTP category; correlated
logs may record both safe attempt categories. HTTPS failure followed by HTTP
success persists `Reachable`, HTTP and `None`.

## Backoff rules

On success, state becomes `Reachable`; attempt and success timestamps are set;
successful transport is recorded; failure count becomes zero; failure category
is cleared; next attempt is the success timestamp plus the normal 60-second
polling interval.

On final failure, state becomes `Unreachable`; attempt time is set; last success
and last successful transport are preserved; count increments with overflow
protection; category is stored. Failure 1 uses normal polling (60 seconds),
failure 2 uses 5 minutes, failure 3 uses 15 minutes, failure 4 uses 30 minutes,
and failure 5 or later uses the 60-minute cap.

Failure count changes only when result persistence commits. Disabling a target
makes it ineligible. Enabling it or changing FQDN, port or transport policy
resets connectivity to `Unknown`, clears failure/backoff state and makes it
immediately eligible. Collector-host upgrade does not reset target state.
No jitter is introduced in the single-instance model.

## Minimum data-model change

`ManagedServer` remains the target aggregate. Its normalized `Fqdn`, optional
environment and enablement are reused. It is already a Windows-server entity,
so no target-type property is needed now.

| Property | SQL | Null/default | Decision |
|---|---|---|---|
| `WinRmTransportMode` | `nvarchar(20)` enum string | required; `Auto` | Readable repository standard |
| `WinRmHttpsPort` | `int` | required; `5986` | Per-target policy |
| `WinRmHttpPort` | `int` | required; `5985` | Per-target policy |
| `LastConnectivityState` | `nvarchar(20)` enum string | required; `Unknown` | Defined initial state |
| `LastConnectivityAttemptAt` | `datetime2(3)` | nullable | No initial attempt |
| `LastConnectivitySuccessAt` | `datetime2(3)` | nullable | No initial success |
| `LastSuccessfulTransport` | `nvarchar(10)` enum string | nullable | Unknown before success |
| `ConsecutiveConnectivityFailures` | `int` | required; `0` | Backoff input |
| `NextConnectivityAttemptAt` | `datetime2(3)` | nullable | Null means eligible |
| `LastConnectivityFailureCategory` | `nvarchar(40)` enum string | nullable | Cleared on success |
| `RowVersion` | `rowversion` | database generated | Protect policy and state |

Application/domain construction owns defaults. A future migration may use
temporary defaults to backfill existing rows, but shall not retain SQL defaults
or use SQL current time. Add port-range and non-negative-count constraints plus
an eligibility index beginning with `IsEnabled, NextConnectivityAttemptAt`.

Probe timeout is runtime behavior, not target state. Until a per-target override
is approved, it belongs in immutable typed host options. No timeout column or
migration is created in WP-004.1.

## Data model impact

The proposal is an additive change to `ManagedServer` plus one controlled
migration, checks and eligibility index. It adds no table, relationship,
credential column, history entity or target-type abstraction. The authoritative
state transitions and field proposal are in
[`../collectors/WP-004-Target-State-and-Backoff.md`](../collectors/WP-004-Target-State-and-Backoff.md).

## Existing entity and history decisions

- `CollectorRun` remains bounded inventory execution, not reachability state.
- `InventorySnapshot` is not a connectivity JSON bag.
- `CollectorHeartbeat` remains collector-process health.
- `CommandQueueItem` is not used for polling.
- Per-probe `AuditLog` records are not justified.
- Attempt history is deferred. Last-known state plus structured logs satisfies
  current operations need without unbounded retention scope.

Detailed probe semantics, technology comparison, loop and transaction design
are authoritative in
[`../collectors/WP-004-WinRM-Connectivity.md`](../collectors/WP-004-WinRM-Connectivity.md).

## Network requirements

The collector runs on Windows Server 2022 or later with the supported .NET 10
runtime and Windows Service prerequisites. Production uses the dedicated
Windows Collector gMSA with `Log on as a service`, least-privilege
OperationsDatabase access and outbound access to allowlisted WinRM targets.
DNS resolution and synchronized time are required. HTTPS requires a certificate
whose name and trust chain validate on the collector.

No local Event Log or additional service-control permission is a baseline
requirement. It is deployment-specific and may be granted only if an approved
logging or installation mechanism consumes it. The runtime identity receives no
installation, local-administrator or database DDL permission.

Targets may run Windows Server 2016, 2019, 2022 or 2025. WinRM must already be
enabled with the listener required by target policy: HTTPS with a valid
certificate, or HTTP only where explicitly approved. DNS, SPN and Kerberos
configuration must support Windows Integrated Authentication for the configured
FQDN. The gMSA receives only the minimum remote access needed to open and close
the authenticated probe session. The collector installs no target software and
creates no target database or table.

| Source | Destination | Protocol | Default port | Purpose | Requirement |
|---|---|---|---:|---|---|
| Windows Collector | Operations SQL Server | TCP | deployment-defined | Central database access | Required |
| Windows Collector | Windows target | WinRM HTTPS | 5986 | Preferred connectivity probe | `Auto`/`HttpsOnly` |
| Windows Collector | Windows target | WinRM HTTP | 5985 | Fallback or explicit HTTP probe | Conditional |
| Windows Collector | DNS | DNS | 53 | Name resolution | Required |
| Windows Collector | AD/DC | Kerberos and directory services | deployment-defined | Integrated authentication | Required |

All rules are explicit source-to-destination allowlists. SQL and AD/DC ports are
deployment-defined; WP-004 neither hard-codes the SQL port nor claims a broad
universal domain-controller port set. Custom WinRM ports replace the defaults
for that target. Deployment documentation records actual destinations, ports
and approved HTTP exceptions. No SMB, RDP, general RPC or non-WinRM remote
management path is assumed between collector and target.

## Deployment prerequisites

Collector/target OS, runtime, service identity, listener, certificate, firewall,
DNS/SPN/Kerberos and operational checks are authoritative in
[`../deployment/WP-004-Windows-Collector-Prerequisites.md`](../deployment/WP-004-Windows-Collector-Prerequisites.md).
The collector performs no target-side installation or database creation.

## OS and configuration-change behavior

WP-004 does not collect or infer OS, IIS, .NET, Windows Service or other
inventory. An OS upgrade may change those components, the listener,
certificate, DNS or endpoint availability; WP-004 observes only the outcome of
the next real connectivity probe.

Last attempt and last success remain separate. Current reachability is based on
the last completed and durably committed real probe, never an older success. A
failure sets current state to `Unreachable` while preserving last success and
last successful transport. A later success sets `Reachable`, resets failure
state and replaces next eligibility.

Changing FQDN, transport mode or either applicable port is an endpoint-policy
change. The domain update resets current state to `Unknown`, clears failure
count/category and `NextConnectivityAttemptAt`, and makes the target eligible in
the next non-overlapping cycle. This is not a probe result and does not erase
`LastConnectivitySuccessAt` or `LastSuccessfulTransport`. `Disabled` to
`Enabled` applies the same immediate-eligibility reset. Disabling preserves
last-known state but excludes the target regardless of eligibility timestamp.

An in-flight result carries the policy and rowversion that produced it.
Persistence reloads once on conflict and discards the result when FQDN, mode,
relevant port or enablement changed. Collector upgrade alone does not reset
state. A long outage naturally reaches the 60-minute cap.

## Target lifecycle and identity

Existing `ManagedServer.IsEnabled` is the complete WP-004 lifecycle. Hard
delete and a new `Decommissioned` state are out of scope; decommissioning is
represented by disabling the target.

The existing identity behavior remains binding:

- FQDN is preferred and required by the current `ManagedServer` contract.
- Input is trimmed, lower-cased invariantly and has trailing dots removed.
- `UX_ManagedServer_Fqdn` prevents duplicates after domain normalization.
- Short/NetBIOS names are not expanded into FQDNs or merged heuristically.
- IP targets are not introduced because FQDN and Integrated
  Authentication/Kerberos make IP identity unsafe and ambiguous.

No hostname-normalization framework is added. Future IP/NetBIOS support or
short-name/FQDN equivalence requires an explicit product and security decision.

## Concurrency behavior

Result persistence uses `rowversion` and never silently overwrites a policy
change. On conflict, reload once. Discard the stale result when the target was
disabled or policy changed; otherwise apply once to the new version. A second
conflict is logged and deferred.

EF provider retry handles recognized transient database errors for one
operation. Exhaustion ends only the current step. Persistence failure must not
re-probe the same target in the same cycle. The next cancellable cycle retries
normal work.

## Result persistence

Each final probe result uses a fresh scope and short transaction. Success and
failure follow the state rules; cancellation and database unavailability
produce no target failure. One target save cannot roll back another. Raw
exception, stack trace, credential, certificate or remote output is never
stored.

## Security

- Run under the dedicated Windows Collector identity/gMSA.
- Use Windows Integrated Authentication for database and target access.
- Never accept, store, log or distribute credentials.
- Never combine Windows and SQL target permissions.
- Never bypass TLS validation or modify TrustedHosts.
- Add no remote action interface.
- Grant only target-read and connectivity-state-update database permissions;
  runtime identity receives no DDL permission.

## Logging and correlation

Reserve event IDs `2300`–`2399` for Windows Collector behavior. Stable events
shall cover service/cycle lifecycle, target-load failure, probe completion,
fallback, result persistence and concurrency conflict.

Every cycle has a correlation ID. Target logs may include normalized FQDN,
target ID, transport, duration, safe category and outcome. They must not include
connection strings, credentials, certificate detail, raw exception messages or
remote output.

## Risks

| Risk | Treatment |
|---|---|
| PowerShell SDK is incompatible with .NET 10 service hosting | Verify before package change; review native WSMan alternative |
| HTTP weakens transport protection | Explicit per-target approval, HTTPS-first, deployment allowlist |
| Slow targets starve cycles | Bounded timeout and bounded concurrency |
| Stale results overwrite changed policy | ManagedServer rowversion and one reload/discard rule |
| Persistent failure floods logs | Backoff, aggregate skips and safe levels |
| Single instance accidentally duplicated | Document operational assumption; no false leasing claim |

## ADR decision

**No new ADR required.** The proposed WinRM adapter, fallback policy and target
backoff are bounded WP-004 design choices. They do not change a repository-wide
security boundary or introduce an irreversible platform dependency. ADR-001 and
ADR-003 remain binding, and package compatibility is deliberately left for
implementation evidence rather than prematurely fixed in an ADR.

## Test strategy

### Unit and deterministic-time tests

Cover every transport mode and fallback-matrix row, including HTTPS-first,
success short-circuit, DNS/authentication/authorization short-circuit, eligible
TLS/timeout/refused/protocol fallback, cancellation, and HTTPS failure followed
by HTTP success without a persisted intermediate failure. Cover guards, result
mapping, disabled/not-due skips, timestamp separation, exact capped backoff,
success and configuration resets, and redaction sentinels.

Use `TimeProvider` with fake time. Do not use `Thread.Sleep`, wait real minutes
or make backoff tests depend on real-time `Task.Delay`.

### Host integration tests

Exercise Generic Host startup, Windows Service registration, WP-003 provider
composition, OperationsDatabase registration, missing connection string and SQL
Authentication fail-fast, scoped DbContext creation, graceful cancellation,
non-overlapping cycles and healthy empty-target cycles. Prove that the singleton
worker captures no scoped DbContext and cancellation is not logged as Error.

### Persistence integration tests

Cover enabled/due projection, disabled exclusion, mapping, constraints,
eligibility index, timestamp rules, failure increment, success reset, next
eligibility, safe category, raw-exception exclusion and rowversion. Prove one
bounded conflict reload, stale-result discard after policy change, isolated
commit failure and absence of automatic migration.

### WinRM tests

Default CI uses fake transports. Optional real WinRM tests are Windows-only and
environment-gated, contain no repository hostname, credential or secret, and do
not fail the default suite without a lab. Local/dedicated targets and HTTPS
certificate scenarios are explicitly opt-in and controlled. Automated tests
never perform privileged remote actions.

### Architecture, logging and redaction tests

Source/model scans prove absence of credential properties/options/columns, SQL
Authentication, `PSCredential`, explicit username/password, TrustedHosts
mutation, certificate bypass, remote commands/actions, Web CRUD, inventory
expansion, SQL Collector changes, direct system time/custom clocks, automatic
migration, new generic repository/plugin frameworks, unbounded `Task.WhenAll`
and scoped DbContext capture.

Logging tests prove connection strings, password/credential sentinels, Kerberos
details, certificate details and raw exception messages are absent; fields are
allowlisted; cancellation is not Error; success/backoff/permission failures do
not flood logs; and event IDs do not collide.

## Acceptance criteria

### Host, configuration and lifecycle

1. Windows Collector runs as a Generic Host with Windows Service support,
   stops gracefully without cancellation Error logs and never overlaps cycles.
2. Each completed cycle uses cancellable delay; target/transient SQL failures
   are isolated without process termination or tight retry.
3. WP-003 composition validates OperationsDatabase fail-fast. Missing connection
   string or SQL Authentication stops startup without logging the string.
4. `OperationsDbContext` is scoped and never captured by the singleton worker.
   Controlled migrations never run automatically at startup.
5. Only enabled, due targets load through a cancellable query. No targets is
   healthy; disabled targets keep last-known state and are not probed.
6. Existing FQDN normalization and uniqueness remain; hard delete,
   decommission state, IP and NetBIOS identity are not added.
7. FQDN/mode/relevant-port changes and re-enablement reset current/backoff state
   for next-cycle immediate eligibility while preserving last-success facts.

### Transport and security

8. Every normal `Auto` probe starts with HTTPS; HTTPS success stops work and
   only fallback-eligible HTTPS failures attempt HTTP.
9. `HttpsOnly` never uses HTTP, `HttpOnly` never uses HTTPS, defaults are
   5986/5985 and configured ports are honored.
10. Cancellation stops fallback; probe/cleanup are bounded and prove
    authenticated WinRM without pipeline, inventory or remote mutation.
11. The dedicated gMSA and Windows Integrated Authentication are used. No
    credential, SQL Authentication, `PSCredential`, certificate bypass,
    TrustedHosts mutation or WinRM mutation exists.
12. Network prerequisites are deployment allowlists with no hard-coded SQL
    port, broad AD/DC port claim, SMB, RDP, general RPC or other protocol.
13. HTTP risk is documented, HTTPS chain/name validation stays enabled and
    Windows/SQL Collector permissions remain separated.

### State, backoff and persistence

14. A committed probe updates current reachability/last attempt. Success alone
    updates last success/transport; failure preserves them.
15. HTTP fallback success is final `Reachable` over HTTP and the intermediate
    HTTPS failure is not persisted.
16. Only committed final target failures increment the count; cancellation,
    disablement and database unavailability do not.
17. Success resets failure state. Eligibility uses `TimeProvider`
    deterministically and backoff never exceeds 60 minutes.
18. Safe category/count/eligibility persist; raw exception, stack trace,
    credential/authentication material and certificate detail do not.
19. Database unavailability is not target failure and per-target persistence
    failures are isolated as far as possible.
20. Rowversion handling reloads at most once, discards stale policy results and
    never retries infinitely.

### Observability, scope and quality

21. Every cycle is correlated, event IDs are repository-unique and structured
    logging uses allowlisted fields.
22. Safe logs exclude connection strings, credentials, Kerberos/certificate and
    raw sensitive exception detail; cancellation and repeated outcomes do not
    flood inappropriate levels.
23. No inventory expansion, Web CRUD, queue/executor/action, target install or
    database/table, SQL Collector change, multi-instance coordination, runtime
    plugin or generic repository framework is added.
24. Unit, host, persistence, fallback, deterministic-time, redaction and
    architecture tests pass; real WinRM tests remain optional and gated.
25. Release build has zero warnings/errors; format, vulnerable-package, scope
    diff and documentation checks are clean.

## Sprint breakdown

1. **WP-004.1 — Analysis and documentation (complete).** Repository/model gap, technology,
   lifecycle, failure, backoff, security, network, acceptance and plan.
2. **WP-004.2 — Collector host foundation (complete).** Generic/Windows Service
   hosting, WP-003/OperationsDatabase composition, scoped persistence
   registration, immutable 60-second polling options, non-overlapping no-op
   lifecycle, safe correlation/logging and host tests. No target query, WinRM
   or result persistence is included.
3. **WP-004.3 — Target provider and eligibility (complete).** Add the nullable
   `NextConnectivityAttemptAt` read prerequisite and eligibility index through a
   controlled migration, then load enabled/due targets into a minimal immutable
   projection with cancellation and no tracking. No network work or state
   mutation is included.
4. **WP-004.3A — Target connectivity configuration model (complete).** Add
   target-specific mode, HTTPS/HTTP ports and timeout with controlled migration,
   validation and eligible-target projection support. It adds no WinRM runtime.
5. **WP-004.4 — WinRM connectivity probe (complete).** Added the in-process
   adapter, HTTPS-first modes, conditional fallback, timeout, cancellation,
   deterministic cleanup, classification and bounded parallel orchestration.
   It produces results but does not own durable transitions.
6. **WP-004.5 — Backoff and result persistence (complete).** Added transitions,
   deterministic scheduling, independent commits, rowversion conflict policy,
   correlation, logging and redaction. It consumes results but does not choose
   transport.
7. **WP-004.6 — Final review and commit readiness (complete).** Acceptance,
   architecture, security, scope, documentation and full quality gates passed.

Each slice has one bounded objective and independently testable evidence. The
schema gap was a proven prerequisite. WP-004.3 added the nullable next-attempt
field and WP-004.5 added state plus rowversion with the mutation boundary. No
separate Work Package
is justified because these additive changes remain bounded to this connectivity
contract.
WP-004.4 owns how a probe result is produced; WP-004.5 owns its durable effect.
Each implementation slice requires separate authorization.

## Open questions

None.

The repository evidence resolves the remaining analysis questions:

- `ManagedServer` is the correct aggregate; controlled migrations and its own
  rowversion are implemented.
- Connectivity state belongs on that aggregate; no history table or existing
  collection entity is reused.
- Implemented technology is an in-process PowerShell runspace over WSMan, with
  .NET 10 build/publish proof and a Windows Collector-only package reference.
- Windows Service hosting uses the official Microsoft hosting integration.
- OperationsDatabase selection uses the existing
  `AddOperationsDatabasePersistence` extension.
- Polling remains a host option with a 60-second default. Target timeout is
  persisted per target with a 10-second default. WP-004.4 maximum concurrency is
  20 and the combined `Auto` budget is 20 seconds.
- DNS, authentication and authorization do not fall back. TLS, refused,
  listener, timeout and protocol failures are eligible in `Auto`.
- Bounded concurrency, per-result DbContext scope and per-target save are
  required; sequential and batch persistence are rejected.
- Existing FQDN normalization is retained; IP is not accepted.
- FQDN/mode/port change and re-enablement reset backoff.
- Event IDs `2300`–`2399` are reserved.

Non-blocking follow-up: restore or formally replace the missing ADR-005 source
document through its governance owner.

## Definition of Done

- [x] WP-004 is approved and every open implementation input is decided.
- [x] Each implementation slice meets its independent acceptance evidence.
- [x] Release build and all required automated tests pass with zero warnings.
- [x] Optional lab verification is documented and remains outside default CI.
- [x] Security, architecture, scope and documentation reviews pass.
- [x] No automatic migration, credential, action or inventory expansion exists.
- [x] Controlled migration/deployment artifacts are reviewed separately.
- [x] Final diff contains only approved WP-004 scope.

## Final review evidence

WP-004.6 completed on 2026-07-27 with .NET SDK 10.0.301. Solution restore,
Release build, all 215 automated tests, format verification, transitive package
vulnerability scan and Windows Collector Release publish passed. Build output
reported zero warnings and zero errors. Migration/model, event-ID, redaction,
architecture and scope assertions are included in the passing suites. The final
Git diff check is clean; optional real Windows Service and WinRM environment
tests remain documented, explicit opt-in procedures and were not executed.

## References

- [`../index.md`](../index.md)
- [`../project/Principles.md`](../project/Principles.md)
- [`../collectors/WP-004-Windows-Collector-Architecture.md`](../collectors/WP-004-Windows-Collector-Architecture.md)
- [`../collectors/WP-004-WinRM-Connectivity.md`](../collectors/WP-004-WinRM-Connectivity.md)
- [`../collectors/WP-004-Target-State-and-Backoff.md`](../collectors/WP-004-Target-State-and-Backoff.md)
- [`../database/WP-004-Connectivity-Model-Gap-Analysis.md`](../database/WP-004-Connectivity-Model-Gap-Analysis.md)
- [`../deployment/WP-004-Windows-Collector-Prerequisites.md`](../deployment/WP-004-Windows-Collector-Prerequisites.md)
- [`../security/WP-004-Windows-Collector-Security.md`](../security/WP-004-Windows-Collector-Security.md)
- [`../adr/ADR-001-Pragmatic-Clean-Architecture.md`](../adr/ADR-001-Pragmatic-Clean-Architecture.md)
- [`../adr/ADR-003-Collector-Separation-by-Security-Boundary.md`](../adr/ADR-003-Collector-Separation-by-Security-Boundary.md)
- [`WP-002-Core-Persistence-Layer.md`](WP-002-Core-Persistence-Layer.md)
- [`WP-003-Configuration-Management.md`](WP-003-Configuration-Management.md)

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.7.0 | 2026-07-27 | Completed WP-004.6 final review, acceptance closure and commit-readiness checks |
| 1.6.0 | 2026-07-27 | Recorded completed WP-004.5 state persistence/backoff and WP-004.6 as next sprint |
| 1.5.0 | 2026-07-27 | Recorded completed WP-004.4 connectivity probe and WP-004.5 as next sprint |
| 1.4.0 | 2026-07-27 | Recorded completed WP-004.3A model/projection correction and resolved WP-004.4 inputs |
| 1.3.0 | 2026-07-27 | Recorded completed WP-004.3 target provider, eligibility read path and controlled migration |
| 1.2.0 | 2026-07-27 | Recorded completed WP-004.2 host foundation and authorized sprint sequence |
| 1.1.0 | 2026-07-27 | Added prerequisites, lifecycle/change behavior, tests, sprint validation and consolidated acceptance criteria |
| 1.0.0 | 2026-07-27 | Completed WP-004.1 analysis and implementation design |
