---
title: Domain Model
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Domain Model

Initial concepts: Server, WindowsInventorySnapshot, WindowsService, IisSite, IisApplication, IisApplicationPool, SqlInstance, SqlDatabase, MetricSample, Alert, CollectorCommand, CollectorInstance, AuditEvent and ConfigurationSetting.

Explicit infrastructure concepts are preferred over a generic ManagedObject inheritance tree. Inventory and monitoring are separate concerns.
