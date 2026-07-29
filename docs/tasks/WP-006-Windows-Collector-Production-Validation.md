---
title: WP-006 - Windows Collector Production Validation
version: 0.5.0
status: Approved for Controlled Execution
owner: Engineering
last_updated: 2026-07-28
work_package_id: WP-006
related_work_packages: [WP-004, WP-005, WP-005.S1, WP-005.S2]
reviewers: [Architecture, Operations, Security]
product: PSM Operations Platform
---
# WP-006 Windows Collector Production Validation

## Status

WP-006.1 analysis review and decision closure is complete. The validation
design is approved to proceed to WP-006.2 Controlled Single-Target Execution.
WP-006.2 execution planning and tooling preparation are complete. No live
validation has been completed, and WP-006.2 itself is not complete.
WP-006.2A deployment tooling and its operator runbook are implemented; live
deployment remains a separately approved action.

WP-006.2A-C3 adds a mandatory read-only Operations Database schema and
permission gate between stopped service registration and CollectorHost
readiness. Database `NOT_READY` blocks service start and cannot be overridden.
The gate applies no migration and performs no repair.

The authoritative WP-006.2 execution package is:

- `docs/testing/WP-006.2-Controlled-Single-Target-Execution-Runbook.md`;
- `docs/testing/templates/WP-006.2-Execution-Manifest.template.json`;
- `docs/testing/templates/WP-006.2-Evidence-Index.template.md`; and
- the four WP-006.2-specific scripts under `tools/validation`.

The authoritative WP-006.2A deployment package is
`docs/deployment/WP-006.2A-Windows-Collector-Deployment-Runbook.md` and the
Windows Collector-specific scripts under `tools/deployment`. Deployment
completion and evidence review are prerequisites to controlled execution.

WP-006 validates the Collector as currently implemented. Independently
committed ownership boundaries, partial target success and mixed boundary
freshness are accepted validation subjects, not analysis blockers. Runtime
hardening is deferred unless execution discovers a defect that makes a
mandatory criterion objectively impossible to validate.

The reported CollectorHost result is `WARNING`, exit code `1`, because Windows
Server 2019 is permitted for non-certifying controlled-lab behavior validation.
WP-006.2 may proceed on that approved host when the exception is documented.
Production certification still requires Windows Server 2022 or later, x64.

## Purpose

Define and execute a controlled, repeatable, repository-grounded program that
validates the real Windows Collector as implemented. Later phases must prove
service hosting, configuration, gMSA identity, WinRM lifecycle, all WP-005
modules, SQL current-state persistence, failure handling, logging, shutdown,
repeatability and resource stability.

WP-006 uses current structured logs, existing tests, SCM/process evidence,
read-only SQL exports, external operating-system counters and timestamped
execution manifests. It does not redesign runtime behavior before controlled
validation. Findings that would improve production operation are deferred to
Enterprise Runtime Hardening unless an actual blocking defect is discovered.

## Repository Evidence

### Verified delivery state

| Area | Repository evidence | Assessment |
|---|---|---|
| WP-001 | Solution structure and architecture tests | Implemented |
| WP-002 | `OperationsDbContext`, initial migration, model/persistence tests | Implemented |
| WP-003 | ordered configuration composition, validation and redaction tests | Implemented |
| WP-004 | service host, target query, WinRM probe, connectivity state/backoff | Implemented; production validation absent |
| WP-005 | seven modules, ownership stores, migration and tests | Implemented; final review recorded passed |
| WP-005.S1 | prerequisites, runbook, matrix, evidence and SQL queries | Preparation complete; live phase not executed |
| WP-005.S2 | `tools/readiness` and Pester tests | Implemented; later uncommitted corrective edits exist |
| WP-005.S2A/S2B | Current worktree contains readiness edits | Not claimed complete: no approved package documents identify these milestones |

The repository was on `main`. Pre-existing modified/untracked readiness,
release and deployment files were preserved.

### Binding evidence

- `docs/project/Principles.md` requires least privilege, observable behavior,
  one source of truth and operational safety.
- ADR-003 requires separate Windows and SQL Collector identities.
- ADR-006 defines logical ownership boundaries, Türkiye-local `CapturedAt`,
  independent module commits and atomic Network Adapter plus IPv4 replacement.
- `docs/deployment/WP-004-Windows-Collector-Prerequisites.md` requires a
  production Collector host on Server 2022 or later.
- WP-005.S1 establishes the non-production execution gate and prohibits inferred
  values, credential material, certificate bypass and target mutation.

Source evidence includes `Program`, `WindowsCollectorHost`, `Worker`,
`WindowsCollectorCycle`, `WindowsTargetProvider`, `WindowsConnectivityProbe`,
`WinRmTransportClient`, `WinRmFailureClassifier`, `PowerShellWinRmSession`,
`WindowsInventoryOrchestrator`, the seven modules, `WindowsInventoryStores`,
`OperationsDbContext`, inventory mappings and migration
`20260727230000_AddWindowsInventoryCurrentState`.

## Current Runtime Flow

