---
title: PSM Engineering Standards
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-28
reviewers:
  - Engineering
  - Security
  - Operations
product: PSM Operations Platform
work_package: WP-000
---
# PSM Engineering Standards

Execution History is a versioned terminal-write durable projection of safe
execution facts. Execution State remains current authority; Monitoring remains
current observation; Audit remains separate. History uses explicit typed
mapping, short transactions, logical uniqueness, bounded no-tracking queries,
and TimeProvider retention. It introduces no event sourcing, outbox, broker,
generic repository, Unit of Work, automatic migration, or production cleanup
scheduler. See [ADR-0008](../../adr/ADR-0008-Durable-Execution-History-Projection.md).

Monitoring quality read models are immutable, bounded, TimeProvider-based and
advisory. Code-owned metric definitions remain authoritative over
documentation. Monitoring health/readiness may not control execution,
eligibility, deployment or remediation. Execution History and Audit remain
separate approved scopes.

Architecture decisions are indexed in the repository [ADR index](../../adr/README.md). The accepted [Architecture Freeze](../../adr/ADR-0001-Architecture-Freeze.md) and [Development Process Freeze](../../adr/ADR-0002-Development-Process-Freeze.md) are binding. WP-008.5 preserves the frozen rule: Dispatcher resolves; Runtime executes; handlers collect. Handler and policy resolution must be explicit, ordinal, deterministic, and free of reflection or service-location.

Collector plugins target the explicit versioned SDK recorded by [ADR-0006](../../adr/ADR-0006-Explicit-Versioned-Collector-Plugin-SDK.md). Plugins are repository-built, explicitly registered, read-only, cancellation-aware and dependency-minimal. Dispatcher must reject SDK, descriptor, validation or policy incompatibility before Runtime submission; unknown compatibility is never compatible.

Execution Monitoring consumes typed lifecycle events through independent,
failure-isolated subscribers. It uses standard .NET diagnostics, bounded
product-owned metric dimensions and bounded current health. Monitoring never
controls execution or mutates Execution State; State remains authoritative.
Monitoring is non-durable, configures no production exporter, and defers
history to WP-008.8.

## 1. Purpose and Authority

This handbook is the primary engineering instruction source for PSM Operations
Platform developers and AI coding agents. It consolidates repository decisions;
it does not replace accepted ADRs or invent architecture.

Precedence is:

1. accepted ADRs;
2. newer approved architecture or security decisions;
3. the active approved Work Package;
4. current implementation and tests;
5. older architecture narrative and historical Work Packages.

When sources conflict, the conflict must remain visible until a newer approved
source clearly supersedes it or an approval resolves it. Status labels mean:

- **IMPLEMENTED**: present in repository code, tests, or tooling.
- **APPROVED**: binding repository decision.
- **APPROVED — INTEGRATION PENDING**: implemented or approved in the repository
  but not validated through the final integration gate.
- **DEFERRED**: intentionally outside the current package or phase.
- **TECHNICAL DEBT**: known inconsistency or missing governance artifact.
- **UNRESOLVED**: repository evidence does not authorize a single decision.

## 2. Product and Supported Platform

**APPROVED**

| Concern | Standard |
|---|---|
| Product | PSM Operations Platform |
| Product model | Product-first Windows operations platform, not a framework |
| Scale | One department; approximately 1,000 Windows servers |
| Application | .NET 10, ASP.NET Core, intended Blazor Interactive Server management UI |
| Persistence | EF Core 10 and SQL Server 2022 or later |
| Collector host | Windows Server 2022 or later for production |
| Lab exception | Windows Server 2019 may be used only where readiness rules allow it with a non-certifying warning |
| Collector runtime | Windows Service under a dedicated identity |
| Environment | Windows domain, Active Directory, gMSA, WinRM |
| Target PowerShell compatibility | Windows PowerShell 5.1-compatible behavior |

Use the smallest platform that satisfies the approved need. Linux, cloud-native
management, Kubernetes, multi-tenancy, runtime plugins, a public plugin SDK,
generic scripting, and additional messaging infrastructure are out of scope
unless a later accepted decision adds them.

## 3. Current Project Phase

**APPROVED — INTEGRATION PENDING**

The current phase is **Repository Complete**. Repository completion is not lab,
production, runtime, or integration validation.

| Allowed | Prohibited until WP-007.Z |
|---|---|
| Architecture analysis and repository implementation | Deployment to test or production |
| Unit, component, mock, and deterministic persistence tests | Contact with PSMLense, myapp01, SQL Server, AD, or other test/production systems |
| Migration-source development and model snapshot updates | Applying migrations or runtime schema mutation |
| Idempotent SQL generation and static review | Real WinRM or remote inventory |
| PowerShell parsing, syntax, and mock-based tests | Starting, stopping, restarting, or recreating services |
| Documentation, build, test, and static security validation | SPN, firewall, listener, certificate-policy, or AD changes |

No repository-only work may claim live validation. Real clean installation and
end-to-end validation belongs exclusively to
**WP-007.Z — Clean Installation and End-to-End Validation**.

## 4. Non-Negotiable Architecture Principles

**APPROVED**

- Operations first; solve an observed product need.
- Keep it simple; every abstraction must earn its place.
- Security boundaries are architecture.
- Apply least privilege to identities, APIs, database permissions, and behavior.
- Document significant decisions before implementation.
- Deliver small, bounded Work Packages.
- Make important activity observable without exposing sensitive data.
- Maintain one authoritative source per concern.
- Prefer consistency and controlled evolution over cleverness.
- Do not add hidden reflection, dynamic loading, speculative extension points,
  feature flags, or frameworks without approval.
