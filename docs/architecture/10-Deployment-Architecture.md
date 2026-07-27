---
title: Deployment Architecture
version: 1.0.5
status: Approved
owner: Architecture
last_updated: 2026-07-27
reviewers:
  - Operations
  - Security
product: PSM Operations Platform
---
# Deployment Architecture

Minimum components: IIS-hosted web application, Windows Collector service, SQL Collector service and central SQL Server database. Collectors may run together or separately. Target network flow is outbound from collectors to managed systems; targets do not initiate connections.

## Host startup configuration

Runtime host application overrides use the WP-003 prefix:

```text
PSM__ConnectionStrings__OperationsDatabase
```

The standard environment provider maps this name to
`ConnectionStrings:OperationsDatabase`. The Windows Collector selects the
OperationsDatabase capability; other production hosts remain unchanged.

WP-004.2 selects the capability only in the Windows Collector, composes scoped
SQL Server persistence and supports standard Windows Service hosting under the
dedicated identity. Interactive console hosting remains available. The service
name is `PSM Operations Platform Windows Collector`. WP-004.3 loads eligible
targets and WP-004.4 probes them through in-process PowerShell/WSMan. No
automatic migration or connectivity-result persistence runs at startup.

Deployment must allow outbound WinRM HTTPS 5986 and, only where target policy
permits, HTTP 5985. HTTPS certificates must chain to a root trusted by the
collector host and match the target name. Deployment must not disable
certificate validation or automate TrustedHosts changes.

The collector host is Windows Server 2022 or later with .NET 10 and a
Windows Service running as the dedicated gMSA. It requires allowlisted paths to
OperationsDatabase, DNS, deployment-required AD/Kerberos services and target
WinRM endpoints, plus synchronized time. SQL and AD/DC ports are
deployment-defined.

Targets require no collector agent, software/file deployment, database or table.
No SMB, RDP or broad RPC path is implied. Detailed operator checks are in
[`../deployment/WP-004-Windows-Collector-Prerequisites.md`](../deployment/WP-004-Windows-Collector-Prerequisites.md).

The provider order from lowest to highest precedence is base JSON, environment
JSON, Development User Secrets, `PSM__` environment variables and command-line
arguments. User Secrets are not loaded outside Development. Runtime configuration
does not reload.

## Controlled database migrations

EF Core migrations are deployment artifacts. Web and collector processes do not
call `Database.Migrate()` and runtime identities do not require DDL permissions.
Run migration commands under the approved deployment identity using Windows
integrated authentication.

From the repository root, create a migration only when the accepted model changes:

```powershell
$env:ConnectionStrings__OperationsDatabase = 'Server=SQLHOST;Database=PSMOperationsPlatform;Integrated Security=true;Encrypt=true;TrustServerCertificate=false'
dotnet ef migrations add <MigrationName> `
  --project .\src\PSMOperationsPlatform.Infrastructure `
  --startup-project .\src\PSMOperationsPlatform.Infrastructure `
  --context OperationsDbContext `
  --output-dir Persistence\Migrations
```

`ConnectionStrings__OperationsDatabase` above is the existing WP-002
design-time factory input for controlled EF tooling; it is not a production host
configuration prefix. The transport values shown are deployment examples, not
WP-003 validation requirements. WP-003 does not mandate `Encrypt=True`, does not
mandate `TrustServerCertificate=False` and does not prohibit
`TrustServerCertificate=True`.

Review the generated migration and produce an idempotent deployment script:

```powershell
dotnet ef migrations script --idempotent `
  --project .\src\PSMOperationsPlatform.Infrastructure `
  --startup-project .\src\PSMOperationsPlatform.Infrastructure `
  --context OperationsDbContext `
  --output .\artifacts\database\PSMOperationsPlatform.sql
```

Apply directly only from a controlled deployment session:

```powershell
dotnet ef database update `
  --project .\src\PSMOperationsPlatform.Infrastructure `
  --startup-project .\src\PSMOperationsPlatform.Infrastructure `
  --context OperationsDbContext
```

Migration failure stops the deployment. Do not print the connection string, add
SQL credentials, or grant runtime permissions from an application migration.

Migration command output and deployment audit logs are the responsibility of the
approved deployment pipeline or operator tooling. They must record migration
start, completion and failure without recording connection strings, SQL text or
credentials. Runtime `OperationsDbContext` logging covers persistence operations;
it does not replace deployment-side migration logging.

## Revision History

| Version | Date | Description |
|---|---|---|
| 1.0.5 | 2026-07-27 | Synchronized deployment wording with completed WP-004 |
| 1.0.4 | 2026-07-27 | Recorded implemented WP-004.2 host and service composition |
| 1.0.3 | 2026-07-27 | Added proposed WP-004 host and allowlist prerequisites |
| 1.0.0 | 2026-07-26 | Initial deployment architecture |
| 1.0.1 | 2026-07-27 | Documented WP-003 host configuration and design-time migration boundary |
| 1.0.2 | 2026-07-27 | Documented planned WP-004 service, database and WinRM boundaries |