```text
SCM/executable start
  -> build host and validate configuration shape
  -> Worker continuous polling loop
  -> PollingCycleId and scoped WindowsCollectorCycle
  -> load enabled/due ManagedServer rows from SQL
  -> probe up to 20 targets concurrently
  -> target-specific WinRM policy and reusable session
  -> persist connectivity result
  -> on AppliedSuccess, run seven ordered inventory modules
  -> each module persists its own ownership boundary
  -> dispose session in cycle finally
  -> PollingInterval delay and repeat until cancellation
```

### Analysis answers

1. Collection starts immediately in `Worker.ExecuteAsync` after host start.
2. It is continuous scheduled polling; default interval is 60 seconds. There is
   no one-shot or external-trigger path.
3. `WindowsTargetProvider` queries `configuration.ManagedServer` for enabled
   rows whose `NextConnectivityAttemptAt` is null or due.
4. A polling cycle has an in-memory `PollingCycleId`; each reachable target
   inventory attempt has an in-memory `InventoryCorrelationId`; target identity
   is `ManagedServer.Id`.
5. `ops.CollectorRun` and `inventory.InventorySnapshot` exist, but this runtime
   uses neither. There is no persisted Windows inventory execution record.
6. Computer, Operating System and Memory use target-primary-key upsert and
   update `CapturedAt`.
7. Processor, Disk, Volume and Network materialize/validate input, delete
   target-owned rows and insert the latest set. Successful empty sets clear the
   boundary.
8. Snapshot tables are:
   `inventory.WindowsComputerInventory`,
   `inventory.WindowsOperatingSystemInventory`,
   `inventory.WindowsMemoryInventory`,
   `inventory.WindowsProcessorInventory`,
   `inventory.WindowsDiskInventory`,
   `inventory.WindowsVolumeInventory`,
   `inventory.WindowsNetworkAdapterInventory` and
   `inventory.WindowsIpv4AddressInventory`.
9. Each plural store replacement is transactional. Network deletes IPv4 before
   adapters and inserts adapters before IPv4 in one transaction. Singular saves
   are individual. There is no target-wide transaction; Disk and Volume are
   independent.
10. A module exception is recorded and later modules continue if the session is
    usable. Earlier commits remain. Failed/cancelled plural replacement rolls
    back. If the session becomes unusable, remaining modules do not run.
11. Connectivity persistence failure prevents inventory. Inventory persistence
    failure becomes a module failure; later modules may continue, and earlier
    commits remain.
12. WinRM open failure is classified; any created failed session is disposed;
    connectivity failure/backoff is persisted; inventory does not run.
13. Authorization failure is `AuthorizationFailure` and never falls back.
14. `Auto` fallback permits only TLS failure, connection refused, timeout,
    WinRM unavailable and protocol failure.
15. Authentication, authorization, DNS, cancellation and unexpected failure
    prohibit fallback. `HttpsOnly`/`HttpOnly` never switch transport.
16. SQL EF execution strategy retries provider-classified transient errors up to
    three times with maximum ten-second delay. Connectivity rowversion conflict
    gets one retry. Failed connectivity is deferred by polling interval, then
    5, 15, 30 and at most 60 minutes.
17. No WinRM open, remote-command or inventory-module retry occurs within a run.
18. Host cancellation flows through Worker, target query, parallel work, probe,
    command invocation and EF calls. Command cancellation calls
    `pipeline.Stop()` and stops later modules.
19. Failed-open sessions are disposed by `WinRmTransportClient`. Successful
    ownership transfers to `WindowsCollectorCycle`, which disposes in `finally`.
20. Fakes count disposal in tests. Real runspace/session counts are not exposed.
21. Stable structured events cover service, polling, target load/probe,
    connectivity persistence, inventory/module execution and summaries.
22. Target ID, polling/inventory correlation, module, transport, durations and
    summaries exist. Persisted run ID, boundary persistence duration/result,
    retry count and service-wide final summary do not.
23. Collector logs use allowlisted identifiers/categories/exception types.
    Configuration and persistence have secret-sentinel tests. Actual sink
    output still needs verification.
24. Target-load, probe, module and inventory durations; probe/result counts;
    selected transport; module failures; connectivity outcomes and EF affected
    row counts are measurable.
25. Tests do not prove SCM/gMSA/Kerberos/certificate behavior, real WSMan
    disposal, SQL Server retry/transaction behavior, sink retention/redaction,
    repeated resource stability or production-supported-host behavior.

## Scope

Validate the existing read-only Windows Collector on one approved controlled-lab
target, then its independently committed persistence boundaries, repeatability,
partial-success behavior, failures, operational evidence and resource
stability.

WP-006 evidence uses the in-memory `PollingCycleId` and
`InventoryCorrelationId`, service logs, timestamped evidence directories,
read-only SQL before/after exports and an external WP-006 execution manifest.
Only one active Collector instance is permitted during initial validation.

## Out of Scope

