---
title: Contributing Guide
version: 1.0.0
status: Approved
owner: Engineering
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Contributing

All significant changes follow this sequence:

1. Product decision
2. Architecture review
3. ADR when required
4. Work Package
5. Implementation
6. Build and tests
7. Review
8. Merge

Future implementation work also follows
[`docs/engineering/Development-Process-Freeze-v1.0.md`](docs/engineering/Development-Process-Freeze-v1.0.md):
an approved repository specification under `workpackages/` precedes
implementation, prompts only launch specifications, and Architecture,
Implementation, and Repository reviews precede approval.

Use branches named `docs/<topic>`, `feature/<wp-id>-<name>` or `fix/<id>-<name>`.

Pull requests must identify related Work Packages, ADRs, validation evidence, security impact and operational impact.