- Preserve the Windows Collector, SQL Collector, Web, and future Windows Action
  Executor identity boundaries.
- The Web process does not connect directly to managed targets.
- Read-only collection does not become an action channel.

## 5. Authentication and Security

**APPROVED; IMPLEMENTED in current configuration and Collector paths**

| Area | Required | Prohibited |
|---|---|---|
| User authentication | Windows Authentication; AD groups map to roles and permissions | Anonymous access to protected management functions |
| Database authentication | Windows Integrated Authentication | SQL Authentication and SQL credential keys |
| Collector identity | Dedicated Windows Collector gMSA/process identity | Embedded or supplied username/password |
| Target authentication | Explicit Kerberos using the process identity | Negotiate, NTLM fallback, Basic, Digest, CredSSP |
| Trust | Normal Windows certificate and domain trust policy | TrustedHosts fallback, trust-all certificates, CA/CN/revocation bypass |
| Secrets | Secure external configuration; allowlisted diagnostics | Logging credentials, secrets, tokens, tickets, private keys, full connection strings, raw CIM output |

Web, Windows Collector, SQL Collector, and the future Windows Action Executor
use separate gMSAs. Do not combine Windows target permissions and SQL target
monitoring permissions under one identity. Runtime identities do not receive
migration DDL merely for convenience.

Structured logs use stable categories and approved fields such as correlation
ID, target ID, normalized FQDN, transport, module, outcome, count, duration,
failure count, and retry time. Raw exception/native text, Kerberos or SPN
details, certificate details, command text, configuration dumps, and sensitive
payloads are excluded.

## 6. Configuration Standards

**IMPLEMENTED — ADR GAP**

Provider precedence from lowest to highest is:

1. `appsettings.json`;
2. `appsettings.{Environment}.json`;
3. Development User Secrets, only in Development;
4. environment variables prefixed with `PSM__`;
5. command-line arguments.

Reload is disabled. Do not use `IOptionsMonitor`. Use built-in .NET providers,
not a custom parser or configuration framework.

`ConnectionStrings:OperationsDatabase` is authoritative. Runtime code reads it
with `IConfiguration.GetConnectionString("OperationsDatabase")`; a connection
string is not copied into an options class. The runtime environment mapping is:

```text
PSM__ConnectionStrings__OperationsDatabase
    -> ConnectionStrings:OperationsDatabase
```

Startup validation is capability-selected, deterministic, side-effect-free,
and fail-fast. It requires a parseable server/database and Windows Integrated
Authentication, rejects SQL credential aliases, and emits only redacted stable
failure codes. It does not prove connectivity.

TLS values follow the supplied approved connection string. WP-003 does not
mandate `Encrypt=True`, prohibit `Encrypt=False`, mandate
`TrustServerCertificate=False`, or prohibit `TrustServerCertificate=True`.
Do not invent a new TLS policy in configuration validation.

The EF design-time factory currently reads unprefixed
`ConnectionStrings__OperationsDatabase`. This is **TECHNICAL DEBT**, limited to
controlled EF tooling, and does not change the runtime `PSM__` rule.

## 7. Time and Timestamp Standards

**APPROVED; IMPLEMENTED for current Collector and inventory paths**

Use `TimeProvider` for wall time, elapsed time, scheduling, and deterministic
tests. Do not scatter `DateTime.Now` or `DateTime.UtcNow` through product code.
Time conversion must be centralized and testable.

Application-owned durable timestamps currently use Türkiye local time
(UTC+3) stored as timezone-less SQL `datetime2`, including inventory
`CapturedAt`. Do not silently convert those values to UTC or add an isolated
UTC convention. This decision assumes a Türkiye-only deployment; multi-region
or platform-wide UTC conversion requires a separate accepted decision and
migration plan.

Artifact creation and deployment-directory suffixes may use UTC when the
relevant deployment standard explicitly defines them; that does not change
application-owned durable timestamp semantics.

ADR-005 is accepted and its restored source records the authoritative
Türkiye-local-time standard. ADR-006 reaffirms inventory timestamp behavior.

## 8. WinRM and Kerberos Standards

**IMPLEMENTED — ADR GAP; integration pending**

- Collector-owned WinRM uses
  `AuthenticationMechanism.Kerberos` explicitly.
- `AuthenticationMechanism.Negotiate`, NTLM fallback, explicit credentials,
  Basic, Digest, CredSSP, and TrustedHosts are prohibited.
- `IncludePortInSPN` is enabled.
- The actual configured port is included in the HTTP service-class SPN:
  HTTP defaults to 5985 and HTTPS defaults to 5986; custom ports replace the
  corresponding default.
- HTTPS remains preferred. `Auto` begins with HTTPS and may use HTTP only after
  an approved fallback-eligible transport failure. Authentication,
  authorization, DNS, cancellation, invalid configuration, and unexpected
  failures do not trigger fallback.
- Certificate validation is never bypassed.
- Existing unqualified IIS HTTP SPNs must not be moved merely for Collector
  operation. Port-qualified WinRM SPNs permit coexistence.
- The Collector neither queries nor modifies AD or SPNs.
- One successful target-scoped WinRM session is shared by the core inventory
  collection run and disposed once.
- Collection uses explicit, allowlisted, read-only CIM projections. Target
  compatibility includes Windows PowerShell 5.1 behavior; PowerShell 7 is not
  required on targets.

Older approved/prepared WP-005 documents and readiness material mention
Negotiate. The newer implemented Kerberos-only security and WinRM standards
supersede those instructions. Historical files remain for traceability and must
not be used as current execution authority.