- Live execution during WP-006.1.
- Production targets/databases or Server 2019 production certification.
- New inventory, history, schema, migrations or generic run framework.
- Integration of `ops.CollectorRun` or `inventory.InventorySnapshot`.
- Target-wide inventory transactions or changed module continuation behavior.
- New logging events, sinks or runtime observability instrumentation.
- Identity consolidation or privileged actions.
- Listener, firewall, certificate, TrustedHosts, AD or gMSA remediation.
- Unapproved service control, deployment or cleanup.
- Scale certification at 1,000 targets.
- Linux, cloud, Kubernetes, multi-tenancy or plugin SDK concepts.

## Risks

| Risk | Effect | Control |
|---|---|---|
| Partial target success | Independently committed boundaries can have mixed freshness | Record every boundary outcome and compare per-table state/time |
| No persisted run | Correlation depends on external evidence | Use cycle/correlation IDs, timestamped manifest, logs and SQL snapshots |
| Success detail is Debug | Default Information hides cycles/modules | Use documented, access-controlled lab Debug profile and restoration procedure |
| No inventory retry | Transient failure waits for future cycle | Test/document; do not invent retry |
| Real SQL behavior unproven | SQLite/fakes do not prove execution strategy | Dedicated SQL Server integration |
| Runspace visibility absent | Disposal call may not prove no leak | Combine disposal tests with repeated external process/session evidence |
| Concurrent instances | Last-writer behavior is undefined | Permit exactly one active Collector during initial validation |
| Dirty worktree | Unrelated edits may be attributed to WP-006 | Keep diff/status attribution explicit |

## Validation Phases

### WP-006.1 Production Validation Analysis

- **Purpose:** approve this design and close the analysis decisions.
- **Inputs:** repository, ADRs, tests, runbooks and readiness result.
- **Implementation needs:** documentation only.
- **Tests/live steps:** reference/diff checks; no live work.
- **Evidence:** reviewed plan and decision log.
- **Entry/exit:** WP-005 evidence exists; this revision exits WP-006.1 when
  approved decisions are recorded consistently and no contradictory blocking
  prerequisite remains.
- **Risks/out of scope:** planned behavior must not be claimed implemented; no
  product/infrastructure changes.

### WP-006.2 Controlled Single-Target Execution

- **Purpose:** prove SCM, configuration, gMSA, target selection, WinRM, module
  order, disposal and shutdown.
- **Inputs:** approved host/artifact/service/gMSA/SQL/target, readiness,
  timestamped evidence directory and execution manifest.
- **Implementation needs:** none. Use the current runtime and a documented,
  temporary, lab-scoped Debug logging profile with restoration procedure.
- **Tests/live steps:** host tests; one SCM-controlled cycle and graceful stop.
- **Evidence:** readiness reports, service metadata, repository/artifact hashes,
  current structured logs, execution manifest, process evidence and read-only
  SQL before/after exports.
- **Entry:** WP-006.1 revision approved; repository commit and artifact hash
  recorded; readiness is `READY` or an explicitly approved `WARNING`; any
  Server 2019 lab-only exception is documented; exactly one approved
  `ManagedServer` is enabled and due and all others are excluded through
  approved data configuration; service account, SQL database, migration,
  transport policy and HTTP fallback permission are verified; read-only SQL
  queries, lab logging profile/restoration, evidence directory, maintenance
  window, approver and rollback owner are recorded; no production system is in
  scope; and only one Collector instance is active.
- **Exit:** service starts under the expected account and remains operational
  for the approved observation period; the approved target is selected; WinRM
  follows its transport policy; one reusable target session is used where the
  current design requires; all seven modules execute in deterministic order;
  each module outcome is identifiable; successful ownership boundaries persist
  valid current state; any failed boundary and resulting partial target success
  are explicit; Network Adapter and IPv4 remain atomic and orphan-free; SQL has
  no invalid singular or stable-key duplicates; service stops gracefully; logs
  contain no secrets; and every deviation is recorded rather than hidden as
  success.
- **Risks/out of scope:** accidental extra targets; no forced failures.

A clean happy-path WP-006.2 run is mandatory before WP-006.3 or any
failure-injection work begins. For that qualifying happy path, all seven module
outcomes must be successful. A failed boundary is recorded as a deviation and
partial target success, then the happy path must be repeated after approved
recovery; it is not hidden as a passing clean run.

### WP-006.3 Database Persistence Verification

- **Purpose:** prove real SQL schema, permissions and ownership transactions.
- **Inputs:** WP-006.2 evidence and migration proof.
- **Implementation needs:** dedicated SQL integration fixture.
- **Tests/live steps:** initial insert, upsert, replacement, rollback, FK,
  execution strategy; read-only post-run queries.
- **Evidence:** query exports, counts, timestamps and integrity report.
- **Entry/exit:** migration/permissions verified; exit with complete current
  state and no duplicates/orphans.
- **Risks/out of scope:** no schema change or production SQL.

### WP-006.4 Repeatability and Current-State Semantics

- **Purpose:** prove second/later runs update without invalid duplication.
- **Inputs:** known baseline and approved repeat count.
- **Implementation needs:** external snapshots/comparison harness.
- **Tests/live steps:** two-run upsert/replace, empty-set test double, target
  isolation and controlled concurrency.
