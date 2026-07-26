---
title: PSM Operations Platform
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# PSM Operations Platform

**Observe. Understand. Operate.**

PSM Operations Platform is an internal operations platform for one IT department managing up to approximately 1,000 Windows servers and related services.

Version 1 focuses on Windows Server, Windows Services, IIS, SQL Server, performance counters, event logs, certificates, dashboards, alerts, audit and controlled operations.

The product is deliberately not a multi-tenant SaaS platform, plugin marketplace or generic monitoring framework.

## Start here

Read [`docs/index.md`](docs/index.md).

## Core architecture

- .NET 10 and Blazor Interactive Server
- SQL Server as the central durable store
- Windows Authentication and AD group-based authorization
- Separate Windows Collector and SQL Collector services
- SQL-backed durable command queue
- gMSA-based least privilege
- Pragmatic Clean Architecture
