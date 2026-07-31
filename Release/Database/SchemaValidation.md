---
title: WP-009.3 Schema Validation
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
# WP-009.3 Schema Validation

WP-009.3 consumes the approved deployment configuration in
`Release/Deployment/DeploymentConfiguration.json`. `SqlServer.Database`,
`CompatibilityLevel`, `Collation`, and `RecoveryModel` provide the matching
SQLCMD inputs. Validate the configuration before running this script.

## Purpose

`SchemaValidation.sql` is a standalone, read-only SQLCMD-compatible validation
package for an authorized DBA. It compares an already-deployed Operations
Database with the repository schema contract. It never creates, alters, drops,
updates, inserts into, or deletes from a target database object.

## Prerequisites

- SQL Server 2022 or later.
- SQLCMD, or SQL Server Management Studio with SQLCMD mode enabled.
- Windows Integrated Authentication.
- Connection to the database being validated.
- Metadata visibility for the target database, tables, indexes, keys, and
  constraints.
- The expected database name, approved database collation, and approved
  recovery model from the environment's deployment record.

Compatibility level 160 is the repository baseline. Collation and recovery
model are deliberately deployment-defined; the repository does not invent
defaults for them.

## Execute with sqlcmd

Use approved environment values and capture the output as release evidence:

```powershell
sqlcmd -S '<APPROVED-SQL-SERVER>' `
  -d '<APPROVED-DATABASE>' `
  -E -b -V 16 `
  -v ExpectedDatabaseName='<APPROVED-DATABASE>' `
     ExpectedCompatibilityLevel='160' `
     ExpectedCollation='<APPROVED-COLLATION>' `
     ExpectedRecoveryModel='<FULL|SIMPLE|BULK_LOGGED>' `
     ExpectedSchemaVersion='20260729191745_WP0088ExecutionHistory' `
  -i '.\Release\Database\SchemaValidation.sql' `
  -o '<APPROVED-EVIDENCE-PATH>\SchemaValidation.txt'
```

Do not place credentials or connection strings in the repository or evidence.
The example uses Windows Integrated Authentication and placeholders only.
Variable values must contain only the documented identifier/value forms.

For SSMS, connect to the target database, enable SQLCMD mode, supply the five
variables on the command line or replace only the `:setvar` values in an
approved evidence copy, then execute the complete batch.

## Result contract

The first result set contains exactly one row and one column:

- `PASS`: every configured database and repository schema expectation passed.
- `FAIL`: at least one expectation failed.

On `FAIL`, a second result set lists every detected issue with:

- `Category`
- `ObjectName`
- `ExpectedValue`
- `ActualValue`
- `Diagnostic`

Diagnostics are ordered by category and object name. Missing migration history
is reported once; dependent migration-row checks are then safely skipped.

## Validated scope

- Database existence and connected database name.
- Compatibility level, collation, and recovery model.
- `dbo.__EFMigrationsHistory`.
- Exact set of 17 expected migrations.
- Latest schema version
  `20260729191745_WP0088ExecutionHistory`.
- 40 required tables.
- 10 critical enabled indexes.
- 6 enabled and trusted foreign keys.
- 6 required primary keys.
- 1 required unique constraint.
- 4 persistent default constraints.

The object lists match
`tools/deployment/PSMOperationsDatabaseSchemaExpectation.json` and the current
EF migrations. Future approved migrations must update both authorities in one
reviewed release change.

## Interpreting failures

Treat every FAIL as a release blocker until an authorized DBA determines
whether the target is stale, incorrectly configured, inaccessible to the
validation identity, or based on an unapproved schema. Do not repair objects
manually from this output and do not grant the application DDL permission.
Apply only an approved release SQL artifact under change control, then rerun
validation.

The script reports metadata state only. It does not validate data correctness,
capacity, backups, restore viability, performance, permissions needed by each
runtime identity, or rollback readiness. Those checks remain part of WP-007.Z.
