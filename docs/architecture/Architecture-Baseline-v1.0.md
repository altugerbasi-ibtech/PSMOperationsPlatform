---
title: PSM Operations Platform — Architecture Baseline
version: 1.0.17
status: Draft
owner: Architecture
last_updated: 2026-07-27
reviewers:
  - Product Owner
  - Chief Software Architect
product: PSM Operations Platform
baseline_scope: WP-001 through WP-004 and WP-005.7 completed
---

# PSM Operations Platform — Architecture Baseline v1.0.17

## 1. Purpose

This document defines the approved architectural baseline for PSM Operations Platform.

It is the primary entry point for understanding the product boundaries, system structure, component responsibilities, dependency rules, security boundaries, database direction, collector behavior, engineering principles, delivery workflow and architecture gate expectations.

Detailed architecture documents and accepted Architecture Decision Records remain authoritative. If this baseline conflicts with an accepted ADR, the ADR takes precedence.

## 2. Baseline status

This baseline represents the architecture at the following delivery point:

- Repository foundation established.
- WP-001 — Solution Skeleton completed and passed Gate 1.
- WP-002 — Core Persistence Layer implemented.
- WP-002 Domain entities, EF Core mappings, the `InitialCreate` migration and
  persistence tests are present. Collector implementations, inventory logic,
  Web CRUD and command workers are not yet implemented.
- ADR-005 — Türkiye Local Time Standard is accepted.
- WP-003 — Configuration Management is completed.
- WP-003 is limited to host startup configuration and does not reopen the
  completed WP-002 persistence design.
- WP-003 implements the standard provider order, `PSM__` environment mapping,
  capability-selected OperationsDatabase validation and one safe post-validation
  startup event. The Windows Collector selects this capability; other
  production hosts remain unchanged.
- WP-004 — Windows Collector Foundation and Target Connectivity is completed.
  WP-004.2 implements Generic/Windows Service hosting and
  OperationsDatabase composition. WP-004.3 implements the scoped enabled/due
  target read path plus its nullable eligibility field, index and controlled
  migration. WP-004.3A adds target transport configuration, WP-004.4 adds
  the read-only WinRM probe and WP-004.5 adds last-known result persistence,
  rowversion conflict handling and deterministic capped backoff.
- Its delivered product policy is one active collector, HTTPS-first `Auto` with
  conditional HTTP fallback, no credentials/actions/inventory and durable
  last-known connectivity with capped backoff.
- WP-005.1 — Windows Inventory Framework analysis/documentation completed
  before implementation and introduced no code or migration in that sprint.
- The approved design reuses one successful target-scoped WinRM session from
  probe through explicit, ordered, compile-time inventory modules.
- Current state uses normalized `inventory` entities under `ManagedServer`.
  Singular Computer, Operating System and Memory update; plural Processor,
  Disk and Volume replace all after complete validation. Network Adapter and
  IPv4 Address form one atomic Network Snapshot. Failure preserves the last
  successful ownership-boundary state.
- Inventory introduces no reflection, dynamic loading, runtime plugin,
  arbitrary script or remote action. DNS Alias Discovery is a separate future
  Work Package.
- WP-005.2 — Session Ownership and Inventory Orchestration Foundation is
  completed. Successful session ownership transfers from transport to probe to
  the target cycle; the cycle passes the identical session to an explicit,
  ordered empty module pipeline and disposes it once.
- WP-005.2 adds no inventory entity, migration, persistence, concrete module,
  remote command, retry or timeout. Existing WP-004 open/operation timeout,
  `Auto` budget, cancellation and bounded target concurrency remain binding.
- Inventory event IDs `2350`–`2354` are reserved for orchestration lifecycle.
- WP-005.3 implements eight normalized current-state tables, explicit
  ownership-focused stores and the controlled
  `20260727230000_AddWindowsInventoryCurrentState` migration.
- WP-005.4 through WP-005.7 implement seven ordered modules: Computer,
  Operating System, Memory, Processor, Disk, Volume and the combined Network
  Snapshot. Each parallel target resolves its scoped orchestrator, modules,
  stores and EF context independently.
