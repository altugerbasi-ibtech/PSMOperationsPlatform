---
title: gMSA Strategy
version: 1.2.0
status: Approved
owner: Security
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Operations
product: PSM Operations Platform
---
# gMSA Strategy

Use separate gMSA identities for Web, Windows Collector, SQL Collector and future Windows Action Executor. Do not grant one identity both Windows administration and SQL monitoring rights for convenience. Document service hosts, target local groups, SQL logins and database roles.

## WP-004 boundary

The Windows Collector gMSA runs only the read-oriented collector service.
It may receive least-privilege OperationsDatabase permissions to read eligible
targets and update connectivity state, plus target permission to open and close
an authenticated WinRM session.

It MUST NOT receive SQL Collector metadata/DMV permissions, Web identity
permissions or Windows Action Executor privileges. No shared credential,
explicit password, `PSCredential` or application-managed secret is introduced.
Database and target access use Windows Integrated Authentication with the
process identity.

## Revision history

| Version | Date | Description |
|---|---|---|
| 1.2.0 | 2026-07-27 | Synchronized the identity boundary with completed WP-004 |
| 1.1.0 | 2026-07-27 | Added proposed WP-004 identity and least-privilege boundary |
| 1.0.0 | 2026-07-26 | Initial gMSA strategy |