## 9. Application and Persistence Architecture

**APPROVED; partially IMPLEMENTED**

Use pragmatic Clean Architecture:

- Domain contains business rules without EF, Web, or Collector dependencies.
- Application coordinates use cases without infrastructure details.
- Infrastructure owns EF Core and integrations.
- Web owns UI and authorization entry points.
- specialized Collectors own remote collection.

EF Core `OperationsDbContext` is the persistence boundary. Do not introduce a
Unit of Work abstraction, expose `IQueryable` outside Infrastructure, enable
lazy loading, or create speculative persistence frameworks. Use explicit
entity configurations and `AsNoTracking` for read-only queries where
appropriate. Narrow interfaces are allowed only for a real capability boundary
or deterministic test seam.

The approved core-inventory design explicitly prohibits a Generic Repository
or generic snapshot framework. The repository still contains legacy
`IRepository<TEntity>` and `Repository<TEntity>` types from WP-002. Their
relationship to the newer rule is **UNRESOLVED / TECHNICAL DEBT**; do not
expand their use and do not remove them without a scoped decision.

Do not add automatic migrations, runtime schema mutation, or feature flags
without separate approval.

## 10. EF Core and Migration Standards

**IMPLEMENTED — INTEGRATION PENDING**

- SQL Server 2022 or later and Windows Integrated Authentication are required.
- Entities and `IEntityTypeConfiguration<T>` mappings are explicit.
- Migration source, designer metadata, model snapshot, configurations,
  validators, and tests remain consistent.
- Migrations are authoritative; generated deployment SQL is never manually
  patched.
- Do not rename or casually rewrite authoritative migration IDs.
- Later approved scope normally uses a new forward migration.
- Application startup never calls `Database.Migrate()` and runtime identities
  do not require DDL.
- Generate idempotent SQL and review it statically during Repository Complete.
- Actual database contact and migration application are deferred to WP-007.Z.
- Migration logs and reviews must not expose connection strings, SQL
  credentials, or sensitive SQL payloads.

WP-007 migrations
`20260728093000_WP0071CoreInventoryReliability` and
`20260728125759_WP0071ADurableInventoryRunCorrelation` are
**APPROVED — INTEGRATION PENDING**.

## 11. SQL Server Batch-Safety Standard

**IMPLEMENTED — ADR GAP; integration pending**

SQL Server may compile later statements in an idempotent migration batch before
earlier `ALTER TABLE` statements execute. A static statement that references a
column, key, candidate key, index, or constraint introduced earlier in that
batch can therefore fail even when EF operation order is correct.

For every migration:

1. generate the complete idempotent SQL;
2. inspect every `UPDATE`, `ALTER TABLE`, `ADD CONSTRAINT`, primary key,
   foreign key, and index that references schema introduced earlier;
3. use deferred dynamic SQL through `EXEC(N'...')` only where compilation
   safety requires it;
4. escape nested SQL literals correctly;
5. do not place `GO` inside `migrationBuilder.Sql`;
6. do not patch generated SQL;
7. retain EF migration-history guards; and
8. add deterministic regression tests for known cases.

WP-007.1 defers new memory-column backfill, primary key, affected checks and
indexes, scheduling constraints/indexes, and the candidate-key-dependent
composite foreign key. WP-007.1A adds columns nullable, uses a migration-local
per-server legacy mapping inside deferred SQL, validates no NULL remains,
alters to NOT NULL, creates `(ManagedServerId, InventoryRunId)` indexes, and
removes its temporary mapping.

Static review is not SQL Server execution evidence. Both migrations remain
**APPROVED — INTEGRATION PENDING**.

## 12. Windows Inventory Standards

**IMPLEMENTED — INTEGRATION PENDING**

WP-007 retains normalized current state only for:

- Computer;
- Operating System;
- BIOS and firmware;
- Processor;
- physical Memory;
- Disk;
- Volume;
- Network Adapter; and
- IPv4 Address.

Computer, Operating System, and BIOS are singular per `ManagedServer`. Processor,
Memory, Disk, Volume, Network Adapter, and IPv4 Address are collections.

The approved pipeline is:

```text
open one shared Kerberos WinRM session
  -> collect every core module
  -> normalize and validate the complete snapshot
  -> begin one database transaction
  -> update singular rows and replace all collections
  -> update schedule, traceability, and version
  -> save and commit once
```

Never hold a database transaction during WinRM collection. Collection,
parsing, timeout, cancellation, or validation failure prevents persistence and
preserves the last successful current state. Persistence failure rolls back the
entire core transaction.

| Module | Empty result after a successful, valid query |
|---|---|
| Processor | Invalid |
| Memory | Valid |
| Disk | Valid |
| Volume | Invalid |
| Network Adapter | Valid |
| IPv4 Address | Valid |

Command failure, malformed output, parse failure, timeout, duplicates, or
ambiguity is never valid-empty.

### Inventory Module Contract

**IMPLEMENTED — INTEGRATION PENDING**

Every core module implements the narrow `IInventoryModule<T>` collection
lifecycle and returns `InventoryModuleResult<T>`. `InventoryModuleContext`
contains only target identity, target FQDN, the complete-run
`InventoryRunId`, the shared `IWinRmCommandSession`, `TimeProvider`, and safe
logging. It must never contain a `DbContext`, credentials, connection strings,
mutable persistence state, a service locator, or another module's data.