- Collection uses explicit `Get-CimInstance` projections. Results are
  normalized and validated before persistence. Processor, Disk, Volume and
  Network use atomic replace-all; successful empty clears owned state and
  failure preserves it.
- WP-005.S1 is the post-implementation controlled lab smoke-test release gate.
  Preparation does not authorize live access; execution requires an explicit
  dedicated non-production topology and approval record.
- WP-005.S2 supplies the release gate's read-only PowerShell environment
  validation tooling. It is operational tooling outside product runtime code,
  has one public entry point, and performs no remediation or automatic
  migration.

## 3. Product vision

> PSM Operations Platform is a Windows-native operations platform designed for enterprise environments to discover, monitor, analyze and safely operate Windows infrastructure through a secure, scalable and architecture-first approach.

The platform is intended for internal enterprise Windows operations, initially targeting approximately 1000 Windows Servers.

## 4. Product scope

### 4.1 In scope

- Windows Server inventory and operations
- IIS inventory and monitoring
- SQL Server inventory and monitoring
- Windows Services
- Windows Event Logs
- Performance Counters
- Certificates
- Future runtime-managed product configuration
- Monitoring and alerts
- Auditing
- Durable operational commands
- Safe, explicitly authorized infrastructure actions in a later phase

### 4.2 Technology baseline

- .NET 10
- Blazor Interactive Server
- ASP.NET Core
- EF Core 10
- SQL Server 2022 or later
- Windows Authentication
- Active Directory
- Group Managed Service Accounts
- Windows Services for collectors
- WinRM for approved Windows collection
- SQL Server as the central durable data and command platform

### 4.3 Explicitly out of scope

- Linux
- Azure-native management
- AWS-native management
- VMware management
- Oracle
- Docker
- Kubernetes
- Multi-tenant architecture
- Multi-region deployment
- Multi-time-zone operation
- Runtime plugin architecture
- Public REST API
- SQL authentication
- Application-managed credentials
- Secret vault integration

New scope must not be introduced implicitly through implementation.

## 5. Architecture principles

### 5.1 Operations first

Architecture must reflect how the platform will be installed, operated, upgraded, monitored, recovered and supported.

### 5.2 Security by design

Security boundaries are part of the component design.

### 5.3 Keep it simple

Use the smallest design that meets the real requirement.

### 5.4 Documentation before implementation

Costly or cross-component decisions are documented before code is written.

### 5.5 Least privilege

Each process identity receives only the permissions required for its responsibility.

### 5.6 No magic

Configuration, component behavior, retries and failure handling must be explicit and observable.

### 5.7 Small evolution

The platform evolves through bounded Work Packages.

### 5.8 Single source of truth

Each architectural concern must have one authoritative definition.

### 5.9 Quality over speed

A Work Package is not complete merely because code exists.

### 5.10 Engineering over coding

The platform is designed for sustainable operations and change.

### 5.11 Every abstraction must earn its place

Interfaces, generic frameworks and extension points are introduced only when a concrete need justifies them.

### 5.12 Operational simplicity over architectural purity

A theoretically elegant design is not preferred when it creates unnecessary operational complexity.

Examples:

- SQL Server durable queue rather than an additional message broker
- gMSA rather than application-managed credentials
- built-in .NET logging before external logging frameworks
- pragmatic Clean Architecture rather than rigid pattern enforcement
- Türkiye local time for a Türkiye-only deployment
- no mandatory generic repository pattern

## 6. High-level system architecture

```text
Operators
   |
   v
Web Application
   |
   v
Central SQL Server
   |-- future runtime-managed product configuration
   |-- inventory
   |-- monitoring
   |-- alerts
   |-- audit
   |-- durable command queue
   |
   +--> Windows Collector ------> Windows / IIS / Services
   |
   +--> SQL Collector ----------> SQL Server
   |
   +--> Windows Action Executor -> privileged Windows actions (future)
```

Core rules:

