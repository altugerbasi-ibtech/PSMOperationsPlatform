---
title: Development Process Freeze v1.0
version: 1.0.0
status: Approved and Active
owner: Engineering
last_updated: 2026-07-29
reviewers:
  - Architecture
product: PSM Operations Platform
---
# Development Process Freeze v1.0

The accepted decision record is [ADR-0002](../../adr/ADR-0002-Development-Process-Freeze.md).

## Purpose

This document is the authoritative process baseline for specifying,
implementing, validating, and reviewing future PSM Operations Platform work.
It establishes Specification-Driven Development and replaces large
prompt-owned specifications with repository-owned work-package specifications.

## Frozen development flow

```text
Engineering Standards
  -> Architecture
  -> Contracts
  -> Work Package Specification
  -> Implementation
  -> Architecture Review
  -> Implementation Review
  -> Repository Review
```

The following rules are frozen:

1. Engineering standards are authoritative.
2. Architecture documents define system boundaries.
3. Contracts define technical behavior.
4. Every implementation begins from an approved repository work-package
   specification.
5. Prompts only launch specifications.
6. Codex does not silently expand, reduce, or reinterpret scope.
7. Codex does not self-approve specifications, implementations, or reviews.
8. Every implemented package produces Architecture, Implementation, and
   Repository reviews.
9. Repository-first validation precedes integration.
10. Real integration remains isolated to the approved integration gate.
11. Architecture changes require an accepted ADR or approved Architecture
    Exception.
12. Specification amendments are explicit, version-visible, and reviewed
    before affected implementation resumes.
13. Implementation does not begin from missing, incomplete, draft, or
    unresolved templates.
14. Commit and push require explicit authorization.
15. Deployment and infrastructure contact are prohibited during
    Repository Complete packages unless a separately approved package
    explicitly authorizes them.

## Authorities

- [`PSM-Engineering-Standards.md`](PSM-Engineering-Standards.md) defines the
  engineering rules and precedence.
- [`../architecture/Architecture-Freeze-v1.0.md`](../architecture/Architecture-Freeze-v1.0.md)
  defines frozen product architecture.
- [`../../workpackages/README.md`](../../workpackages/README.md) defines
  specification ownership, statuses, amendments, and approval.
- [`../../prompts/Prompt-Framework-v2.md`](../../prompts/Prompt-Framework-v2.md)
  defines valid launch prompts.
- Applicable architecture, contracts, runtime, SDK, decision rules, ADRs, and
  Architecture Exceptions remain binding.

Historical work-package records under `docs/tasks/` remain evidence. Future
implementable specifications are authoritative under `workpackages/`.

## Freeze distinction

**Architecture Freeze v1.0** freezes product architecture, component
responsibilities, dependency direction, security boundaries, and deferred
layers.

**Development Process Freeze v1.0** freezes how work is specified, launched,
implemented, validated, reviewed, amended, and approved.

Neither freeze replaces the other. A process-compliant specification cannot
override architecture, and an architecture-compliant implementation cannot
bypass the development process.

## Specification readiness gate

A specification is implementable only when:

- its file exists under `workpackages/`;
- required metadata and sections are complete;
- status is `Approved for Implementation` or `In Implementation`;
- dependencies and deferrals are explicit;
- tests, documentation, validation, acceptance criteria, and completion line
  are complete;
- no unresolved placeholder or replacement field remains; and
- conflicts with Architecture Freeze, ADRs, contracts, or security standards
  are resolved.

Failure at this gate produces `NOT READY`; it does not authorize invention.

## Review and approval

Implementation prepares evidence using the repository review templates.
Readiness means ready for human review, not approved. Designated human
reviewers approve, request changes, supersede, or cancel the package.

Repository-complete evidence must not be presented as live integration.
Integration-pending items remain assigned to their named gate until that gate
records successful evidence.