Modules own explicit CIM projection, parsing, normalization, validation,
deterministic identity, and valid-empty classification. They do not open
sessions, persist data, schedule retries, update connectivity, or increment
`InventoryVersion`. The core pipeline owns orchestration and the one atomic
persistence transaction. Module DTOs, raw contracts, normalized results, and
entity mappings remain independent.

### Deterministic Inventory

**IMPLEMENTED — INTEGRATION PENDING**

The same logical target state must produce the same normalized values, logical
ordering, hardware keys, duplicate decision, and validation outcome regardless
of provider enumeration order. Normalize stable fields first, sort with an
explicit ordinal comparer, then assign any fallback occurrence index.
Culture-sensitive comparison, enumeration-order identity, `.NET GetHashCode`,
and process-randomized hashes are prohibited.

Fallback hashes use SHA-256 over UTF-8 canonical input with documented field
order, an explicit unit-separator delimiter, and `<NULL>` for missing values.
`InventoryRunId`, `CapturedAt`, `InventoryVersion`, duration, and log timestamps
may legitimately differ between successful runs.

### Hardware Identity Rules

**IMPLEMENTED — INTEGRATION PENDING**

Plural hardware identity prefers: vendor stable identifier, firmware stable
identifier, operating-system stable identifier, normalized hardware SHA-256
hash, then deterministic occurrence index. Each module owns an explicit
placeholder set and validates identifier structure and stability.

Two distinct normalized rows producing the same strongest identity are
ambiguous: do not merge or select one; fail validation and preserve prior
current state. Exact duplicate provider rows may be collapsed only when the
module contract explicitly proves they cannot represent distinct hardware.

### Physical disk and volume

Physical Disk uses an explicit `Win32_DiskDrive` projection. `DiskKey` prefers
a valid serial, PNP device ID, device ID, index plus stable hash, then stable
hash plus occurrence. Empty is valid only after successful command, parsing,
and validation. Media type is never inferred from model text.

Volume uses an explicit `Win32_Volume` projection so drive-letter and
mount-point-only volumes share one authoritative provider contract.
`VolumeKey` prefers volume device/GUID, serial plus stable attributes, drive
letter, then stable hash plus occurrence. Drive letter is canonicalized but is
not preferred over volume GUID. Empty volume inventory is invalid. Disk and
Volume remain independent; disk-to-volume correlation is deferred.

### Processor

Processor uses `Win32_Processor`, one row per returned package/socket.
`ProcessorKey` is deterministic and unique with `ManagedServerId`, preferring
normalized Device ID, then socket designation, then a valid non-placeholder
Processor ID, and finally a hash plus deterministic occurrence index.
Duplicate explicit keys are ambiguous and fail the complete core run.
Processor inventory cannot be valid-empty.

Persisted processor facts are limited to identity, display description,
topology, clock speeds, address/data widths, architecture, and provider
virtualization-capability flags. Virtualization is never inferred from
processor name or manufacturer. Processor ID is not logged.

### Physical memory

Memory uses `Win32_PhysicalMemory`, one row per physical module. Total memory is
calculated from module capacities; it is not a special persisted aggregate row.
`ModuleKey` is deterministic and unique with `ManagedServerId`. Placeholder
serial numbers are rejected as stable identity inputs. Duplicate module keys
are an ambiguous collection failure.

Current persisted memory fields are `Id`, `ManagedServerId`, `InventoryRunId`,
`ModuleKey`, `DeviceLocator`, `BankLabel`, `CapacityBytes`, `SpeedMHz`,
`ConfiguredClockSpeedMHz`, `Manufacturer`, `PartNumber`, `SerialNumber`,
`FormFactor`, `MemoryType`, `CapturedAt`, and `RowVersion`.

### Adapter and IPv4

IPv4 is canonical IPv4 text with prefix length `0..32`; IPv6 and IPv4-mapped
IPv6 are out of scope. Every IPv4 row must reference an adapter owned by the
same `ManagedServer`. A composite foreign key enforces adapter ID plus server
ID, with restrictive/no-action delete behavior. Delete IPv4 dependents before
adapters; insert adapters before IPv4 rows. Cross-target references and
cascade delete are prohibited.

Network Adapter uses an explicit read-only `Win32_NetworkAdapter` projection.
`AdapterKey` prefers valid Interface GUID, MAC address, PNP device ID, then a
deterministic SHA-256 hardware hash and occurrence index. Placeholder MAC
addresses are not identity. Empty is valid only after successful command,
parsing, and validation.

IPv4 independently uses `Win32_NetworkAdapterConfiguration`. Its `SettingID`
provides the adapter GUID without consuming the Adapter module's result.
`Ipv4Key` is `AdapterKey` plus canonical IPv4 address; the same address on two
adapters is therefore distinct. Subnet masks must be contiguous and are
converted deterministically to prefix length. Gateway values must be canonical
IPv4. IPv4 empty is valid. IPv6 is ignored rather than persisted.

Both modules implement the narrow inventory lifecycle, reuse the complete-run
WinRM session and `InventoryRunId`, and remain independent until the core
pipeline validates and persists their combined current state.

## 13. Inventory Scheduling and Traceability

**IMPLEMENTED — INTEGRATION PENDING**

Inventory scheduling is independent of connectivity scheduling. A target is
inventory-eligible only when `IsEnabled=true`, inventory is due, and the
current processing cycle has successfully opened its WinRM session. A
previously persisted `Reachable` state alone is insufficient.

`NextInventoryAttemptAt=NULL` means immediately due. The normal success
interval is six hours.

| Consecutive failure | Retry delay |
|---:|---:|
| 1 | 5 minutes |
| 2 | 15 minutes |
| 3 | 30 minutes |
| 4 and later | 60 minutes |

