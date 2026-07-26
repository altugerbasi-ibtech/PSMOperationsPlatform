---
title: ADR-002 — SQL Durable Command Queue
version: 1.0.0
status: Accepted
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# ADR-002 — SQL Durable Command Queue

Use SQL Server as the durable collector command queue. SignalR is limited to UI notification. This avoids additional messaging infrastructure but requires tested leasing, retry and dead-letter behavior.