- The Web application never directly connects to managed targets.
- Collectors do not depend on the Web process being online.
- SQL Server is the durable coordination point.
- SignalR, when introduced, is presentation-only and never the durable queue.
- Read-oriented collection and privileged actions are separated.
- The future Action Executor is not part of the initial collector implementation.

## 7. Component responsibilities

### 7.1 Web application

Responsibilities:

- Operator user interface
- Windows Authentication entry point
- Authorization entry point
- Future runtime-managed product configuration management
- Inventory and monitoring presentation
- Command submission to the durable SQL queue
- Audit presentation
- Health endpoints

Restrictions:

- Must not connect directly to managed targets.
- Must not execute remote infrastructure actions.
- Must not store or manage credentials.
- Must not become a background collection engine.

### 7.2 Windows Collector

Responsibilities:

- WinRM-based Windows discovery
- Operating system discovery
- IIS discovery and monitoring
- Windows Service discovery
- Performance Counter collection
- Event Log collection
- Certificate discovery
- Windows-oriented command leasing
- Heartbeat and execution status

Initial permission model:

- Read-only access to approved Windows targets
- No privileged infrastructure actions

### 7.3 SQL Collector

Responsibilities:

- SQL Server connectivity
- Instance and database discovery
- Session and connection monitoring
- Blocking and wait analysis
- Database-file usage collection
- SQL-oriented command leasing
- Heartbeat and execution status

Initial permission model:

- Approved metadata and DMV access
- No administrative actions unless introduced through a later approved Work Package

### 7.4 Windows Action Executor

Status: Future component.

Responsibilities:

- Explicitly authorized privileged Windows operations
- Separate command type and queue leasing
- Strong audit requirements
- Dedicated privileged gMSA

Restrictions:

- Must remain separate from the read-oriented Windows Collector.
- Must not be introduced implicitly through discovery or monitoring work.

### 7.5 Central SQL Server

Responsibilities:

- Durable application data
- Future runtime-managed product configuration; not host startup configuration
- Inventory
- Monitoring
- Alerts
- Audit
- Collector heartbeat
- Durable command queue
- Leasing and execution coordination in later Work Packages

## 8. Solution structure

```text
src/
  PSMOperationsPlatform.Domain
  PSMOperationsPlatform.Application
  PSMOperationsPlatform.Contracts
  PSMOperationsPlatform.Infrastructure
  PSMOperationsPlatform.Web
  PSMOperationsPlatform.Collectors.Common
  PSMOperationsPlatform.WindowsCollector
  PSMOperationsPlatform.SqlCollector
  PSMOperationsPlatform.WindowsActionExecutor  # future
```

### 8.1 Project responsibilities

**Domain**

- Core business concepts and rules
- No Infrastructure, EF Core, host or remote-integration dependencies

**Application**

- Use-case coordination
- May depend on Domain and Contracts
- Must not depend on Web or collector hosts

**Contracts**

- Stable cross-boundary messages and data contracts
- Must remain small and must not become a shared dumping ground

**Infrastructure**

- EF Core, SQL Server persistence and approved integrations
- Must not leak EF Core into Domain
- Must not introduce speculative generic repositories

**Web**

- Blazor UI
- ASP.NET Core composition
- Authentication and authorization entry points
- HTTP health and error handling

**Collectors.Common**

Limited to genuinely shared collector infrastructure:

- queue leasing
- heartbeat
- correlation
- retry
- shared execution contracts

It must not become a plugin framework, generic execution platform or shared business-logic container.

**Collector hosts**

Each collector:

- owns its process lifecycle,
- owns target-specific execution,
- leases only its approved command type,
- uses its own gMSA,
- and composes only required dependencies.

## 9. Dependency rules

The accepted dependency direction is pragmatic Clean Architecture.

```text
Domain
  ^
  |
Application
  ^
  |
Infrastructure
  ^
  |
Host composition: Web / WindowsCollector / SqlCollector
```

Rules:

