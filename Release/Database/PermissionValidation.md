---
title: WP-009.4 Database Permission Validation
version: 1.0.0
status: Implemented — Review Pending
owner: Release Engineering
last_updated: 2026-07-30
reviewers:
  - Architecture
  - Security
  - Operations
product: PSM Operations Platform
---
# WP-009.4 Database Permission Validation

WP-009.4 consumes the approved deployment configuration in
`Release/Deployment/DeploymentConfiguration.json`. The Collector, Portal, and
SQL Collector service-account values identify the environment mappings from
which the three database-user inputs are derived. Do not create a separate
principal configuration file.

## Purpose

`PermissionValidation.sql` validates effective Operations Database permissions
for the Windows Collector, Portal, and SQL Collector gMSA database users. It
reports missing required permissions and effective permissions prohibited by
the defined least-privilege profiles.

The script never creates users, grants, denies, or revokes permissions. It does
not modify data or schema.

## Prerequisites

- SQL Server 2022 or later.
- The three approved gMSAs already mapped as users in the target database.
- An authorized DBA validation identity permitted to inspect and impersonate
  those database users.
- SQLCMD or SQL Server Management Studio with SQLCMD mode.
- Windows Integrated Authentication.

Use database-user names exactly as mapped in SQL Server. Do not commit real
environment account names or credentials.

## Permission Profiles

| Profile | Database permissions | Required schema permissions | Prohibited database permissions |
|---|---|---|---|
| Collector | `CONNECT`, `SELECT`, `INSERT`, `UPDATE`, `DELETE` | Read/write on `configuration`, `collection`, `inventory`, `monitoring`, `operations`, `runtime`, `history` | `EXECUTE`, `VIEW DATABASE STATE` |
| Portal | `CONNECT`, `SELECT` | Read on `audit`, `collection`, `configuration`, `history`, `inventory`, `monitoring`, `operations`, `runtime` | `INSERT`, `UPDATE`, `DELETE`, `EXECUTE`, `VIEW DATABASE STATE` |
| SQL Collector | `CONNECT`, `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `VIEW DATABASE STATE` | Read/write on `collection`, `inventory`, `monitoring` | `EXECUTE` |

These profiles apply to the central Operations Database. Permissions used by
the SQL Collector against managed SQL Server targets are a separate security
boundary and require separate approval. Windows-target rights must never be
assigned to the SQL Collector identity.

## Execute

Supply approved database-user names and capture the output:

```powershell
sqlcmd -S '<APPROVED-SQL-SERVER>' `
  -d '<APPROVED-DATABASE>' `
  -E -b -V 16 `
  -v CollectorPrincipal='<APPROVED-COLLECTOR-DATABASE-USER>' `
     PortalPrincipal='<APPROVED-PORTAL-DATABASE-USER>' `
     SqlCollectorPrincipal='<APPROVED-SQL-COLLECTOR-DATABASE-USER>' `
  -i '.\Release\Database\PermissionValidation.sql' `
  -o '<APPROVED-EVIDENCE-PATH>\PermissionValidation.txt'
```

The validation identity must be able to use `EXECUTE AS USER` for each mapped
user. The script immediately executes `REVERT` after each profile.
Impersonation changes session context only.

## Interpretation

The first result set contains one value:

- `PASS`: all required permissions are effective and all prohibited permissions
  are absent.
- `FAIL`: at least one user is missing, a required permission is absent, a
  prohibited permission is effective, or permission visibility is unknown.

On `FAIL`, the second result set contains:

- `PrincipalRole`
- `PrincipalName`
- `Securable`
- `PermissionName`
- `ExpectedValue`
- `ActualValue`
- `Diagnostic`

Treat `NULL` as failure because SQL Server could not determine effective
permission in the tested context. Diagnostics are deterministically ordered.

## Remediation Boundary

This package does not prescribe or execute grants. An authorized DBA and
Security reviewer must determine the narrowest role or grant that satisfies an
approved profile. Rerun the validator after separately approved remediation.
Never grant `db_owner` or DDL rights to a runtime identity as a shortcut.

Live execution, evidence acceptance, managed-target permission validation, and
production certification remain under WP-007.Z.
