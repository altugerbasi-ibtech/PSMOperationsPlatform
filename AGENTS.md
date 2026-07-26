---
title: AI Agent Instructions
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# AI Agent Instructions

1. Start with `docs/index.md`.
2. Treat `docs/project/Principles.md` as the primary decision filter.
3. Treat approved ADRs as binding decisions.
4. Implement only the active Work Package.
5. Do not invent missing requirements.
6. Do not introduce Linux, cloud, Kubernetes, multi-tenant or plugin SDK concepts without an approved ADR.
7. Preserve the Windows Collector and SQL Collector security boundary.
8. Do not combine Windows and SQL target permissions under one identity.
9. Do not execute real Start, Stop, Restart, Recycle, Kill or Reboot operations unless explicitly authorized by the Work Package.
10. Prefer small, readable and testable changes.
11. Run build and tests before completion.
12. Do not commit or merge unless explicitly requested.
13. Raise architecture conflicts rather than silently bypassing them.