The maximum delay is 60 minutes. Success resets the failure count and category.
Scheduling fields are `LastInventoryAttemptAt`, `LastInventorySuccessAt`,
`NextInventoryAttemptAt`, `ConsecutiveInventoryFailures`,
`LastInventoryFailureCategory`, and `InventoryVersion`. Persisted failure
categories are `CollectionFailure`, `ParsingFailure`, `ValidationFailure`,
`PersistenceFailure`, `Timeout`, and `Unexpected`.

### InventoryRunId

One GUID identifies one complete core inventory execution in logs and durable
current-state rows. All core tables receive the same value inside the core
transaction. A failed collection or rolled-back persistence must not publish a
new durable run ID.

The separate WP-007.1A forward migration gives pre-existing rows one generated
legacy run ID per `ManagedServerId` across all eight tables. It does not use one
global GUID, invent timestamps, reconstruct history, or claim historical
accuracy. Durable migration integration is pending WP-007.Z.

The BIOS table is introduced later by WP-007.2 and therefore has no legacy
rows to backfill. It receives `InventoryRunId` directly when a complete core
run is committed.

### InventoryVersion

`InventoryVersion` is a non-negative, monotonically increasing `bigint` per
`ManagedServer`. It increments only inside a successfully committed core
inventory transaction. Collection failure and persistence rollback leave it
unchanged. It supports later diff/UI decisions without creating inventory
history.

### Capability Discovery

**IMPLEMENTED — INTEGRATION PENDING**

Platform discovery emits independent, normalized facts. The Capability Engine
is the only component that combines IIS, .NET, PowerShell, role, and feature
facts. Capability evaluation follows discovery and committed inventory;
WP-008.2 alone may consume capability snapshots to select strategies.

Platform discovery and capability evaluation are not configuration. They are
read-only and never install, remove, configure, remediate, execute installers,
or modify registry, files, services, tasks, firewalls, IIS, or Windows
features.

Capabilities use an explicit `ManagedTargetServer` subject, stable capability
codes and reason codes, versioned rules, schema version 1, and distinct typed
support and readiness states. `Unknown` is not `NotSupported`; absent required
facts produce `Unknown`. Collector capabilities must not be inferred from
managed-target facts.

Platform modules remain independent and never consume another module's DTO or
the Capability Engine. The engine consumes narrow immutable fact contracts.
Rules are explicit code in ordinal order—not free-form strings, reflection,
scripts, database rules, feature flags, or a generic rules framework.

## 13A. Capability and Collector Decision Standards

**IMPLEMENTED — INTEGRATION PENDING**

`CapabilitySchemaVersion` versions snapshot structure and shared semantics.
Every capability has its own positive explicit integer `RuleVersion` and a
strongly typed Platform, Collection, Monitoring, Management, or Diagnostics
category. An individual rule change does not require a schema-version change.

`Unknown` means evidence is insufficient. It is distinct from false,
NotSupported, blocked, and failure. Unknown required evidence cannot produce
Eligible; it produces an explainable Indeterminate result.

Safe capability provenance retains normalized module/category/stable-key
references, source inventory run/version, and rule version. Raw provider data
and secrets are excluded. Capabilities and decisions use stable reason codes,
deterministic explanations, and explicit prerequisite groups.

Collector rules are explicit code, explicitly registered, independently
versioned, and evaluated only from a committed capability snapshot. Decision
status is separate from platform eligibility and execution readiness. Platform
eligible plus readiness unknown is Indeterminate. Order is Priority,
ExecutionOrder, then ordinal StrategyCode. Every WP-008.2 strategy is read-only
and decision evaluation performs no target access or collector execution.

## 13B. Execution Planning Standards

**IMPLEMENTED — INTEGRATION PENDING**

The Collector Decision Plan answers what should run. The immutable Execution
Plan answers how eligible and ready strategies are arranged. Planning consumes
one committed Decision Plan and must not reevaluate capabilities or decision
rules, resolve collectors, access targets, or execute work.

Execution Plans and their steps, exclusions, logical identities, order,
timeouts, dependencies, parallel groups, throttling classes, batching metadata,
read-only policy, and policy versions are immutable after persistence. Runtime
outcomes belong to separate mutable Execution State records. Plan entities
must not contain attempts, StartedAt, CompletedAt, mutable runtime status, or
retry scheduling.

Collector plugins are a future explicit dependency-injection boundary.
Planning references stable StrategyCode values only and never scans assemblies,
loads plugins, stores implementation types, or instantiates collectors.

Retry execution is owned by the future Collector Runtime. Plans may retain only
explicit product-defined RetryPolicyCode and RetryPolicyVersion references.
They never sleep, retry, increment attempts, or calculate retry times.

Cancellation is first-class at asynchronous loading and persistence boundaries.
OperationCanceledException propagates, cancelled planning creates no partial
replacement, and the prior current plan remains authoritative. Pure
deterministic in-memory planning remains synchronous.

## 13C. Collector Runtime Standards

**IMPLEMENTED — INTEGRATION PENDING**

The Collector Runtime consumes one committed immutable Execution Plan. It
never reevaluates capabilities or decisions and never mutates plan intent.
Timeout, retry, parallel-group, and throttling policies are explicit,
product-owned, positively versioned, and resolved without infrastructure
access. Retry execution belongs only to the Runtime.

`CollectorRuntimeContext` is narrow and immutable. Handlers receive no
`DbContext`, `IServiceProvider`, `IConfiguration`, credential, connection
string, persistence service, or scheduler internals. WP-008.4 implements only
a minimal explicitly registered read-only handler boundary. Reflection
scanning, dynamic loading, `Assembly.Load`, `Activator.CreateInstance`,
external plugins, and production collectors are prohibited; the full plugin
model remains deferred to WP-008.5.

