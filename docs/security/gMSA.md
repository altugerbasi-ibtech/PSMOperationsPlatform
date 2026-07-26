---
title: gMSA Strategy
version: 1.0.0
status: Approved
owner: Security
last_updated: 2026-07-26
product: PSM Operations Platform
---
# gMSA Strategy

Use separate gMSA identities for Web, Windows Collector, SQL Collector and future Windows Action Executor. Do not grant one identity both Windows administration and SQL monitoring rights for convenience. Document service hosts, target local groups, SQL logins and database roles.
