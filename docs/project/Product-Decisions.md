---
title: Product Decisions Log
version: 1.0.0
status: Approved
owner: Product
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Product Decisions Log

## PD-001 — Product name
Use `PSM Operations Platform`.

## PD-002 — Departmental scale
Optimize for one department and approximately 1,000 Windows servers.

## PD-003 — Initial scope
Focus on Windows Server, IIS, SQL Server and Windows Services.

## PD-004 — Avoid generic expansion
Linux, cloud, VMware and plugin SDK are outside version 1.

## PD-005 — Collector separation
Separate Windows and SQL collection by security boundary and gMSA identity.

## PD-006 — Privileged operations
Read-only collection comes first. Privileged actions will use a separate Windows Action Executor.