- **Evidence:** hashed before/after exports.
- **Entry/exit:** complete baseline; exit with singular count one, latest
  stable-key sets and unchanged unrelated data.
- **Risks/out of scope:** natural source changes; no database history.

### WP-006.5 Failure Classification and Recovery

- **Purpose:** prove fallback, retry/no-retry, cancellation and preservation.
- **Inputs:** success baseline and isolated controls.
- **Implementation needs:** none assumed. Use existing mocks and isolated lab
  controls. If a mandatory case cannot be proved safely, record the exact gap
  for separate approval rather than silently adding a seam.
- **Tests/live steps:** mocks/database tests first, then approved natural/isolated
  lab cases and recovery cycle.
- **Evidence:** attempt/category logs, before/after data and cleanup.
- **Entry/exit:** rollback owner present; all executed cases match policy.
- **Risks/out of scope:** no TLS weakening or real wrong credentials.

### WP-006.6 Logging and Operational Evidence

- **Purpose:** prove actual logs are correlated, retained, useful and safe.
- **Inputs:** successful/failure executions and sink decision.
- **Implementation needs:** none before initial controlled validation. Use
  current events and external evidence; record exact insufficiencies for
  deferred hardening.
- **Tests/live steps:** stable fields, sentinel redaction and actual sink scan.
- **Evidence:** field coverage and redaction report.
- **Entry/exit:** the lab capture method, access controls and restoration are
  approved; exit when operators can correlate startup/shutdown, polling cycle,
  target, transport, module order/outcome, duration and final inventory outcome
  without secrets, or when each unprovable field is documented precisely.
- **Risks/out of scope:** evidence itself needs restricted access; no centralized
  platform selection.

### WP-006.7 Performance and Resource Stability

- **Purpose:** measure startup, target duration and repeated resource trends.
- **Inputs:** stable topology, repeat-count and sampling decisions.
- **Implementation needs:** process/WinRM/SQL observation method.
- **Tests/live steps:** 10, 25 and conditionally 50 runs; approved cancellation.
- **Evidence:** CSV/JSON samples and trend analysis.
- **Entry/exit:** functional pass; no sustained unbounded growth. Numeric limits
  are **DECISION REQUIRED**.
- **Risks/out of scope:** host noise; no scale/capacity promise.

### WP-006.8 Final Production Validation Review

- **Purpose:** decide lab pass, certification, conditional approval or reject.
- **Inputs:** evidence index, decisions and deviations.
- **Tests/live steps:** independent completeness review; no live work.
- **Evidence:** review record and residual risks.
- **Entry/exit:** tests completed or justified Not Executed; named reviewers
  approve the defined outcome.
- **Risk/out of scope:** lab pass must not be overstated as deployment approval.

## Validation Matrix

`Both` means automated evidence precedes separately approved lab evidence.

### A. Service and startup

| Scenario | Expected result | Method | Class |
|---|---|---|---|
| Registration/path/account | Exact service, hashed executable, safe quoting and Windows Collector gMSA | readiness/service metadata | Lab |
| Starts/remains running | Running plus event 2300 for approved observation period | SCM/process/log | Lab; period **DECISION REQUIRED** |
| Valid startup | configuration shape validates safely | host/config tests and log | Both |
| Missing/malformed connection | startup validation fails with stable safe code | existing tests | Automated |
| SQL unreachable at startup | shape can validate; first target load fails safely; future cycles continue | isolated SQL test | Both |
| Graceful stop | cancellation, event 2301 and process exit | Worker tests/SCM | Both |
| Cancel during collection | later modules stop, transaction rolls back, disposal attempted | fakes, optional lab | Both |
| Restart clean/after failure | new process polls normally; failure does not poison Worker | Worker tests/SCM | Both |

### B. WinRM connectivity

| Scenario | Expected result | Class |
|---|---|---|
| HTTPS success | one HTTPS attempt; reusable session | Both |
| TLS/refused/timeout/unavailable/protocol then HTTP success | `Auto` only; HTTP selected within shared budget | Both |
| Authentication/authorization/DNS | no fallback | Automated; DNS also lab |
| Cancellation/unexpected | no fallback; safe cancellation/type | Automated |
| Target unavailable | failure/backoff; old inventory retained | Both |
| Session disposal | failed opens disposed; successful session disposed once | Automated |
| No obvious session leak | disposal tests plus external process/WSMan evidence show no accumulating resources | Lab |

### C. Inventory collection

| Scenario | Expected result/evidence |
|---|---|
| Computer | Computer System plus BIOS projections; one target row |
| Operating System | required identity fields and optional dates |
| Memory | non-negative total physical memory |
| Processor | every unique DeviceID; multiple processors supported |
| Disk/Volume | independent replace-all; multiple and empty supported |
| Network/IPv4 | one atomic IPv4-only normalized snapshot |
| Disabled/disconnected/virtual adapters | persisted if returned; no repository filter |
| APIPA/loopback | persisted if returned as canonical IPv4; no repository filter |
| Optional data | null allowed only for mapped nullable fields |
| Multiple children | one row per unique stable key |
| Empty children | successful empty clears owned collection |
| Order | Computer, OperatingSystem, Memory, Processor, Disk, Volume, NetworkAdapter |
| Module failure | type logged; continue if session usable; failed boundary preserves implemented prior state; partial success explicit |