- Domain has no infrastructure dependency.
- Application coordinates use cases.
- Infrastructure implements persistence and integrations.
- Web owns UI and authorization entry points.
- Collectors own remote execution.
- CQRS is used only where it clarifies behavior.
- Interfaces are not added solely to satisfy a pattern.
- Dependency Injection is not a reason to create abstractions.
- Host projects must not reference one another.
- Collector-specific logic must not be placed in Web.

## 10. Security architecture

### 10.1 Authentication

Users authenticate through Windows Authentication.

The application does not implement a separate username/password system.

### 10.2 Authorization

AD groups map to application roles and explicit permissions.

Detailed role mapping requires a dedicated Work Package.

### 10.3 Process identities

| Component | Suggested identity | Permission area |
|---|---|---|
| Web | `gMSA-PSMWeb$` | Application database only |
| Windows Collector | `gMSA-PSMWindows$` | Approved read-only Windows, IIS and performance access |
| SQL Collector | `gMSA-PSMSql$` | Approved SQL metadata and DMV access |
| Windows Action Executor | `gMSA-PSMAction$` | Future privileged operations |

Exact names are deployment conventions; identity separation is architectural.

### 10.4 Credential policy

> The platform never stores, manages, encrypts, rotates or distributes credentials.

Therefore:

- No SQL usernames or passwords
- No credential configuration sections
- No secret store
- No Key Vault dependency
- No DPAPI credential wrapper
- No credential cache
- No password rotation feature
- No SQL-authentication fallback

Authentication is delegated to Active Directory using gMSA and Kerberos.

### 10.5 SQL connectivity

Expected controlled internal deployment characteristics:

```text
Integrated Security=True;
Encrypt=True;
TrustServerCertificate=True;
```

Connection strings must not contain credentials and must never be written to logs.

### 10.6 Remote-action boundary

The Windows Collector must not gradually acquire action permissions.

Privileged actions require:

- dedicated Action Executor,
- dedicated command types,
- dedicated authorization,
- dedicated gMSA,
- and complete auditing.

## 11. Database architecture

### 11.1 Central database

One central SQL Server database is used.

Initial logical schemas:

- `inventory`
- `monitoring`
- `collection`
- `operations`
- `security`
- `audit`
- `configuration` (future runtime-managed product configuration; not WP-003
  host startup configuration)

Collector identities receive only the permissions required for their function.

### 11.2 Durable command queue

SQL Server is the approved durable command queue technology.

The queue must eventually support:

- collector type
- durable command state
- leasing
- lease expiration
- retry count
- timeout handling
- concurrency control
- failure recording
- dead-letter behavior
- auditability

Implementation remains out of scope until its dedicated Work Package.

### 11.3 Time-series retention

Continuously growing monitoring and collection tables require explicit retention rules. Infinite retention is not an acceptable default.

### 11.4 EF Core direction

- EF Core 10 with SQL Server provider
- No lazy loading
- No automatic migration during application startup
- Migrations are controlled deployment artifacts
- Background services create scopes for scoped DbContext dependencies
- Provider-supported transient retry applies to individual SQL operations
- Exhausting an EF retry does not permanently terminate a collector

### 11.5 Database design baseline status

WP-002 — Core Persistence Layer completed the current database foundation.
Its approved task, ER model, migration and validation evidence define:

- the EF Core SQL Server provider and `OperationsDbContext`;
- entity mappings, schemas, tables, constraints and indexes;
- migration and controlled deployment behavior;
- concurrency behavior;
- persistence exceptions, error mapping and logging.

WP-002 remains the database foundation. ADR-006 defines the later WP-005
inventory ownership boundary, IPv4-only Network Snapshot and atomic normalized
multi-table replacement. It does not redesign WP-002 or change WP-003
configuration behavior.

## 12. Configuration and infrastructure direction

This section records host startup configuration direction. WP-002 owns SQL
Server persistence behavior; WP-003 owns how each host composes, binds,
validates and safely reports its runtime configuration. SQL-backed runtime
product configuration remains future scope and is not part of WP-003.

### 12.1 Configuration

Standard .NET configuration sources:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Development User Secrets, only in Development
4. `PSM__`-prefixed environment variables
5. Command-line arguments

