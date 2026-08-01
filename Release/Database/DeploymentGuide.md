---
title: PSM Operations Database Deployment Guide
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
# PSM Operations Database Deployment Guide

## Purpose and Boundary

This guide is the DBA handoff for deploying the WP-009.1 SQL package. An
authorized DBA executes the reviewed artifact under approved change control.
The application and Collector processes never apply migrations and must not
receive DDL permission.

This document does not authorize a deployment. Replace every
`<APPROVED-...>` value from the environment's approved deployment record.
Never store credentials or production connection strings in the repository or
release evidence.

WP-009.2 consumes the single approved
`Release/Deployment/DeploymentConfiguration.json` copy described in
`Release/Deployment/DeploymentConfiguration.md`. Its `SqlServer`,
`Deployment`, and `Security` values replace the placeholders in this guide;
parallel environment configuration files are not permitted.

## Supported SQL Server Versions

- Microsoft SQL Server 2022 or later.
- Database compatibility level `160`.
- A currently supported DBA client capable of executing SQLCMD-compatible SQL,
  such as `sqlcmd` or SQL Server Management Studio.
- Windows Integrated Authentication.

Later SQL Server releases remain subject to release-specific compatibility and
validation approval. Azure SQL and other database engines are not approved by
this guide.

## Required SQL Server Configuration

Before the change window, record and approve:

- SQL Server instance and database names.
- Database owner and deployment identity.
- Compatibility level `160`.
- Exact database collation.
- Recovery model: `FULL`, `SIMPLE`, or `BULK_LOGGED`.
- Backup location, retention, restore owner, and recovery objectives.
- Available data/log capacity and autogrowth policy.
- Encryption and certificate policy for the DBA connection.
- Maintenance window, application/Collector coordination, monitoring, and
  evidence location.

Do not change server or database configuration merely to match an example.
Any configuration change requires its own approval.

## Required Permissions

The deployment identity must:

- authenticate with Windows Integrated Authentication;
- have `CONNECT SQL` and `CONNECT` to the approved target database;
- have only the DDL rights required by the reviewed release SQL for the change
  window; and
- be able to read `dbo.__EFMigrationsHistory` and database catalog metadata for
  verification.

A DBA-controlled temporary database-owner assignment is an operational option
only when separately approved; remove temporary elevation immediately after
successful validation. Runtime Web, Windows Collector, and SQL Collector
identities must not receive `db_owner`, `db_ddladmin`, `ALTER`, `CONTROL`, or
other schema-management permission.

Read-only validation requires database access, metadata visibility (normally
`VIEW DEFINITION` or equivalent ownership visibility), and `SELECT` access to
`dbo.__EFMigrationsHistory`. WP-009.4 owns the authoritative runtime permission
matrix and excessive-permission validation.

## Collation

The repository does not prescribe an environment collation. The DBA must
record the approved database collation before deployment and compare it
exactly after deployment. Do not rebuild or change collation as part of this
package.

Supply the approved value to `SchemaValidation.sql` as
`ExpectedCollation`. A missing or mismatched value is a deployment blocker.

## Compatibility Level

The required compatibility level is `160`, matching SQL Server 2022. Verify it
before and after deployment. Changing compatibility level is not part of the
release SQL package and requires separate approval.

## Recovery Model

Recovery model is deployment-defined: `FULL`, `SIMPLE`, or `BULK_LOGGED`.
Record the approved value and confirm that backup operations support it.
For `FULL` or `BULK_LOGGED`, confirm an operational log-backup chain and
available log capacity. Do not change recovery model during this deployment
unless separately approved.

## Release Artifact Verification

Work from a copied, access-controlled release directory containing exactly:

```text
PSMOperations-v{Version}.sql
Manifest.json
Checksums.sha256
```

Confirm that `Manifest.json` has the approved `ProductVersion`, `GitCommit`,
`SQLScriptName`, source-derived UTC `BuildDate`, and SQL `SHA256`.

From PowerShell, verify every checksum entry before reviewing or executing SQL:

```powershell
$releasePath = Resolve-Path '.\Release\Database'
Push-Location $releasePath
try {
    Get-Content '.\Checksums.sha256' | ForEach-Object {
        if ($_ -notmatch '^([0-9A-F]{64}) \*(.+)$') {
            throw "Invalid checksum entry: $_"
        }
        $expected = $Matches[1]
        $artifact = $Matches[2]
        $actual = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
        if ($actual -cne $expected) {
            throw "Checksum mismatch: $artifact"
        }
    }
} finally {
    Pop-Location
}
```

Stop on any missing file, unexpected extra file, invalid manifest field, or
checksum mismatch. Obtain a new approved package; do not edit release files.

## Pre-Deployment Validation

1. Confirm the change record, approvers, maintenance window, and deployment
   owner.
2. Confirm SQL Server version, target instance/database, compatibility,
   collation, recovery model, capacity, and connectivity.
3. Confirm the deployment identity and temporary-elevation removal plan.
4. Verify artifact inventory, manifest version/commit, and checksums.
5. Review the complete idempotent SQL and its expected migrations.
6. Run `ValidationQueries.sql` and retain the result as pre-deployment
   evidence.
7. Run `SchemaValidation.sql` with approved database, collation, recovery, and
   expected schema-version inputs. Record existing failures; do not remediate
   them outside change control.