Module tests plus source-to-SQL lab comparison supply evidence. Natural edge
cases may be `Not Executed`; do not mutate a target merely to manufacture them.

### D. SQL persistence

| Scenario | Expected result | Evidence |
|---|---|---|
| Initial/second run | complete insert; singular remains one; stable keys unique | SQL exports |
| Singular update | values and `CapturedAt` update | test plus SQL |
| Replace-all/empty | latest set only; empty clears; target isolation | tests plus SQL |
| Network atomicity/order | one transaction; delete IPv4 before adapters | failure test/join |
| Restrict/NoAction | dependent parent deletion rejected | model/store tests |
| Rollback | prior boundary remains after failure/cancellation | SQL integration |
| Execution strategy | transient transaction delegate replays safely | SQL Server integration |
| Timestamp/Türkiye | application-local `datetime2(3)` matches recorded host clock | readiness/query |
| SQL interruption/permission | safe exhausted failure, no secret, transactional preservation | isolated integration |
| Partial target success | earlier successful boundaries remain; failed plural boundary rolls back; later behavior follows session usability | module logs, manifest and per-table export |
| Concurrent execution | excluded from initial WP-006: exactly one active Collector instance | service/process manifest |

### E. Failure handling

| Scenario | Expected result |
|---|---|
| Retryable SQL transient/timeout | provider execution strategy retries; actual count evidenced |
| Authentication/authorization | non-retryable in-run; no fallback |
| DNS/invalid target | DNS category, no fallback, persisted backoff |
| SQL permission | safe failure; no connection string/statement |
| Remote command/data mapping | module failure; destructive replace not begun for invalid input |
| Cancellation | no retry; normal propagation; disposal |
| Unexpected | exception type only; other targets/cycles continue |
| Recovery | later due cycle succeeds and service remains healthy |

### F. Logging and evidence

| Requirement | Current evidence/gap |
|---|---|
| Service startup/shutdown | Information events 2300/2301 |
| Target start/final | reconstruct from target probe and inventory events; exact insufficiency is deferred |
| Module start/completion/duration | Debug 2351/2352 |
| Transport/fallback | selected transport exists; successful fallback reason may require external test precondition evidence |
| SQL persistence | connectivity/generic EF events plus before/after SQL prove persisted state; duration is not explicit |
| Run/target IDs | cycle/inventory GUID and target GUID correlated through timestamped execution manifest |
| Error/final summary | categories and partial summaries; no whole-cycle inventory summary |
| Redaction | allowlisted events/sentinel tests; actual sink must be scanned |
| Event Log/file | use the approved lab capture method; final production sink remains **DECISION REQUIRED** |

### G. Performance and stability

| Measurement | Method | Acceptance |
|---|---|---|
| Cold startup | SCM request to running/start event | threshold **DECISION REQUIRED** |
| Target/probe/modules | structured event durations | baseline-derived limits |
| SQL duration | not directly emitted; use total/module timing and record limitation |
| Memory/handles/threads | before/after each cycle process counters | no sustained unbounded trend |
| 10/25/50 runs | staged sampling; 50 if practical | count **DECISION REQUIRED** |
| WinRM/SQL sessions | read-only before/during/after observation | no accumulation |
| Cancellation under load | approved graceful stop | limit **DECISION REQUIRED** |

## Acceptance Criteria

1. Service registration resolves to the hashed executable and exact approved
   Windows Collector gMSA.
2. On Server 2022+, it starts, logs startup and remains running for an approved
   observation period (**DECISION REQUIRED**).
3. Approved configuration-provider order loads correctly; invalid database
   configuration fails startup without disclosure.
4. One target inventory cycle executes seven ordered modules and identifies the
   outcome of every attempted ownership boundary.
5. A second run creates no duplicate singular row or target/stable-key pair.
6. Successful boundaries remain internally consistent and equal their latest
   results; successful empty input clears only that boundary. Earlier successful
   commits remain after later failure.
7. Network Adapter/IPv4 replacement is atomic, ordered and orphan-free.
8. Authentication, authorization, DNS, cancellation and unexpected failures
   never cause HTTP fallback.
9. Existing disposal tests and external lab evidence show that created WinRM
   sessions are disposed and repeated runs do not show obvious accumulation.
10. Cancellation stops later modules, rolls back in-flight plural work and ends
    work within a measured limit (**DECISION REQUIRED**).
11. Real SQL Server evidence proves retryable transient replay and non-retryable
    behavior.
12. One target failure does not terminate other targets or later cycles.
13. Current logs plus the execution manifest and SQL exports identify cycle,
    target, module order/outcome, transport, duration and final inventory
    outcome. Any boundary failure and mixed freshness are explicit.
14. No connection string, credential, raw exception message or sensitive
    payload appears in captured sinks.
