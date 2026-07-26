---
title: ADR-003 — Collector Separation by Security Boundary
version: 1.0.0
status: Accepted
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# ADR-003 — Collector Separation by Security Boundary

Use separate Windows Collector and SQL Collector services under different gMSA identities. Introduce a separate Windows Action Executor for privileged actions. This increases service count but preserves least privilege.
