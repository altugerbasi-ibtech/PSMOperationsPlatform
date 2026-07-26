---
title: Collector Architecture
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Collector Architecture

Collectors are separated by target permission boundary.

**Windows Collector:** WinRM, OS discovery, performance counters, Windows Services, IIS, event logs and certificates.

**SQL Collector:** SQL connectivity, instance and database discovery, sessions, connections, blocking, waits and file usage.

**Windows Action Executor:** future privileged actions.

Every queued command contains a collector type: `Windows`, `Sql` or `WindowsAction`. Each service leases only its own type.