15. Repeated runs show no sustained unbounded memory, handle, thread, WinRM
    session or SQL connection growth; numeric bounds are **DECISION REQUIRED**.
16. WP-006.2 may validate behavior on the approved Server 2019 host with
    documented `WARNING`/exit-code-1 exception. Production certification
    requires Server 2022+, mandatory matrix evidence and
    Architecture/Security/Operations approval.

## Test Strategy

| Test class | Validates | Admin | SQL | WinRM | Domain identity | CI | Runtime/evidence |
|---|---|---:|---:|---:|---:|---:|---|
| Unit | ordering, mapping, fallback, classification, cancellation, disposal, redaction | No | No | No | No | Yes | seconds; xUnit report |
| Integration/test doubles | host, Worker scopes, fake transport/store failure, structured fields | No | No | No | No | Yes | seconds/minutes; report/log |
| Database integration | mappings, transactions, strategy, FK, permission, concurrency | Provisioner only | Real test SQL | No | Integrated preferred | Dedicated CI only | minutes; report/query output |
| Windows Service Host | SCM lifetime, path/account, start/stop/restart | Yes | Optional | No | gMSA case | No | minutes; SCM/event/process |
| Controlled Lab | gMSA, Kerberos, WSMan, modules, persistence, sink | Service control | Yes | Yes | Yes | No | window-bounded; evidence bundle |
| Stability | repeated resource/session/connection trends | Maybe | Yes | Yes | Yes | No | minutes/hours; samples/report |

## Live Lab Prerequisites

| Input | Status |
|---|---|
| Collector host FQDN | **REQUIRED BEFORE EXECUTION** |
| Host OS/x64 | Actual **REQUIRED BEFORE EXECUTION**; approved Server 2019 is behavior-only, while certification requires Server 2022+ |
| Install path, artifact hash/commit | **REQUIRED BEFORE EXECUTION** |
| Service | display name evidenced as `PSM Operations Platform Windows Collector`; SCM key name **REQUIRED BEFORE EXECUTION** |
| Exact Windows Collector gMSA | **REQUIRED BEFORE EXECUTION** |
| SQL server/port/database | **REQUIRED BEFORE EXECUTION** |
| Migration | expected ID evidenced; applied state **REQUIRED BEFORE EXECUTION** |
| Target FQDN and `ManagedServer.Id` | **REQUIRED BEFORE EXECUTION** |
| HTTPS listener/certificate trust | **REQUIRED BEFORE EXECUTION** |
| HTTP fallback approval/port | **REQUIRED BEFORE EXECUTION**; no approval means no HTTP |
| Firewall allowlists | rule categories evidenced; actual **REQUIRED BEFORE EXECUTION** |
| gMSA host/logon authorization | **REQUIRED BEFORE EXECUTION** |
| SQL effective permissions | required capability evidenced; actual **REQUIRED BEFORE EXECUTION** |
| Target CIM rights | namespaces/classes evidenced; actual **REQUIRED BEFORE EXECUTION** |
| Türkiye time synchronization | policy evidenced; actual **REQUIRED BEFORE EXECUTION** |
| Sink/level/retention/access | **REQUIRED BEFORE EXECUTION** |
| Window, approver, rollback owner | **REQUIRED BEFORE EXECUTION** |
| Existing restricted evidence directory | **REQUIRED BEFORE EXECUTION** |
| Non-production attestations | host, target and database **REQUIRED BEFORE EXECUTION** |

## Data Verification Plan

Use Integrated Authentication and the approved ID. Never print a connection
string. The authoritative detailed query source is
`docs/testing/WP-005-Database-Verification-Queries.md`.

```sql
SET NOCOUNT ON;
DECLARE @ManagedServerId uniqueidentifier = '<APPROVED-MANAGED-SERVER-ID>';

SELECT Id,Fqdn,IsEnabled,LastConnectivityState,
 LastConnectivityFailureCategory,LastConnectivityAttemptAt,
 LastConnectivitySuccessAt,LastSuccessfulTransport,NextConnectivityAttemptAt
FROM configuration.ManagedServer WHERE Id=@ManagedServerId;

SELECT * FROM inventory.WindowsComputerInventory
 WHERE ManagedServerId=@ManagedServerId;
SELECT * FROM inventory.WindowsOperatingSystemInventory
 WHERE ManagedServerId=@ManagedServerId;
SELECT * FROM inventory.WindowsMemoryInventory
 WHERE ManagedServerId=@ManagedServerId;
SELECT * FROM inventory.WindowsProcessorInventory
 WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey;
SELECT * FROM inventory.WindowsDiskInventory
 WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey;
SELECT * FROM inventory.WindowsVolumeInventory
 WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey;
SELECT * FROM inventory.WindowsNetworkAdapterInventory
 WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey;
SELECT ip.*,a.StableSourceKey AS AdapterStableSourceKey
FROM inventory.WindowsIpv4AddressInventory ip
JOIN inventory.WindowsNetworkAdapterInventory a
 ON a.Id=ip.NetworkAdapterInventoryId
 AND a.ManagedServerId=ip.ManagedServerId
WHERE ip.ManagedServerId=@ManagedServerId ORDER BY ip.StableSourceKey;
```

