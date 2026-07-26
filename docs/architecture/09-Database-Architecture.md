---
title: Database Architecture
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Database Architecture

Use one central SQL Server database with schemas: `inventory`, `monitoring`, `collection`, `operations`, `security`, `audit` and `configuration`. Collector identities receive only required schema permissions. Time-series tables require retention. SQL Server is the durable queue; SignalR is presentation-only.
