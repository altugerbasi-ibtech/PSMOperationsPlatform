---
title: Work Package Specifications
version: 1.0.0
status: Approved and Active
owner: Engineering
last_updated: 2026-07-29
reviewers:
  - Architecture
product: PSM Operations Platform
---
# Work Package Specifications

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
