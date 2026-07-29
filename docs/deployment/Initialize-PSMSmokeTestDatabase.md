---
title: Initialize PSM Smoke Test Database
version: 1.0.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Security
product: PSM Operations Platform
---
# Initialize PSM Smoke Test Database

## Purpose

`tools/deployment/Initialize-PSMSmokeTestDatabase.ps1` prepares the minimum SQL
Server security state required for the controlled WP-005 smoke test. It is an
operator-run deployment utility, not runtime product code.

## Scope

The utility may create one non-production database, one Windows login, its
mapped database user, and memberships in `db_datareader` and `db_datawriter`.
It does not apply migrations, create application schema/tables, configure a
collector, change AD, or modify Windows.

Repository tooling is separated as follows:

- `tools/deployment` contains controlled utilities that change an environment.
- `tools/readiness` contains read-only validation utilities.
- `tools/diagnostics` is reserved for read-only diagnostic/export utilities.

This Work Package adds only the deployment utility and does not alter readiness
or create an empty diagnostics utility.

## Defaults and parameters

| Parameter | Default |
|---|---|
| `Server` | `mydb01.ae.local` |
| `Database` | `PSMOperationsPlatform_SmokeTest` |
| `ServiceAccount` | `AE\gmsaSPWorker$` |
| `ReportPath` | No report is written |

Only parameter defaults contain the environment-specific values. Custom values
replace only the supplied parameters.

```powershell
.\tools\deployment\Initialize-PSMSmokeTestDatabase.ps1
```

```powershell
.\tools\deployment\Initialize-PSMSmokeTestDatabase.ps1 `
    -Server 'mydb02.ae.local' `
    -Database 'PSMOperationsPlatform_Test' `
    -ServiceAccount 'AE\gmsaOtherCollector$'
```

## Prerequisites

- Windows PowerShell 5.1 or later.
- SQL Server 2022 or later, consistent with the repository baseline.
- The approved `SqlServer` PowerShell module and `Invoke-Sqlcmd`.
- Network/TLS access to the SQL Server.
- Windows Integrated Authentication.
- An executing identity permitted to inspect server/database principals and
  roles and to create the approved database, login, user, and role memberships.
- A confirmed non-production target.

The utility never installs a module or package. It fails safely when the SQL
client is unavailable. It does not accept credentials, passwords, SQL
Authentication, or raw connection strings.

## WhatIf and Confirm

```powershell
.\tools\deployment\Initialize-PSMSmokeTestDatabase.ps1 -WhatIf
```

`-WhatIf` performs connectivity and read-only state checks. Each missing
database, login, user, or role membership is reported as `PLANNED`; no mutation
SQL is sent. Exit code 3 means required changes were not applied. If the target
is already ready, `-WhatIf` may return READY and exit 0.

```powershell
.\tools\deployment\Initialize-PSMSmokeTestDatabase.ps1 `
    -Server 'mydb02.ae.local' `
    -Database 'PSMOperationsPlatform_Test' `
    -ServiceAccount 'AE\gmsaOtherCollector$' `
    -Confirm
```

Each of the five logical mutations has its own confirmation boundary. Declined
operations are reported as planned/not applied.

## Idempotency and reruns

Every creation or role assignment has a read-only existence check, a
server-side `IF` guard, and post-operation verification. A completed second run
performs no mutation. A partial failure is reported without dropping or
rolling back completed objects; rerunning safely completes missing steps.
Server-scope and database-scope operations are not placed in one transaction.

## Database, login, user, and roles

Database creation uses SQL Server defaults and verifies that the database is
ONLINE. The utility does not alter recovery, compatibility, file, collation,
owner, encryption, or containment settings.

The login is created only as `FROM WINDOWS`. Existing principal type, disabled
state, database-user mapping, orphan state, and authentication type are
verified. Conflicts fail for manual correction and are never remapped or
enabled automatically.

Only `db_datareader` and `db_datawriter` are granted. The utility never grants
`db_owner`, `db_ddladmin`, `db_securityadmin`, `sysadmin`, `securityadmin`,
`serveradmin`, `setupadmin`, `processadmin`, `diskadmin`, or `bulkadmin`.

## Higher privilege warnings

Existing direct memberships in `db_owner`, `db_ddladmin`,
`db_securityadmin`, `sysadmin`, `securityadmin`, and `serveradmin` produce a
warning. They are not removed or changed. SQL Server membership metadata may
not completely resolve effective privileges inherited through nested AD
groups; the report does not claim otherwise.

## Output and exit codes

Normalized results contain `Name`, `Status`, `Summary`, `Changed`, and
`Recommendation`. Statuses are PASS, WARNING, FAIL, PLANNED, and SKIPPED.

| Exit code | Overall | Meaning |
|---:|---|---|
| 0 | READY | Required state is ready |
| 1 | WARNING | Required state is ready but higher privilege exists |
| 2 | FAILED | Validation, SQL operation, verification, or report write failed |
| 3 | WHATIF | Required changes were planned but not applied |

Example:

```text
[PASS] Connectivity — Connected to mydb01.ae.local with Windows Integrated Authentication.
[PASS] Database — Already exists and is ONLINE.
[PASS] Login — Already exists as an enabled Windows principal.
[PASS] User — Already exists with the correct login mapping.
[PASS] db_datareader — Already assigned.
[PASS] db_datawriter — Already assigned.
[WARNING] HigherPrivileges — Direct SQL role memberships detected: db_owner.
Overall:        WARNING
Exit Code:      1
```

## Markdown report

No file is written by default. Supply an explicit file path whose parent
directory already exists:

```powershell
.\tools\deployment\Initialize-PSMSmokeTestDatabase.ps1 `
    -ReportPath 'C:\PSMEvidence\Initialize-PSMSmokeTestDatabase-Report.md'
```

The report write is independently guarded by ShouldProcess. The utility does
not create directories. The report contains a manifest, results, detected
higher privileges, manual actions, and a security confirmation. It contains no
raw connection string or secret.

## Security model

Inputs are fully validated before SQL client detection or connectivity.
Identifiers use explicit allowlists and SQL bracket quoting; data literals are
bounded and safely encoded. Queries are fixed and deterministic. Exception
details, stack traces, credentials, and raw connection strings are suppressed.

The Windows Collector and SQL Collector security boundary remains unchanged.
This utility grants the named Windows identity only the two stated database
roles and does not combine target permissions or identities.

## Out of scope

WP-005 migration `20260727230000_AddWindowsInventoryCurrentState` is not
invoked. Schema/table creation, seed data, service deployment/control, gMSA or
AD changes, firewall/WinRM/certificate/registry changes, SQL Agent, production
initialization, and automatic rollback/drop remain out of scope.

## References

- [WP-005.S1](../tasks/WP-005.S1-Controlled-Lab-Smoke-Test.md)
- [WP-005.S2](../tasks/WP-005.S2-Collector-Environment-Validation.md)
- [WP-005 inventory migration](../database/WP-005-Inventory-Migration.md)
- [Engineering principles](../project/Principles.md)

## Revision History

| Version | Date | Description |
|---|---|---|
| 1.0.0 | 2026-07-27 | Added controlled smoke-test database initialization guidance |
