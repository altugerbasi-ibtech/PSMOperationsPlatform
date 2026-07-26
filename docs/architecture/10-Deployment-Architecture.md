---
title: Deployment Architecture
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Deployment Architecture

Minimum components: IIS-hosted web application, Windows Collector service, SQL Collector service and central SQL Server database. Collectors may run together or separately. Target network flow is outbound from collectors to managed systems; targets do not initiate connections.
