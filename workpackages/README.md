---
title: Work Package Specifications
version: 1.8.0
status: Approved and Active
owner: Engineering
last_updated: 2026-07-30
reviewers:
  - Architecture
product: PSM Operations Platform
---
# Work Package Specifications

[`WP-007.Z.10`](WP-007.Z.10.md) provides the final evidence aggregation and
deterministic production-readiness gate. Its repository implementation is
approved; the current decision is `NOT_READY_FOR_PRODUCTION` because mandatory
Portal Windows Authentication is not composed, and no live evidence is inferred.

[`WP-007.Z.8`](WP-007.Z.8.md) validates Execution History persistence, query,
projection and retention readiness through the standard operational validation
toolkit. Repository implementation and human reviews are approved; live History
and Retention validation remain separate and were not executed.

[`WP-007.Z.7`](WP-007.Z.7.md) validates Portal and Execution Monitoring
readiness through the standard operational validation toolkit. Its repository
implementation and human reviews are approved; live Portal and Monitoring
validation remain separate, not executed, and not authorized by repository
approval.

[`WP-007.Z.6`](WP-007.Z.6.md) composes the approved Collector host, IIS, SQL,
and Operations database validation evidence into a deterministic end-to-end
Collector readiness decision. Its repository implementation is ready for
human review; live end-to-end validation remains separate and was not run.

[`WP-007.Z.5`](WP-007.Z.5.md) extends the WP-007.Z.3 operational validation
framework with current-identity, read-only SQL target validation. Its
repository implementation is ready for human review; live SQL validation
remains a separate WP-007.Z integration action.

[`WP-007.Z.4`](WP-007.Z.4.md) extends the WP-007.Z.3 operational validation
framework with read-only IIS target validation. Its repository implementation
is ready for human review; live IIS validation remains a separate WP-007.Z
integration action.

[`WP-009`](WP-009.md) establishes the Release Engineering & Deployment
Infrastructure epic. WP-009.1 through WP-009.7 are implemented and ready for
review:

- [`WP-009.1`](WP-009.1.md) — Release SQL Package — Ready for Review
- [`WP-009.2`](WP-009.2.md) — Database Deployment Guide — Ready for Review
- [`WP-009.3`](WP-009.3.md) — Schema Validation — Ready for Review
- [`WP-009.4`](WP-009.4.md) — Database Permission Validation — Ready for Review
- [`WP-009.5`](WP-009.5.md) — Release Verification Package — Ready for Review
- [`WP-009.6`](WP-009.6.md) — Release Bundle Generator — Ready for Review
- [`WP-009.7`](WP-009.7.md) — Release Acceptance Test — Ready for Review

WP-009 separates release engineering from runtime feature development.
WP-009.1 adds release tooling, WP-009.2 adds DBA documentation and read-only
queries, WP-009.3 adds standalone read-only schema validation, WP-009.4 adds
read-only effective-permission validation, WP-009.5 adds read-only
post-deployment verification scripts, and WP-009.6 adds one-command bundle
generation, and WP-009.7 adds deterministic RAT production-readiness
decisions and reports. They authorize no runtime, feature, business-logic, migration,
deployment, or production operation.

[`WP-008.9`](WP-008.9.md) is the release-gate remediation specification for
findings B1 through B5 from Repository Readiness Review v1.0. It is Approved,
adds no product capability, and defers live validation to WP-007.Z.

[`WP-007.1`](WP-007.1.md) defines the deterministic, idempotent SQL Server
release package generated from the existing EF Core migrations. It is ready
for human review; DBA execution remains deferred to WP-007.Z.

[`WP-007.1.V`](WP-007.1.V.md) defines the standalone read-only database schema
validation package. It is ready for human review; real SQL validation remains
deferred to WP-007.Z.

WP-008.5, WP-008.6, WP-008.7, WP-008.7.Q, and WP-008.8 are Approved from the
recorded human review. WP-008.4 is an approved historical package whose review
records remain under `docs/tasks/`. All remain integration-pending WP-007.Z.

WP-008.8 establishes durable Execution History while preserving the explicit
History-versus-Audit boundary. Future Audit work requires its own approved
specification and may not silently extend WP-008.8.

