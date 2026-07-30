---
title: Work Packages
version: 1.26.0
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
9. [`WP-007.1`](../../workpackages/WP-007.1.md) — Ready for Review;
   deterministic DBA-owned SQL release package
10. [`WP-007.1.V`](../../workpackages/WP-007.1.V.md) — Ready for Review;
    standalone read-only schema validation package

11. [`WP-009`](../../workpackages/WP-009.md) — Release
    Engineering & Deployment Infrastructure epic
    - [`WP-009.1`](../../workpackages/WP-009.1.md) — Release SQL Package —
      Ready for Review
    - [`WP-009.2`](../../workpackages/WP-009.2.md) — Database Deployment Guide —
      Ready for Review
    - [`WP-009.3`](../../workpackages/WP-009.3.md) — Schema Validation —
      Ready for Review
    - [`WP-009.4`](../../workpackages/WP-009.4.md) — Database Permission Validation —
      Ready for Review
    - [`WP-009.5`](../../workpackages/WP-009.5.md) — Release Verification Package —
      Ready for Review
    - [`WP-009.6`](../../workpackages/WP-009.6.md) — Release Bundle Generator —
      Ready for Review
    - [`WP-009.7`](../../workpackages/WP-009.7.md) — Release Acceptance Test —
      Ready for Review
      - [`Completion report`](WP-009.7-Completion-Report.md)

WP-009.1 implements release-generation infrastructure and WP-009.2 adds DBA
documentation plus read-only validation queries, and WP-009.3 adds standalone
read-only schema validation, WP-009.4 adds read-only effective-permission
validation, WP-009.5 adds read-only post-deployment infrastructure
verification, WP-009.6 adds one-command bundle generation, and WP-009.7 adds
release-acceptance reporting and production-readiness decisions. They authorize
no runtime change, remediation, or live deployment.

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.26.0 | 2026-07-30 | Recorded WP-009.7 Release Acceptance Test ready for review |
| 1.25.0 | 2026-07-30 | Recorded WP-009.6 Release Bundle Generator ready for review |
| 1.24.0 | 2026-07-30 | Recorded WP-009.5 Release Verification Package ready for review |
| 1.23.0 | 2026-07-30 | Recorded WP-009.4 Database Permission Validation ready for review |
| 1.22.0 | 2026-07-30 | Recorded WP-009.3 Schema Validation ready for review |
| 1.21.0 | 2026-07-30 | Recorded WP-009.2 Database Deployment Guide ready for review |
| 1.20.0 | 2026-07-30 | Recorded WP-009.1 Release SQL Package ready for review |
| 1.19.0 | 2026-07-30 | Indexed the Draft WP-009 release-engineering epic and five children |
| 1.18.0 | 2026-07-30 | Added WP-007.1.V schema validation package |
| 1.17.0 | 2026-07-30 | Added WP-007.1 release SQL package review records |
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
