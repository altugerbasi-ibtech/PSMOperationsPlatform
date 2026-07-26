---
title: Security Architecture
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Security Architecture

| Component | Suggested identity | Permission area |
|---|---|---|
| Web | `gMSA-PSMWeb$` | Application database only |
| Windows Collector | `gMSA-PSMWindows$` | Read-only Windows/IIS/performance |
| SQL Collector | `gMSA-PSMSql$` | Approved SQL metadata and DMV access |
| Windows Action Executor | `gMSA-PSMAction$` | Future privileged operations |

Users authenticate with Windows Authentication. AD groups map to application roles and explicit permissions.