Execution Run, Step, and Attempt state are mutable, separate from the plan, and
use `ExecutionStateSchemaVersion = 1`. Metrics belong to state. Strongly typed
in-process events describe lifecycle transitions; state is authoritative,
event failure is classified, and exactly-once delivery is not claimed.

Timeout and external cancellation remain distinct. Cancellation propagates
through waits, retry delay, and handler calls, and prevents further retry.
Handler failures are classified and isolated. Dependencies take precedence
over explicit bounded in-process parallel and throttling limits. Distributed
execution, queues, locks, schedulers, event buses, and automatic production
activation are not introduced.

Architecture Freeze v1.0 is proposed by WP-008.4 and becomes effective only
after review approval. Integration remains deferred to WP-007.Z.

## 14. Deployment Standards

**IMPLEMENTED tooling; execution DEFERRED to WP-007.Z**

The authoritative deployment path is package-first and local-first:

1. build and validate a package on a build machine;
2. record source revision, dirty state, manifest, file sizes, SHA-256 hashes,
   and package summary hash;
3. transfer it through an operator-approved mechanism;
4. run the installer locally on the Collector server;
5. discover and preserve the existing service definition and gMSA;
6. stage on the same volume;
7. request one graceful stop;
8. replace only product-managed runtime files;
9. preserve allowlisted target-owned configuration;
10. start and perform local health checks; and
11. restore the previous-version directory on failure.

Deployment scripts support Windows PowerShell 5.1, deterministic exit codes,
`WhatIf`, confirmation, safe logs, package hashing, simple retained previous
versions, and rollback. They do not form a general backup system.

Normal update does not recreate the service, change its identity/path/start
mode/dependencies/recovery settings, accept credentials, use TrustedHosts,
depend on remote PowerShell, modify AD/SPNs, contact SQL Server, apply
migrations, mutate database configuration, kill a process, or reboot.
Configuration ownership and local/operator files are preserved according to the
approved allowlist.

Older remote-first deployment material is historical. The approved
`WP-006.8-Safe-Collector-Deployment.md` local-first path supersedes it.

## 15. Testing Standards

**IMPLEMENTED**

- Prefer deterministic unit, component, architecture, and mock-based tests.
- Unit/component tests must not depend on real servers or SQL Server.
- Introduce narrow adapters for process, file system, service control, WinRM,
  event log, hashing, and clock only where a deterministic seam is needed.
- Test malformed and missing data, nulls, duplicates, ambiguous identities,
  valid/invalid empty results, cancellation, timeout, cleanup, transaction
  failure, rollback, idempotency, security redaction, and resource disposal.
- Verify PowerShell 5.1 parsing and behavior for deployment tooling.
- Validate model/snapshot consistency and generated idempotent migration SQL
  without opening a database connection.
- Treat warnings as errors in the build; review any tool warning rather than
  casually suppressing it.
- Real database, WinRM, service, and end-to-end tests are allowed only in
  WP-007.Z under its explicit approvals.

## 16. Documentation Standards

**APPROVED**

Documentation is part of the product. Use repository terminology, ATX
headings, short paragraphs, explicit status, metadata, sources, and revision
history where established.

Documents must distinguish implemented, approved, integration pending,
deferred, technical debt, and unresolved work. ADRs capture costly or
cross-component decisions. Work-package prompts are not a durable substitute
for repository documentation. Update the handbook and applicable ADRs when an
approved decision changes.

Never claim execution, lab validation, production support, or certification
without evidence. Do not include credentials, connection strings, tickets,
tokens, secrets, or sensitive environment-specific details.

## 17. Work Package Lifecycle

**APPROVED**

Development Process Freeze v1.0 is the authoritative detailed process. Future
implementation begins from an approved specification under `workpackages/`;
prompts only launch that specification. See
[`Development-Process-Freeze-v1.0.md`](Development-Process-Freeze-v1.0.md),
[`../../workpackages/README.md`](../../workpackages/README.md), and
[`../../prompts/Prompt-Framework-v2.md`](../../prompts/Prompt-Framework-v2.md).
Historical `docs/tasks/` records remain evidence and are not competing future
specifications.

1. **Analysis** — establish product need, scope, evidence, constraints, and
   conflicts.
2. **Architecture review** — assess boundaries, security, operations, and
   cross-component impact.
3. **Decision gate** — accept an ADR or explicit decision where needed.
4. **Execution planning** — define one bounded Work Package, tests, migration
   impact, documentation, and safety limits.
5. **Repository implementation** — implement only the active package.
6. **Repository review** — inspect code, tests, generated artifacts,
   documentation, security, and diff.
7. **Repository complete** — repository acceptance criteria pass without
   implying runtime integration.
8. **Integration gate** — perform explicitly authorized real installation and
   validation.
9. **Release candidate** — accept integration evidence and remaining risks.

A Work Package stops at an approval gate when a required decision cannot be
safely inferred. Do not silently broaden scope or start the next package.
Codex may report readiness for human review but may not approve its own work.

## 18. Definition of Done

**APPROVED**

