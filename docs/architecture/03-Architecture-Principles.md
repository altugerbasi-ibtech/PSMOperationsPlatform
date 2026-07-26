---
title: Architecture Principles
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Architecture Principles

Use Clean Architecture pragmatically. Domain has no infrastructure dependency. Application coordinates use cases. Infrastructure implements persistence and integrations. Web owns UI and authorization entry points. Collectors own remote execution. CQRS is used only where it clarifies behavior.
