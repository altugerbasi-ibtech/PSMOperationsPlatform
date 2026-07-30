---
title: Work Packages
version: 1.16.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
reviewers:
  - Architecture
product: PSM Operations Platform
---
# Work Packages

A Work Package is the implementation contract for one bounded change. It includes objective, scope, design, security, acceptance criteria, tests and documentation updates. Give an AI coding agent only one active Work Package at a time.

This directory retains historical work-package and review evidence. Future
Specification-Driven Development uses authoritative implementable
specifications under [`../../workpackages/`](../../workpackages/), governed by
Development Process Freeze v1.0. Do not create a competing future
specification in both locations.

## Index

1. [`WP-001-Solution-Skeleton.md`](WP-001-Solution-Skeleton.md) — Completed
2. [`WP-002-Core-Persistence-Layer.md`](WP-002-Core-Persistence-Layer.md) — Completed
3. [`WP-003-Configuration-Management.md`](WP-003-Configuration-Management.md) — Completed
4. [`WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md`](WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md) — Completed
5. [`WP-005-Windows-Inventory-Framework.md`](WP-005-Windows-Inventory-Framework.md)
   — Completed; final architecture review passed
   - [`WP-005.2-Implementation.md`](WP-005.2-Implementation.md)
   - [`WP-005.3-Inventory-Persistence-Foundation.md`](WP-005.3-Inventory-Persistence-Foundation.md)
   - [`WP-005.S1-Controlled-Lab-Smoke-Test.md`](WP-005.S1-Controlled-Lab-Smoke-Test.md)
   - [`WP-005.S2-Collector-Environment-Validation.md`](WP-005.S2-Collector-Environment-Validation.md)

6. [`WP-006-Windows-Collector-Production-Validation.md`](WP-006-Windows-Collector-Production-Validation.md)
   - WP-006.2A deployment tooling implemented; live deployment and validation
     not yet executed
7. WP-008.4 through WP-008.8 — Approved; integration pending WP-007.Z
8. [`WP-008.9`](../../workpackages/WP-008.9.md) — Approved;
   Repository Readiness Review v1.0 remediation

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.16.0 | 2026-07-30 | Reconciled WP-001 completion and indexed approved WP-008 remediation state |
| 1.15.0 | 2026-07-27 | Implemented WP-006.2A deployment package; live deployment pending |
| 1.14.0 | 2026-07-27 | Prepared WP-006.2 controlled execution package |
| 1.13.0 | 2026-07-27 | Approved WP-006.1 and opened controlled execution planning |
| 1.12.0 | 2026-07-27 | Added WP-006 production validation analysis draft |
| 1.11.0 | 2026-07-27 | Added completed WP-005.S2 readiness tooling |
| 1.10.0 | 2026-07-27 | Added WP-005.S1 controlled lab preparation package |
| 1.9.0 | 2026-07-27 | Recorded WP-005 final architecture review completion |
| 1.8.0 | 2026-07-27 | Recorded completed WP-005.2 foundation |
| 1.7.0 | 2026-07-27 | Added WP-005 Windows Inventory Framework analysis package |
| 1.6.0 | 2026-07-27 | Recorded WP-004 final review and completion |
| 1.5.0 | 2026-07-27 | Recorded WP-004.5 state persistence/backoff completion |
| 1.4.0 | 2026-07-27 | Recorded WP-004.4 connectivity probe completion |
| 1.3.0 | 2026-07-27 | Recorded WP-004.3 target provider and eligibility completion |
| 1.2.0 | 2026-07-27 | Recorded WP-004.2 host foundation completion |
| 1.1.1 | 2026-07-27 | Clarified WP-004 proposed documentation-only status |
| 1.0.0 | 2026-07-26 | Initial guidance |
| 1.1.0 | 2026-07-27 | Added Work Package index and WP-004 |
