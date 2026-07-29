# ADR-0002 — Development Process Freeze v1.0

## Status

Accepted

## Date

2026-07-29

## Context

Large prompts duplicated scope and weakened repository ownership.

## Decision

Use Specification-Driven Development. Repository-owned, approved work-package specifications define scope; prompts only launch them. Every package produces Architecture, Implementation, and Repository reviews after repository-first validation. Scope cannot expand silently, Codex cannot self-approve, and amendments must be explicit and reviewed. Commit, push, deployment, and infrastructure contact require explicit authorization. Architecture changes additionally require an ADR or Architecture Exception.

## Consequences

Incomplete specifications and unresolved templates are non-implementable. Integration remains isolated to its approved gate.

## Security Impact

Repository-complete work cannot contact infrastructure or broaden target permissions implicitly.

## Migration/Compatibility Impact

No product schema impact. Existing work adopts the process through approved specifications.

## Alternatives Considered

Prompt-owned specifications and implicit implementation-time scope changes were rejected.

## Related Documents

- [Development Process Freeze](../docs/engineering/Development-Process-Freeze-v1.0.md)
- [Work Packages](../workpackages/README.md)
- [Prompt Framework](../prompts/Prompt-Framework-v2.md)

## Supersession Rules

Only a later accepted process ADR explicitly superseding ADR-0002 may change this decision.