For each run export target-scoped row counts, ordered stable keys, values and
minimum/maximum `CapturedAt`. Assert at most one singular row; group collections
by `(ManagedServerId, StableSourceKey)` and expect count one. Left-join IPv4 to
adapter on adapter and target IDs and expect zero orphans.

Second-run verification retains singular counts, compares collection stable
keys/values rather than generated GUIDs, proves removed source rows disappear,
proves another target is unchanged and compares `CapturedAt` to the recorded
Collector Türkiye-local clock.

Although `ops.CollectorRun` and `inventory.InventorySnapshot` exist, this
Collector does not use them. Historical comparison therefore requires secured
external exports or controlled row-count snapshots.

## Failure Injection Plan

| Scenario | Safe method | Expected log/persistence | Cleanup/risk |
|---|---|---|---|
| WinRM unavailable | mock, then isolated offline lab VM/pre-existing block | classified failure/backoff; no inventory; old state | restore and prove recovery; approval |
| HTTPS TLS failure | classifier/probe mock; isolated invalid-trust endpoint only if approved | `TlsFailure`; `Auto` may try HTTP | owner removes condition; security approval |
| HTTP unavailable | mock; already absent isolated HTTP listener | allowed HTTPS failure then HTTP failure | no target mutation; approval |
| Wrong credentials | authentication exception fake | no fallback/inventory | fake disposal; never live wrong password |
| Unauthorized | fake; optional dedicated restricted lab identity | `AuthorizationFailure`, no fallback | owner restores test ACL; approval |
| DNS | approved nonexistent test/lab name | `DnsFailure`, no fallback, backoff | separately approved test-row cleanup |
| SQL unavailable | mock/proxy first; isolated owner-controlled SQL interruption | safe target-load/save failure; retry only if transient | restore/prove recovery; high-risk approval |
| SQL denied | mock or dedicated restricted principal | safe failure, no statement/secret, no partial transaction | DBA-owned cleanup/approval |
| Service cancellation | test double, then approved graceful SCM stop | later modules absent, rollback, disposal, event 2301 | service-control approval |

Each record includes purpose, control, pre/post state, expected category,
transport attempts, observed retry count, cleanup owner and approval ID.

## Performance and Stability Plan

1. Measure cold start separately, then warm one successful cycle.
2. Sample PID, working set, private bytes, handles and threads before start,
   after startup, before/after each cycle and after graceful stop.
3. Capture probe, module and inventory durations from structured events.
4. Record that SQL-only duration and actual EF retry count are not emitted by
   the current runtime; use existing tests and total/module timing without
   claiming finer precision.
5. Observe WinRM/runspace and SQL connections with approved read-only methods.
6. Run 10 cycles, review, then 25; run 50 only if practical in the window.
7. Distinguish bounded GC/cache variation from monotonic growth.
8. Test cancellation under active work only after the normal baseline passes.

Duration, growth, absolute ceilings, observation time and run count are
**DECISION REQUIRED**. Set them after a clean 10-run baseline on a
production-class Server 2022+ host; do not use the Server 2019 exception host as
the production baseline.

## Observability Gaps

No current observability gap blocks WP-006.2. Mandatory initial criteria can be
evaluated by combining current structured logs, existing tests, SCM/process
evidence, read-only SQL queries, external operating-system counters and a
timestamped execution manifest.

| Classification | Gap | WP-006 evidence method |
|---|---|---|
| IMPORTANT | No persisted Windows inventory run record | `PollingCycleId`, `InventoryCorrelationId`, service logs, timestamped manifest and SQL exports |
| IMPORTANT | No target-wide transaction | Validate accepted independent boundaries, partial target success and mixed freshness explicitly |
| IMPORTANT | Successful module detail requires Debug | Temporary lab-scoped Debug profile with access control and restoration |
| IMPORTANT | No boundary persistence duration/row-count event | Module outcome/duration logs plus read-only before/after SQL |
| IMPORTANT | No explicit target start/final summary | Correlate probe, inventory start/completion and manifest entries |
| IMPORTANT | No explicit successful fallback-reason event | Record controlled precondition and correlate attempt count, selected transport and supporting evidence |
| IMPORTANT | EF retry count is not emitted | Use existing configuration/tests; do not claim a live count without evidence |
| IMPORTANT | No real runspace/session counter | Combine ownership/disposal tests with external process/WSMan observations |
| IMPORTANT | No explicit Event Log/file sink | Use approved lab capture; production sink remains undecided |
| OPTIONAL | No connection-only or SQL-only duration | Record coarse timing limitation |
| OPTIONAL | Polling completion lacks total duration/inventory totals | Correlate current events in the manifest |

A finding is `BLOCKING` only if a mandatory acceptance criterion cannot be
objectively evaluated through any safe evidence method above. No such remaining
blocker is known at WP-006.1 closure. Any discovered blocker must identify the
exact criterion, attempted evidence methods and why none can produce an
objective result.

No gap is implemented in this analysis revision.

## Required Decisions

