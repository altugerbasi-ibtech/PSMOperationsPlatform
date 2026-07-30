---
title: Product Roadmap
version: 1.27.0
status: Draft
owner: Product
last_updated: 2026-07-30
reviewers:
  - Architecture
  - Engineering
product: PSM Operations Platform
---
# Product Roadmap

1. Engineering foundation
2. Solution skeleton
3. Windows inventory and service discovery
4. Core monitoring and alerts
5. IIS discovery and health
6. SQL Server discovery and health
7. Controlled operations through Windows Action Executor
8. Reporting, performance validation and operational hardening
9. Release engineering and deployment infrastructure

WP-006 Windows Collector Production Validation has its controlled execution
package prepared. WP-006.2A deployment tooling is implemented; live deployment
and validation have not yet been executed.

## Delivery sequence

1. WP-001 — Solution Skeleton — Completed
2. WP-002 — Core Persistence Layer — Completed
3. WP-003 — Configuration Management — Completed
4. WP-004 — Windows Collector Foundation and Target Connectivity — Completed
5. WP-005 — Windows Inventory Framework — Completed; final architecture review
   passed
6. Windows Service discovery/inventory — Planned; number not assigned
7. DNS Alias Discovery — Future separate Work Package; number not assigned
8. Durable command queue behavior — Planned; number not assigned

9. [WP-009 — Release Engineering & Deployment Infrastructure](../../workpackages/WP-009.md)
   — Release-engineering delivery structure
   - WP-009.1 — Release SQL Package — Ready for Review
   - WP-009.2 — Database Deployment Guide — Ready for Review
   - WP-009.3 — Schema Validation — Ready for Review
   - WP-009.4 — Database Permission Validation — Ready for Review
   - WP-009.5 — Release Verification Package — Ready for Review
   - WP-009.6 — Release Bundle Generator — Ready for Review
   - WP-009.7 — Release Acceptance Test — Ready for Review

WP-009 separates production-deployment, DBA-handoff,
infrastructure-validation, and release-packaging deliverables from runtime
feature development. WP-009.1 implements release-generation infrastructure;
WP-009.2 adds DBA documentation and read-only validation queries; WP-009.3
adds standalone read-only schema validation; WP-009.4 adds read-only
effective-permission validation; WP-009.5 adds read-only post-deployment
infrastructure verification; WP-009.6 adds one-command bundle generation; and
WP-009.7 adds deterministic release-acceptance decisions and reports. All
WP-009 children are ready for human review; live execution remains deferred.

WP-005.S1 controlled lab validation is preparation-complete and awaits the
explicit non-production execution gate. WP-005 remains completed.
WP-005.S2 supplies the completed read-only environment validation tooling used
by that gate; it does not change WP-005 product behavior.

The older Architecture Baseline list assigns WP-004 to Durable Command Queue.
That entry is obsolete and requires an approved baseline revision. Remaining
future Work Packages are not silently renumbered here.

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.27.0 | 2026-07-30 | Recorded WP-009.7 Release Acceptance Test ready for review |
| 1.26.0 | 2026-07-30 | Recorded WP-009.6 Release Bundle Generator ready for review |
| 1.25.0 | 2026-07-30 | Recorded WP-009.5 Release Verification Package ready for review |
| 1.24.0 | 2026-07-30 | Recorded WP-009.4 Database Permission Validation ready for review |
| 1.23.0 | 2026-07-30 | Recorded WP-009.3 Schema Validation ready for review |
| 1.22.0 | 2026-07-30 | Recorded WP-009.2 Database Deployment Guide ready for review |
| 1.21.0 | 2026-07-30 | Recorded WP-009.1 Release SQL Package ready for review |
| 1.20.0 | 2026-07-30 | Added Draft WP-009 release-engineering epic and five planning packages |
| 1.19.0 | 2026-07-27 | Implemented WP-006.2A deployment package; live deployment pending |
| 1.18.0 | 2026-07-27 | Prepared WP-006.2 controlled execution package |
| 1.17.0 | 2026-07-27 | Approved WP-006.1 for controlled execution |
| 1.16.0 | 2026-07-27 | Added WP-006 production validation analysis status |
| 1.15.0 | 2026-07-27 | Completed WP-005.S2 collector readiness tooling |
| 1.14.0 | 2026-07-27 | Added post-implementation WP-005.S1 lab validation gate |
| 1.13.0 | 2026-07-27 | Passed WP-005 final architecture review |
| 1.12.0 | 2026-07-27 | Completed WP-005.7 Network Adapter and IPv4 inventory |
| 1.11.0 | 2026-07-27 | Completed WP-005.6 Disk and Volume inventory |
| 1.10.0 | 2026-07-27 | Completed WP-005.5 Processor inventory |
| 1.9.0 | 2026-07-27 | Completed WP-005.4 core system inventory |
| 1.8.0 | 2026-07-27 | Completed WP-005.3 current-state persistence foundation |
| 1.7.0 | 2026-07-27 | Completed WP-005.2 and advanced to current-state persistence foundation |
| 1.6.0 | 2026-07-27 | Added WP-005 sprint plan and separate future DNS Alias Discovery |
| 1.5.0 | 2026-07-27 | Completed WP-004 and advanced delivery to Windows OS inventory planning |
| 1.4.0 | 2026-07-27 | Recorded WP-004.5 persistence/backoff completion and final review next |
| 1.3.0 | 2026-07-27 | Recorded WP-004.3 target provider and eligibility completion |
| 1.2.0 | 2026-07-27 | Recorded WP-004.2 host foundation completion |
| 1.1.1 | 2026-07-27 | Clarified proposed WP-004 and separated later inventory scopes |
| 1.0.0 | 2026-07-26 | Initial roadmap |
| 1.1.0 | 2026-07-27 | Recorded approved WP-004 identity and reconciliation gap |
