# PSM Operations Platform

## Current Engineering Status

The repository contains the .NET 10 solution foundation, SQL Server persistence,
configuration composition, Windows Collector host and target connectivity, and
the completed WP-005 Windows inventory framework. Seven explicit inventory
modules collect and persist validated current state through ownership-focused
stores. Read-only environment and deployment-validation tooling is implemented.
The controlled-lab runbook and evidence package are prepared, but live Phase B
execution and production certification are not complete.

## Current Release

| Field | Value |
|---|---|
| Release | v1.0.0-rc1 |
| Status | WP-008.9 Approved — Release Candidate Capture |
| Repository State | Technically Ready; Immutable Revision Being Captured |

## Completed Milestones

- WP-001 — Solution Skeleton
- WP-002 — Core Persistence Layer
- WP-003 — Configuration Management
- WP-004 — Windows Collector Foundation and Target Connectivity
- WP-005 — Windows Inventory Framework
- WP-005.2 — Session Ownership and Inventory Orchestration Foundation
- WP-005.3 — Inventory Persistence Foundation
- WP-005.S1 — Controlled Lab Smoke Test Preparation
- WP-005.S2 — Collector Environment Validation Framework
- WP-008.4 — Collector Runtime (Approved; integration pending)
- WP-008.5 — Execution Dispatcher (Approved; integration pending)
- WP-008.6 — Collector Plugin SDK (Approved; integration pending)
- WP-008.7 — Execution Monitoring (Approved; integration pending)
- WP-008.7.Q — Execution Monitoring Quality Completion (Approved; integration pending)
- WP-008.8 — Execution History (Approved; integration pending)

WP-005.S1 is preparation-complete; its controlled-lab execution phase has not
been authorized or executed.

## Architecture Highlights

- .NET 10, ASP.NET Core, Blazor Interactive Server, EF Core 10, and SQL Server
  2022 or later form the approved technology baseline.
- Pragmatic Clean Architecture and explicit project dependency rules are
  established by ADR-001.
- Windows Authentication, Windows Integrated SQL authentication, gMSAs, and
  separate Windows and SQL collector identities preserve security boundaries.
- Host configuration uses ordered JSON, Development User Secrets,
  `PSM__`-prefixed environment variables, and command-line providers.
- Windows collection uses target-specific WinRM with HTTPS-first `Auto`,
  `HttpsOnly`, or `HttpOnly` policy and controlled HTTP fallback.
- Collector scheduling, timestamps, timeout behavior, and tests use
  `TimeProvider`.
- Seven compile-time Windows inventory modules use explicit projections,
  validation, stores, and deterministic ordering without runtime plugins.
- ADR-006 defines ownership-boundary persistence: singular state updates and
  validated transactional replace-all for plural snapshots.
- Readiness tooling provides non-interactive CollectorHost and SmokeTest
  validation, deterministic JSON/Markdown reports, redaction, and fixed exit
  semantics without remediation.

## Validation Status

| Area | Status |
|---|---|
| Windows Collector implementation | Architecture review passed |
| Automated repository validation | Implemented |
| Collector environment validation | Implemented |
| Configuration-source validation | Implemented |
| CollectorHost deployment validation | Implemented |
| Controlled-lab smoke-test preparation | Complete |
| Controlled-lab smoke-test execution | Pending explicit execution gate |
| Production certification | Not completed |

## Known Limitations

- WP-005.S1 Phase B controlled-lab execution has not been authorized or
  completed.
- Production deployment validation and certification have not been completed.
- Windows Service discovery and inventory are planned but not implemented.
- IIS discovery and health capabilities are not implemented.
- SQL Server discovery and health capabilities are not implemented.
- DNS Alias Discovery remains a separate future work package.
- Durable command queue behavior remains planned; ADR-002 defines its
  architectural direction.
- ADR-005 has been restored from repository-supported authority; timestamp
  governance is no longer a release-readiness blocker.

## Next Milestone

The next gate is a repeated Repository Readiness Review v1.0 of WP-008.9.
WP-007.Z Real Integration and Production Validation remains deferred and not
started. Live WinRM, SQL, IIS, inventory, monitoring, history, deployment,
installer, rollback, and upgrade evidence may be collected only under that
separately authorized integration package.

The authoritative immutable source record for that gate is
[WP-007.Z Golden Baseline](docs/release/WP-007.Z-Golden-Baseline.md).

## Repository Health

| Area | Assessment |
|---|---|
| Architecture | Established — accepted ADRs and explicit security, ownership, and dependency boundaries |
| Testing | High — unit, persistence, architecture, collector, readiness, and safety coverage is present |
| Documentation | High — architecture, work packages, security, deployment preparation, and testing guidance are maintained |
| Deployment | Preparation complete — controlled-lab execution and production certification remain pending |
| Maintainability | High — explicit modules, stores, configuration providers, and test seams avoid hidden runtime behavior |
| Technical Debt | Low but visible — roadmap/baseline numbering inconsistency remains documented |

## Version History

| Release | Status | Date | Notes |
|---|---|---|---|
| 0.1.0 | Controlled Lab Preparation Complete | 2026-07-27 | Initial engineering release summary |
