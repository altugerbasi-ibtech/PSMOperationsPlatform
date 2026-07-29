---
title: Product Roadmap
version: 1.19.0
status: Draft
owner: Product
last_updated: 2026-07-27
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