8. Complete and verify a backup appropriate to the approved recovery model.
   Confirm the restore owner and location; a backup without restore readiness
   is insufficient.
9. Confirm application and Collector coordination and active-session policy.
10. Stop if any prerequisite, approval, backup, identity, artifact, or target
    value is uncertain.

## Deployment Sequence

1. Open the approved change window and record the operator, target, package
   version, Git commit, and start time in the controlled change record.
2. Re-verify checksums from the execution directory.
3. Connect using the approved deployment identity and Windows Integrated
   Authentication.
4. Verify `DB_NAME()` is the approved target before executing any release SQL.
5. Execute the complete `PSMOperations-v{Version}.sql` artifact with a DBA
   client configured to stop on errors. Do not split, reorder, or edit it.
6. Capture exit status and safe deployment output without credentials,
   connection strings, or unnecessary SQL text.
7. On any error, stop. Do not mark the deployment successful and do not
   continue with manual object creation.
8. Run all post-deployment validation.
9. Remove any approved temporary elevation and verify its removal.
10. Record completion or failure and attach the approved evidence.

Example invocation placeholders:

```powershell
sqlcmd -S '<APPROVED-SQL-SERVER>' `
  -d '<APPROVED-DATABASE>' `
  -E -I -b -V 16 `
  -i '.\Release\Database\PSMOperations-v<APPROVED-VERSION>.sql' `
  -o '<APPROVED-EVIDENCE-PATH>\DatabaseDeployment.txt'
```

## Post-Deployment Validation

1. Confirm the client returned success and no batch error was suppressed.
2. Rerun `ValidationQueries.sql`; compare server/database configuration,
   latest migration, migration list, object counts, and permission diagnostics
   with pre-deployment evidence.
3. Run `SchemaValidation.sql` with all approved inputs and require `PASS`.
4. Verify the expected migration is the highest applied `MigrationId` and
   matches the approved release contract.
5. Verify compatibility level, collation, and recovery model are unchanged
   from the approved values.
6. Confirm the database is `ONLINE` and no unexpected user objects or
   migrations were introduced.
7. Verify temporary deployment elevation was removed.
8. Complete only separately approved application/Collector readiness checks.
9. Attach checksums, manifest, query results, schema-validation results,
   deployment output, and permission-removal evidence to the change record.

## Version Verification

Use `Manifest.json` for the product version and Git commit. Use
`dbo.__EFMigrationsHistory` for the deployed schema version. These are related
but distinct:

- `ProductVersion` identifies the release package.
- `GitCommit` identifies its source revision.
- the highest ordered `MigrationId` identifies deployed schema state.

Do not infer product version solely from a database migration name.

## Rollback Considerations

The idempotent release SQL is a forward-deployment artifact, not a rollback
script. EF down-migration SQL is not included.

Before deployment, approve one of:

- restore the verified pre-deployment backup; or
- apply a separately reviewed forward-fix package.

Rollback planning must address recovery point/time objectives, database and log
backup sequence, restore location, application/Collector coordination, data
created after deployment, and validation after recovery. Do not manually
remove migration-history rows or database objects. A failed deployment requires
DBA and release-owner assessment before retry.

## Common Troubleshooting

| Symptom | Likely cause | Safe response |
|---|---|---|
| Checksum mismatch | Damaged, edited, or mixed package | Stop and reacquire the approved package |
| Manifest version differs | Wrong package selected | Stop and reconcile release approval |
| Login or database access denied | Identity, mapping, or target error | Verify approved identity and target; do not broaden runtime rights |
| DDL permission denied | Deployment identity lacks an approved required right | Stop and have the DBA/security owner review the specific denial |
| Wrong database from `DB_NAME()` | Client connected to another catalog | Disconnect and correct the approved target |
| Compatibility mismatch | Target configuration differs | Stop; obtain configuration approval |
| Collation mismatch | Wrong database or unapproved configuration drift | Stop; do not change collation ad hoc |
| Recovery-model mismatch | Target or deployment record differs | Stop and reconcile backup/recovery ownership |
| SQL script reports an error | Migration prerequisite, state, or engine failure | Stop, preserve evidence, assess rollback |
| Expected migration missing | Script failed or wrong package/target | Treat as failed deployment |
| Unexpected migration present | Target drift or newer schema | Stop; do not force an older release |
| Schema validator reports missing objects | Incomplete deployment or insufficient metadata visibility | Distinguish permission visibility from actual schema state |

## Best Practices

- Use four-eyes review for artifact selection and target confirmation.
- Generate once and promote the same checksum-verified package.
- Keep release artifacts immutable and access-controlled.
- Use separate deployment and runtime identities.
- Keep Windows and SQL Collector target permissions under separate identities.
- Test backup restoration and the deployment sequence outside production.
- Capture pre/post evidence with target, version, commit, and change-record ID.
- Prefer a separately rehearsed forward fix when restore would discard valid
  post-deployment data.
- Stop on uncertainty; never repair schema or migration history ad hoc.
- Never enable application-startup migration or call `Database.Migrate()`.

## References

- [WP-009.1 release package documentation](README.md)
- [Schema validation guide](SchemaValidation.md)
- [WP-009.2 specification](../../workpackages/WP-009.2.md)
