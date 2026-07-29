---
title: Work Package Launch Template
version: 2.0.0
status: Template — not executable until all fields are resolved
owner: Engineering
last_updated: 2026-07-29
product: PSM Operations Platform
---
# Work Package Launch Template

This is a short launcher, not a technical specification. Replace every
double-braced field before use. An unresolved field makes the prompt invalid.

```text
PSM OPERATIONS PLATFORM

{{WORK_PACKAGE_ID}} — {{WORK_PACKAGE_TITLE}}

CURRENT PROJECT PHASE

Repository Complete

Architecture Freeze v1.0

Read and follow strictly:

- docs/engineering/PSM-Engineering-Standards.md
- docs/contracts/*
- docs/architecture/*
- docs/runtime/*
- docs/sdk/*
- docs/decision-rules/*
- {{SPECIFICATION_PATH}}

Implement the referenced work package only.

Do not violate Architecture Freeze v1.0.

Do not deploy, contact real infrastructure, apply migrations, commit or push.

Finish with exactly:

{{READY_LINE}}

or:

NOT READY
```

## Launcher validation checklist

- [ ] `{{WORK_PACKAGE_ID}}` is replaced with the approved identifier.
- [ ] `{{WORK_PACKAGE_TITLE}}` matches the specification metadata.
- [ ] `{{SPECIFICATION_PATH}}` is replaced with an existing repository file.
- [ ] The specification status permits implementation.
- [ ] The specification has no unresolved placeholders or incomplete required
      sections.
- [ ] `{{READY_LINE}}` exactly matches the specification.
- [ ] Prohibited operations match the current project phase.
- [ ] No replacement field remains.