### Resolved for WP-006

| Decision | Resolution |
|---|---|
| Collection initiation | Retain continuous `Worker` polling; add no one-shot or external trigger |
| Failed module behavior | Accept independent commits; later modules continue when the session remains usable |
| Partial persistence | Allowed as implemented; boundary outcomes and mixed freshness must be explicit |
| Persistent run record | External manifest/log/SQL evidence is sufficient; do not integrate generic run entities |
| Logging level | Temporary lab-scoped Debug is allowed with access control and restoration |
| Concurrent Collectors | Exactly one active Collector instance during initial validation |
| Production support | Server 2019 validates behavior only; certification requires Server 2022+ |

### Remaining decisions

| Decision | Repository evidence/recommendation | Consequence of delay |
|---|---|---|
| Exact production polling frequency | Default is 60 seconds; record lab value and approve production value later | production cadence/load remains unsettled |
| WP-006.2 observation period | No repository duration; approve before execution | service-stability exit cannot be timed |
| Performance thresholds | No SLO; derive median/p95 limits from a production-class baseline | no objective performance certification |
| Cancellation-time target | Cancellation propagates but no limit exists | shutdown can be measured but not bounded |
| Stability count/resource growth | Stage 10/25/optional 50 and approve slope/absolute limits after baseline | long-run pass remains qualitative |
| Final production logging sink | No explicit sink; lab capture is sufficient for WP-006.2 | production retention/access remains deferred |

## Deferred Enterprise Runtime Hardening

The following improvements are intentionally deferred:

1. Evaluate target-wide atomic inventory persistence.
2. Evaluate persistent Windows inventory execution records.
3. Add production-level target start and final summary events.
4. Add inventory-boundary persistence status, duration and row-count events.
5. Add explicit telemetry for the reason successful HTTP fallback occurred.
6. Add EF retry observability.
7. Improve real WinRM/runspace resource observability.
8. Define production logging sinks, retention and access controls.
9. Define concurrency ownership or lease behavior for multiple Collector
   instances.
10. Define production SLOs and resource-growth thresholds.

These items are not implemented by WP-006.1 and must not be silently included
in validation tasks. WP-006 findings may refine their priority. They are
addressed after the current WP-006 validation sequence unless validation
discovers an actual defect that prevents objective proof of a mandatory
criterion.

No new work-package number is assigned in this revision.

## Execution Order

1. Approve this WP-006.1 revision.
2. Prepare the WP-006.2 manifest, evidence directory and lab logging profile.
3. Complete deployment infrastructure validation, Operations Database schema
   validation, and readiness, including any approved Server 2019
   `WARNING` exception.
4. Execute and review the WP-006.2 single-target happy path.
5. Only after that review, proceed to WP-006.3 SQL verification.
6. Verify second-run/current-state semantics.
7. Run mock/database failures before separately approved live failures.
8. Validate recovery and operational evidence.
9. Run baseline and approved stability count.
10. Assemble evidence and conduct independent final review.

Suspected production scope, identity conflict, secret exposure, orphan data,
partial Network snapshot, failed cleanup or unexplained resource growth stops
execution.

## Deliverables

- Approved analysis and decisions.
- Automated, database and host test reports.
- Readiness, artifact, service and identity evidence.
- Controlled-lab execution record.
- Safe logs and redaction report.
- SQL before/after and integrity exports.
- Failure/cleanup records.
- Resource samples and analysis.
- Deviations, residual risks and final review.

## Exit Criteria

- WP-006.1 decisions are resolved and this revision is approved.
- Mandatory automated and supported-host lab scenarios pass.
- Every Not Executed case has approved rationale and compensating evidence.
- Every attempted ownership boundary outcome is identifiable, partial target
  success is explicit, successful boundaries are internally consistent and
  failed boundaries retain data according to implemented store semantics.
- Repeat behavior is proven without invalid singular or stable-key duplicates.
- Fallback, retry, cancellation and recovery match implemented policy.
- Disposal behavior and external evidence show no obvious accumulating
  sessions/connections or sustained unbounded resource growth.
- Evidence is correlated, retained and secret-free.
- Security boundaries remain intact.
- Reviewers distinguish lab success from production certification.
- Deferred hardening is not hidden in validation. Any actual blocker or defect
  is recorded and assigned separately.

## Recommended Next Action

Prepare and execute WP-006.2 Controlled Single-Target Execution.

Do not begin WP-006.3 or failure injection until the initial happy-path evidence
has been reviewed.

## Revision history

| Version | Date | Description |
|---|---|---|
| 0.5.0 | 2026-07-28 | Added the read-only database schema gate as a WP-006.2 entry requirement |
| 0.4.0 | 2026-07-27 | Implemented WP-006.2A deployment tooling and runbook; live deployment pending |
| 0.3.0 | 2026-07-27 | Prepared authoritative WP-006.2 runbook, templates and validation tools; no live execution |
| 0.2.0 | 2026-07-27 | Approved analysis decisions and opened WP-006.2 controlled execution |
| 0.1.0 | 2026-07-27 | Repository-grounded validation analysis draft |