The list is ordered from lowest to highest precedence. Environment variables
override JSON and Development User Secrets. Command-line arguments have the
highest precedence. Production does not load User Secrets. WP-003 follows the
standard .NET provider precedence model and does not introduce a custom
precedence mechanism or configuration parser.

The standard environment provider removes the `PSM__` prefix and treats double
underscores as section separators. For example,
`PSM__SomeSection__SomeValue` maps to `SomeSection:SomeValue`. This mapping is
illustrative and does not add an options model or property to WP-003.
`PSM__ConnectionStrings__OperationsDatabase` is the concrete WP-003 mapping.
Environment-variable name casing can differ by operating system; deployments
SHOULD use the documented casing consistently. Implementation tests SHALL
verify prefix and separator behavior. Exact bootstrap API use SHALL be selected
after reviewing each existing host composition root.

Initial environments:

- Development
- Test
- Production

### 12.2 Options

- Strongly typed options
- Startup validation for critical settings
- Fail-fast behavior for invalid configuration
- Small and coherent configuration sections
- No custom configuration framework

WP-003 implements no public options model. No real options property remains
after the named OperationsDatabase connection is kept outside options.
`ConnectionStrings:OperationsDatabase` is read through the standard named
connection API and validated only by hosts that select the operations database
configuration capability. The Windows Collector selects it for target loading;
Web and SQL Collector behavior remains unchanged.

`PlatformOptions`, `CollectorRuntimeOptions`, `HeartbeatOptions`,
`CommandQueueOptions`, `InventoryOptions` and `RetentionOptions` are not WP-003
options. They may be introduced only by a later Work Package with a concrete
consumer and behavior requirement.

WP-003 startup validation covers connection-string presence, SQL Server syntax
and Windows Integrated Authentication mode only where the capability is
registered. The connection string is never stored in an options property. This
does not redefine SQL Server persistence behavior.

### 12.3 Logging

Use built-in `Microsoft.Extensions.Logging`.

Rules:

- `ILogger<T>`
- structured templates
- no sensitive values
- no connection strings
- contextual scopes
- no external logging framework without a measured requirement

### 12.4 Correlation

Web:

- accept valid `X-Correlation-ID`,
- generate one when absent,
- return it in the response,
- include it in request logging scope.

Collectors:

- generate a correlation identifier for each polling cycle or bounded execution,
- include it in logging scope.

OpenTelemetry and distributed tracing are not part of WP-002.

### 12.5 Health checks

Web endpoints:

- `/health/live`: process liveness only
- `/health/ready`: configuration and required SQL readiness

Rules:

- SQL outage must not make liveness unhealthy.
- SQL outage may make readiness unhealthy.
- Health output must not expose secrets, connection strings or stack traces.
- Collectors use internal health services without unnecessary HTTP listeners.

### 12.6 Error handling

Web:

- ASP.NET Core exception handling
- `ProblemDetails`
- no internal exception details in Production

Collectors:

- catch only to add context, classify recovery or preserve the loop,
- treat cancellation as expected shutdown,
- prohibit empty catch blocks.

## 13. Reliability and retry model

### 13.1 EF Core operation retry

Purpose: recover one SQL operation from provider-recognized transient failures.

Initial baseline:

- maximum retry count: 3
- maximum retry delay: 10 seconds, configurable

After retries are exhausted:

- the current SQL operation fails,
- the failure is logged with context,
- control returns to the collector loop.

### 13.2 Collector loop recovery

```text
Polling cycle
    |
    v
SQL operation
    |
    v
EF transient retry
    |
    v
Operation still fails
    |
    v
Structured log and classification
    |
    v
Cancellable delay
    |
    v
Next polling cycle
```

Initial defaults:

- temporary SQL or network failure: 30 seconds
- authentication, authorization or login failure: 300 seconds
- graceful host shutdown timeout: 30 seconds

Rules:

- no tight retry loops,
- all delays are cancellable,
- shutdown cancellation is not logged as failure,
- persistent permission errors must not flood logs,
- collectors do not stop permanently because one operation exhausted three EF retries.

