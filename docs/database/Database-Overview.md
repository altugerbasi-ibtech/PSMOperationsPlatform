---
title: Database Overview
version: 1.1.0
status: Approved
owner: Database
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Database Overview

The first release uses one SQL Server database and schema isolation rather than separate databases. Collector identities are not database owners. Application migrations run under a dedicated deployment identity or controlled release process.

WP-004.5 stores Windows target last-known connectivity on
`configuration.ManagedServer`: state, separate attempt/success timestamps,
last successful transport, consecutive failure count, next eligibility, safe
failure category and rowversion. The controlled
`AddManagedServerConnectivityState` migration adds these fields without
history, raw errors, credentials or automatic startup migration.
