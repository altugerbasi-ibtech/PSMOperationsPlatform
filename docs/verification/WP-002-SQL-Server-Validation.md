---
title: WP-002 SQL Server Validation
version: 1.0.0
status: Completed
owner: Engineering
last_updated: 2026-07-26
product: PSM Operations Platform
---
# WP-002 SQL Server Validation

## Validation scope

- Work package: WP-002 — Core Persistence Layer
- Validation date: 2026-07-26
- SQL Server product: SQL Server 2022
- SQL Server major version: 16
- Authentication type: Windows Authentication
- Test database type: isolated disposable database
- Test database name: `PSMOperationsPlatform_WP002_Test`
- Execution mode: controlled manual integration validation

This validation is opt-in and is not an automated CI test. No server hostname,
domain account, credential or real connection string is recorded in this
repository.

## Validation boundary

The validation evidence has three distinct parts:

1. Repository unit and infrastructure tests run automatically with `dotnet test`.
   They cover Domain behavior, EF Core model metadata, provider-independent
   persistence behavior with SQLite and generated SQL Server migration scripts.
2. Controlled real SQL Server validation was run manually against an isolated,
   disposable SQL Server database using Windows Authentication.
3. CI currently runs the repository tests only. It does not provision SQL Server
   or execute this controlled manual validation.

## Connection template

Only a placeholder connection template is documented:

```text
Server=<sql-server>;Database=<test-database>;Integrated Security=True;Encrypt=True;TrustServerCertificate=True
```

The deployment operator supplies approved values outside source control.

## Results

| Validation | Result |
|---|---|
| `InitialCreate` applied to an empty database | Passed |
| `__EFMigrationsHistory` contains `InitialCreate` | Passed |
| Six schemas exist | Passed |
| Seven WP-002 tables exist | Passed |
| Six restrictive foreign keys exist | Passed |
| Three unique indexes exist | Passed |
| Eight query indexes exist | Passed |
| Five check constraints exist | Passed |
| Two physical SQL Server `rowversion` columns exist | Passed |
| Valid JSON is accepted | Passed |
| Invalid JSON is rejected with SQL Server error 547 | Passed |
| JSON validation transaction is rolled back | Passed |
| Two-`DbContext` optimistic concurrency validation | Passed |
| Outer exception is `PersistenceConcurrencyException` | Passed |
| Error code is `persistence.concurrency_conflict` | Passed |
| Inner exception chain contains `DbUpdateConcurrencyException` | Passed |
| Only the first concurrent update persists | Passed |
| Validation records are removed | Passed |

The JSON checks were executed inside a transaction. The valid insert succeeded,
the invalid insert was rejected by its JSON check constraint with SQL Server
error 547, and the transaction was rolled back.

The concurrency check loaded one `CollectorNode` through two independent
`OperationsDbContext` instances. The first update succeeded. The second update
used the stale `rowversion` and surfaced the platform concurrency exception while
preserving the EF Core concurrency exception in its inner exception chain. A
follow-up read confirmed that only the first update persisted.

Test records were removed after validation. Manual disposal of the isolated test
database remained an external operator step and was not performed by application
startup, repository tests or migration code.

## Example controlled commands

Generate a reviewable idempotent migration script:

```powershell
$env:ConnectionStrings__OperationsDatabase = 'Server=<sql-server>;Database=<test-database>;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet ef migrations script --idempotent `
  --project .\src\PSMOperationsPlatform.Infrastructure `
  --startup-project .\src\PSMOperationsPlatform.Infrastructure `
  --context OperationsDbContext
```

Apply migrations from an approved deployment session:

```powershell
dotnet ef database update `
  --project .\src\PSMOperationsPlatform.Infrastructure `
  --startup-project .\src\PSMOperationsPlatform.Infrastructure `
  --context OperationsDbContext
```

These examples do not authorize automatic migration, database creation or
database deletion.