### 13.3 Fail-fast conditions

Startup stops for:

- missing required configuration,
- invalid options,
- unsupported startup configuration,
- invalid dependency composition,
- known incompatible schema state when checking exists.

The process does not permanently stop for:

- temporary SQL outage,
- temporary DNS or network failure,
- one failed polling cycle.

## 14. Time standard

The platform is designed for Türkiye-only deployment.

Accepted rules:

- Application-owned persisted timestamps use Türkiye local time, UTC+3.
- Multi-time-zone support is out of scope.
- Use `TimeProvider.System.GetLocalNow()`.
- Do not use `DateTime.Now` in new application code.
- Do not use `DateTime.UtcNow` for application-owned current timestamps.
- Do not use SQL current-time functions for application-owned timestamps.
- The application supplies timestamps to SQL Server.
- Future timestamp columns use `datetime2(3)` unless separately justified.
- Do not introduce a custom clock interface.

This is a deliberate product-specific decision.

## 15. Deployment architecture

```text
IIS
  |
  +-- PSM Web Application
      identity: Web gMSA

Windows Service Host A
  |
  +-- Windows Collector
      identity: Windows Collector gMSA

Windows Service Host B
  |
  +-- SQL Collector
      identity: SQL Collector gMSA

SQL Server
  |
  +-- PSM Operations Platform database

Future Windows Service Host
  |
  +-- Windows Action Executor
      identity: Action gMSA
```

Collectors may run on separate servers. Separation is preferred where it reinforces permission boundaries and operational independence.

Network paths must be documented per component and collector capability. Access must be allow-listed rather than broadly assumed.

## 16. Engineering workflow

```text
Product Idea
    |
    v
Requirement Analysis
    |
    v
Architecture Discussion
    |
    v
ADR when required
    |
    v
Bounded Work Package
    |
    v
Codex implementation
    |
    v
Build, tests and verification
    |
    v
Architecture Gate Review
    |
    +--> PASS   -> merge
    |
    +--> REWORK -> correct and review again
```

### 16.1 Roles

**Product Owner / Technical Lead**

- defines product need,
- approves scope,
- makes final product decisions,
- authorizes implementation.

**Chief Software Architect**

- preserves architecture,
- reviews designs,
- identifies cross-component decisions,
- prevents unnecessary abstractions,
- produces or reviews ADRs and Work Packages,
- performs Architecture Gate Reviews.

**Codex**

- implements only the active Work Package,
- follows repository instructions and ADRs,
- builds and tests,
- reports evidence and deviations.

Codex must not:

- redesign the architecture,
- expand scope,
- execute real infrastructure actions unless explicitly authorized,
- commit or push automatically.

### 16.2 Work Package sizing

A Work Package should normally fit approximately two to five working days.

Each Work Package requires:

- one bounded objective,
- in-scope and out-of-scope definitions,
- architecture constraints,
- security requirements,
- acceptance criteria,
- tests,
- documentation updates,
- gate evidence.

## 17. Architecture Gate Review

Every Work Package is reviewed for:

### Architecture

- dependency direction
- ADR compliance
- unnecessary layers
- speculative frameworks
- scope expansion
- correct component ownership

### Security

- least privilege
- gMSA boundaries
- no stored credentials
- no unauthorized authentication method
- no unreviewed action path
- no sensitive logging

### Performance and scale

- reasonable behavior for approximately 1000 servers
- controlled SQL load and polling
- no unbounded memory growth
- no avoidable high-frequency patterns
- retention considered for growing data

### Reliability

- correct timeout and retry behavior
- cancellable waits
- graceful shutdown
- no permanent collector termination for transient failure
- observable failure state

### Operations

- installable
- configurable
- upgradeable
- rollback-aware
- monitorable
- supportable
- diagnosable

### Code quality

- restore, build and tests succeed
- no critical warnings
- readable code
- tests cover introduced behavior
- no unnecessary interfaces or generics
- no unresolved critical TODOs

## 18. Definition of Done