| Repository Work Package | WP-007 final completion |
|---|---|
| Scope implemented and architecture preserved | Every repository criterion |
| Build passes with zero errors; warnings reviewed | WP-007.Z approved and executed |
| Deterministic tests pass | Clean database and Collector installation |
| Migration/model consistency verified where applicable | Real PSMLense-to-myapp01 validation |
| Idempotent SQL reviewed where applicable | Initial and repeat inventory verified |
| Documentation and sources updated | Duplicate and same-target integrity verified |
| Security constraints and prohibited operations checked | `InventoryRunId` and `InventoryVersion` verified |
| Git diff/status reviewed without fabricating cleanliness | Rollback/failure preservation verified |
| No unauthorized live operation performed | Event logs, SQL state, and release acceptance reviewed |

## 19. Integration Gate: WP-007.Z

**DEFERRED; not started**

WP-007.Z exclusively owns real integration:

1. create a clean approved lab database;
2. generate a fresh idempotent script from the complete migration chain;
3. apply the entire migration chain;
4. validate schema, constraints, indexes, and migration history;
5. create a fresh Collector package;
6. perform a clean local Collector installation;
7. seed `ManagedServer` from scratch;
8. validate Kerberos and WinRM;
9. execute first inventory;
10. execute second inventory;
11. verify no duplicates;
12. verify `InventoryRunId` and `InventoryVersion`;
13. verify transaction and Adapter/IPv4 consistency;
14. test failure and rollback preservation;
15. inspect event logs and SQL state; and
16. approve the release candidate.

No earlier package may perform these actions or claim runtime completion.

## 20. Git and Change Management

**APPROVED where evidence is clear**

- Do not commit, merge, or push unless explicitly requested.
- Inspect `git status` before and after work.
- Preserve unrelated dirty-worktree changes and operator work.
- Do not reset, discard, rewrite, or report a clean tree unless evidence
  supports it.
- Keep Work Package scope visible in the change description and, when a commit
  is authorized, use a clear conventional-style subject where established.
- Do not create branches without authorization from the active Work Package.

`CONTRIBUTING.md` and the approved development workflow specify named branches.
No repository source establishes main-only development. Therefore branch
workflow is the current documented practice, while the precise protected-branch
and merge policy is **UNRESOLVED**. Do not invent one.

## 21. Review Checklist

- [ ] Active Work Package and status are explicit.
- [ ] `docs/index.md` and engineering principles were reviewed first.
- [ ] Accepted ADRs and newer approved sources were applied by precedence.
- [ ] Architecture and Windows/SQL identity boundaries remain intact.
- [ ] No credentials, connection strings, tokens, tickets, or raw remote data
      are logged or committed.
- [ ] `TimeProvider` and Türkiye-local durable timestamp rules are preserved.
- [ ] No Generic Repository, Unit of Work, feature flag, plugin, or speculative
      framework was introduced.
- [ ] No automatic migration or runtime schema change was introduced.
- [ ] Migration source, model, snapshot, and tests agree.
- [ ] Idempotent SQL batch safety was reviewed where applicable.
- [ ] Deterministic tests cover failure, cancellation, rollback, and
      idempotency.
- [ ] Documentation distinguishes implementation from integration evidence.
- [ ] Git diff/status were reviewed and unrelated changes preserved.
- [ ] No WP-007.Z action occurred outside its gate.

## 22. Prohibited Practices

| Practice | Status |
|---|---|
| SQL Authentication or embedded database credentials | **PROHIBITED** |
| Basic, Digest, CredSSP, Negotiate, or NTLM fallback in Collector WinRM | **PROHIBITED** |
| TrustedHosts or certificate-validation bypass | **PROHIBITED** |
| Logging secrets, tokens, tickets, connection strings, raw CIM output, or raw security exceptions | **PROHIBITED** |
| Combining Windows and SQL target permissions | **PROHIBITED** |
| Collector remote-action or arbitrary-script channel | **PROHIBITED** |
| Generic Repository or Unit of Work for new core-inventory work | **PROHIBITED** |
| Runtime plugin loading, speculative frameworks, or unapproved feature flags | **PROHIBITED** |
| `Database.Migrate()` or application-startup schema mutation | **PROHIBITED** |
| Manual edits to generated migration SQL | **PROHIBITED** |
| `GO` inside `migrationBuilder.Sql` | **PROHIBITED** |
| Database transaction held during WinRM collection | **PROHIBITED** |
| Cross-target Adapter/IPv4 references or cascade delete | **PROHIBITED** |
| Deployment, SQL contact, real WinRM, service control, or AD/SPN changes during Repository Complete | **PROHIBITED** |
| Fabricated validation, clean-tree, or production-readiness claims | **PROHIBITED** |

## 23. Approved but Deferred Items

| Item | Status |
|---|---|
| WP-007 clean installation and end-to-end validation | **DEFERRED to WP-007.Z** |
| Applying WP-007.1 and WP-007.1A migrations | **APPROVED — INTEGRATION PENDING** |
| Durable `InventoryRunId` database verification | **APPROVED — INTEGRATION PENDING** |
| Real Collector package/install and PSMLense-to-myapp01 inventory | **DEFERRED to WP-007.Z** |
| Windows Service, IIS, SQL health, alerting, and privileged action scopes beyond current core inventory | **DEFERRED to later approved packages** |
| Windows Action Executor | **DEFERRED** |
| Durable SQL command-queue behavior beyond the foundational model | **DEFERRED** |
| Platform-wide UTC or multi-region transition | **DEFERRED; requires accepted ADR** |

## 24. Known Technical Debt

