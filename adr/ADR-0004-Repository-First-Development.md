# ADR-0004 — Repository-First Development

## Status

Accepted

## Date

2026-07-29

## Context

Repeatable delivery requires the repository—not a live environment—to contain the complete product definition.

## Decision

The repository is the product; deployment is validation. Complete source, deterministic tests, forward-only migrations, documentation, and static idempotent SQL review locally first. Do not run automatic migrations. Package-first/local-first deployment artifacts derive from reviewed repository state. Repository Complete work does not contact real infrastructure; integration is deferred to the approved gate.

## Consequences

Repository validation can complete without SQL Server or target connectivity. Runtime validation remains explicitly pending.

## Security Impact

Environment secrets and privileged infrastructure access are excluded from normal implementation work.

## Migration/Compatibility Impact

Migrations are additive/forward-only, are generated and reviewed statically, and are not applied in Repository Complete packages.

## Alternatives Considered

Environment-first development, automatic migration application, and direct live troubleshooting were rejected.

## Related Documents

- [Engineering Standards](../docs/engineering/PSM-Engineering-Standards.md)
- [Development Process Freeze](../docs/engineering/Development-Process-Freeze-v1.0.md)

## Supersession Rules

Only a later accepted ADR explicitly superseding ADR-0004 may change this policy.