A Work Package is complete only when:

- acceptance criteria are met,
- architecture review passes,
- security boundaries are preserved,
- `dotnet restore` succeeds,
- `dotnet build` succeeds,
- `dotnet test` succeeds,
- required runtime verification is complete,
- documentation is updated,
- no critical TODO remains,
- repository is clean,
- and Gate Review records a PASS.

It is not complete when:

- a critical item is informally deferred,
- a workaround remains as permanent design,
- acceptance criteria are incomplete,
- tests were not executed,
- documentation is stale,
- or architecture changed without an ADR.

## 19. Delivery roadmap baseline

1. WP-001 — Solution Skeleton — Completed
2. WP-002 — Core Persistence Layer — Implemented
3. WP-003 — Configuration Management — Completed
4. WP-004 — Windows Collector Foundation and Target Connectivity — Completed
5. WP-005 — Windows Inventory Framework — Completed through WP-005.7
6. Windows Service discovery/inventory — Planned; number not assigned
7. DNS Alias Discovery — Future separate Work Package; number not assigned
8. Durable command queue behavior — Planned; number not assigned

IIS discovery and Windows Service inventory are candidates for a later
production-value collector feature.

The earlier WP-004 Durable Command Queue label is superseded by the completed
bounded Work Package above; command behavior remains future scope.

## 20. Architecture decision baseline

### Accepted

- ADR-001 — Pragmatic Clean Architecture
- ADR-002 — SQL Durable Command Queue
- ADR-003 — Collector Separation by Security Boundary
- ADR-005 — Türkiye Local Time Standard
- ADR-006 — Inventory Ownership Boundaries

ADR-005 remains accepted in this baseline. Its restored source and ADR-006
consistently preserve the existing Türkiye local-time standard for
application-owned persisted timestamps and WP-005 inventory.

## 21. Prohibited drift

The following require explicit architecture review and normally a new ADR:

- replacing SQL Server as the durable queue,
- merging collector identities,
- adding credential storage,
- adding SQL authentication,
- allowing Web to connect directly to targets,
- adding privileged actions to Windows Collector,
- introducing a plugin runtime,
- adding Linux or cloud-provider scope,
- changing the time-storage standard,
- introducing automatic production migrations,
- adding a new external infrastructure dependency,
- changing central database topology,
- breaking project dependency direction.

Codex must report the conflict rather than implement such a change silently.

## 22. Baseline maintenance

Review this document when:

- a new ADR is accepted,
- a major component is added,
- a security boundary changes,
- deployment topology changes,
- database principles change,
- a new external dependency is introduced,
- or a major milestone is completed.

Versioning:

- Patch: clarification without architectural change
- Minor: additional accepted architecture within the same direction
- Major: incompatible architectural direction or security-boundary change

## 23. Source documents

Primary repository references:

- `docs/architecture/Architecture-Handbook.md`
- `docs/architecture/03-Architecture-Principles.md`
- `docs/architecture/04-System-Overview.md`
- `docs/architecture/05-Solution-Structure.md`
- `docs/architecture/07-Collector-Architecture.md`
- `docs/architecture/08-Security-Architecture.md`
- `docs/architecture/09-Database-Architecture.md`
- `docs/adr/ADR-001-Pragmatic-Clean-Architecture.md`
- `docs/adr/ADR-002-SQL-Durable-Command-Queue.md`
- `docs/adr/ADR-003-Collector-Separation-by-Security-Boundary.md`
- `docs/adr/ADR-006-Inventory-Ownership-Boundaries.md`
- `docs/tasks/WP-001-Solution-Skeleton.md`
- `docs/tasks/WP-002-Core-Persistence-Layer.md`
- `docs/tasks/WP-003-Configuration-Management.md`
- `docs/project/Principles.md`
- `AGENTS.md`

## 24. Approval record

| Role | Status | Date |
|---|---|---|
| Product Owner / Technical Lead | Pending review | |
| Architecture | Draft prepared | 2026-07-26 |

After approval, change the document status from `Draft` to `Approved`.