Quality-completion packages may use a suffix such as `.Q` and must name their
parent package. They remain full specifications with the same approval,
amendment, validation and three-review requirements and cannot broaden the
parent architecture silently.

## Purpose and authority

`workpackages/` is the authoritative location for future implementation
specifications under Specification-Driven Development. Historical delivery
records remain under `docs/tasks/`; they are not moved or rewritten.

A work-package specification is the bounded implementation contract. It owns
scope, behavior, acceptance criteria, tests, documentation, validation,
deferrals, integration constraints, and the completion line. A prompt only
launches that specification.

## Naming

Use `workpackages/WP-<number>[.<subnumber>].md`, matching the approved work
package identifier. One file owns one implementable package. Review evidence
uses the package ID and the Architecture, Implementation, and Repository review
types.

## Status lifecycle

1. `Draft` — under authoring; implementation prohibited.
2. `Approved for Implementation` — reviewed and implementable.
3. `In Implementation` — active repository work.
4. `Ready for Review` — implementation and repository validation complete.
5. `Approved` — designated human reviewers accepted the package.
6. `Superseded` — replaced by an identified newer specification.
7. `Cancelled` — intentionally closed without completion.

Codex may move an approved package to `In Implementation` or report it ready
when explicitly tasked, but may not self-approve a specification or review.

## Required content

Every specification uses the sections defined by
[`WorkPackage-Specification-Template.md`](WorkPackage-Specification-Template.md).
It must be complete, internally consistent, linked to repository authorities,
and free of unresolved placeholders before approval for implementation.

Implementation from an incomplete or missing specification is prohibited.
Codex reports the issue and does not invent requirements.

## Architecture Freeze, ADRs, and exceptions

Every package names the applicable Architecture Freeze version and extends the
frozen architecture without changing its boundaries. A conflicting change
requires an accepted ADR or an explicitly approved Architecture Exception
before implementation. A work package cannot authorize itself to bypass the
freeze.

## Ownership and review

Engineering owns specification completeness. Architecture reviews boundaries,
ADRs, exceptions, and freeze compliance. Security and Operations review when
their boundaries are affected. Final approval is a human governance action.

Every implemented package produces:

- Architecture Review;
- Implementation Review; and
- Repository Review.

Review templates are in [`prompts/`](../prompts/). They collect evidence and do
not imply approval.

## Scope control and amendment

Implementation must not silently expand, reduce, or reinterpret scope. When a
material requirement changes:

1. stop affected implementation;
2. edit the authoritative specification;
3. describe the amendment and affected criteria/tests/documents;
4. review architecture/security impact;
5. return the specification to `Approved for Implementation`; and
6. resume only from the approved revision.

Non-semantic typo or link corrections may be made without reopening scope, but
must remain visible in repository review.

## Integration-pending work

Repository validation and real integration are separate evidence. A package
lists every item deferred to its named Integration Gate. Repository-complete
work is marked integration-pending until that gate executes successfully; it
must not claim live validation.

## Final review and approval

After implementation, required commands and acceptance criteria are evaluated.
The package becomes `Ready for Review`, and the three review records are
prepared. Human reviewers either request changes or mark the package
`Approved`. A package is not approved merely because its completion line says
it is ready for review.

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.8.0 | 2026-07-30 | Recorded WP-009.7 Release Acceptance Test ready for review |
| 1.7.0 | 2026-07-30 | Recorded WP-009.6 Release Bundle Generator ready for review |
| 1.6.0 | 2026-07-30 | Recorded WP-009.5 Release Verification Package ready for review |
| 1.5.0 | 2026-07-30 | Recorded WP-009.4 Database Permission Validation ready for review |
| 1.4.0 | 2026-07-30 | Recorded WP-009.3 Schema Validation ready for review |
| 1.3.0 | 2026-07-30 | Recorded WP-009.2 Database Deployment Guide ready for review |
| 1.2.0 | 2026-07-30 | Recorded WP-009.1 Release SQL Package ready for review |
| 1.1.0 | 2026-07-30 | Added the Draft WP-009 release-engineering epic and child-package index |
| 1.0.0 | 2026-07-29 | Established authoritative work-package specification governance |