| Debt or conflict | Classification | Current instruction |
|---|---|---|
| ADR-005 Türkiye-local-time authority | **RESOLVED** | The accepted source is restored and remains consistent with ADR-006 and current implementation |
| EF design-time tooling uses unprefixed `ConnectionStrings__OperationsDatabase` while runtime uses `PSM__` | **TECHNICAL DEBT** | Treat as tooling-only inconsistency; do not weaken runtime prefix |
| Legacy generic `IRepository<TEntity>` / `Repository<TEntity>` coexist with newer no-Generic-Repository direction | **UNRESOLVED / TECHNICAL DEBT** | Do not expand or remove without a scoped decision |
| Older Negotiate documents conflict with newer Kerberos-only implementation | **RESOLVED** | Active operational guidance now requires explicit Kerberos and prohibits authentication downgrade |
| Older singular total-memory model conflicts with WP-007 physical-module model | **RESOLVED BY ADR-006 v2 and WP-007 implementation** | Use plural module inventory |
| Older independently committed inventory boundaries conflict with WP-007 atomic core transaction | **RESOLVED BY ADR-006 v2** | Use one all-core transaction |
| Older remote-first deployment material conflicts with WP-006.8 | **RESOLVED BY NEWER APPROVED SOURCE** | Use package-first local deployment |
| Roadmap, RELEASE, baseline, and task index lag WP-007 repository state | **TECHNICAL DEBT** | Do not infer phase from stale summaries |
| Protected-branch/merge policy is not defined | **UNRESOLVED** | Follow documented branch naming when authorized; invent no policy |

## 25. Source Documents and ADR Mapping

| Standard | Status | Repository sources |
|---|---|---|
| Product, scope, simplicity, security | **APPROVED** | `docs/project/Manifesto.md`, `Principles.md`, `Scope.md`, `Product-Decisions.md` |
| Pragmatic Clean Architecture | **APPROVED** | ADR-001; `docs/architecture/03-Architecture-Principles.md`, `05-Solution-Structure.md` |
| SQL durable command direction | **APPROVED; behavior deferred** | ADR-002; `docs/collectors/CollectorQueue.md` |
| Collector identity separation | **APPROVED** | ADR-003; `docs/security/gMSA.md`, `Authentication.md` |
| Core inventory ownership, current state, atomicity, time | **APPROVED; integration pending** | ADR-006 v2; `AtomicCoreInventoryPipeline.cs`; `WindowsInventoryStores.cs`; `AtomicCoreInventoryPipelineTests.cs`; `WindowsInventoryPersistenceTests.cs` |
| Runtime configuration and redaction | **IMPLEMENTED — ADR GAP** | `docs/configuration/WP-003-*`; `PsmConfigurationExtensions.cs`; `OperationsDatabaseConfigurationValidator.cs`; configuration tests |
| EF design-time inconsistency | **TECHNICAL DEBT** | `OperationsDbContextFactory.cs`; `EfCoreToolingTests.cs`; `OperationsDbContextFactoryTests.cs` |
| Kerberos-only WinRM and port-qualified SPN | **IMPLEMENTED — ADR GAP** | `docs/handbook/WinRM.md`; `docs/collectors/WP-004-WinRM-Connectivity.md`; `docs/security/WP-004-Windows-Collector-Security.md`; `PowerShellWinRmSession.cs`; WinRM architecture/tests |
| SQL Server/EF standards | **IMPLEMENTED; integration pending** | `docs/handbook/EFCore.md`, `SQLServer.md`; `OperationsDbContext.cs`; entity configurations; migrations; model tests |
| SQL batch safety | **IMPLEMENTED — ADR GAP; integration pending** | WP-007.1 and WP-007.1A migrations; `MigrationScriptTests.cs` |
| Physical-memory modules | **IMPLEMENTED; integration pending** | ADR-006 v2; `AtomicCoreInventoryPipeline.cs`; `WindowsInventoryEntities.cs`; `WindowsInventoryConfigurations.cs`; pipeline/persistence tests |
| Adapter/IPv4 same-target integrity | **IMPLEMENTED; integration pending** | ADR-006 v2; `WindowsInventoryConfigurations.cs`; WP-007.1 migration; model/migration/persistence tests |
| Scheduling and traceability | **IMPLEMENTED; integration pending** | `ManagedServer.cs`; `ManagedServerConfiguration.cs`; `AtomicCoreInventoryPipeline.cs`; `WindowsInventoryStores.cs`; WP-007 migrations and tests |
| Local package deployment and rollback | **IMPLEMENTED tooling; execution deferred** | `docs/deployment/WP-006.8-Safe-Collector-Deployment.md`; `scripts/deployment/*.ps1`; `CollectorDeploymentPackage.Tests.ps1` |
| Testing and no-live-operation boundary | **APPROVED** | `docs/handbook/Testing.md`; AGENTS.md; architecture, infrastructure, Collector, readiness, deployment, and PowerShell test suites |
| Work Package lifecycle | **APPROVED** | `CONTRIBUTING.md`; `docs/architecture/11-Development-Workflow.md`; `docs/tasks/WorkPackage-Template.md`; AGENTS.md |
| Repository Complete / WP-007.Z gate | **APPROVED — INTEGRATION PENDING** | `docs/tasks/WP-007.1A-Approved-Enhancements.md`; `docs/testing/WP-007.1-Core-Inventory-Lab-Validation.md`; this WP-000 handbook |

## 26. Change Control for This Handbook

This handbook changes only through an approved Work Package. A change must:

1. identify the source decision and affected sections;
2. update or add an ADR when architecture changes;
3. preserve historical records;
4. mark implementation and integration status accurately;
5. update the source mapping and technical-debt tables;
6. validate links, terminology, statuses, secrets, and Markdown structure; and
7. pass repository review before becoming authoritative.

Do not silently convert implementation behavior into architecture, erase a
conflict, or claim integration from repository-only evidence.
