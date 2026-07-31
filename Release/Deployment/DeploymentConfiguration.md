---
title: Deployment Configuration
version: 1.0.0
status: Implemented - Review Pending
owner: Release Engineering
last_updated: 2026-07-31
product: PSM Operations Platform
---
# Deployment Configuration

## Contract

`DeploymentConfiguration.json` is the single environment-input contract for
release engineering, deployment, validation, and future installation tooling.
Copy `DeploymentConfiguration.template.json` outside the repository, populate
it through approved change control, validate it, and pass that same immutable
copy to every activity. Do not commit a populated environment file.

The template intentionally contains blank required strings and therefore does
not pass validation until an operator supplies approved values. The sample is
valid but uses reserved `.invalid` hosts, an example domain, and synthetic
metadata; it is not production configuration.

Validate a populated copy with:

```powershell
.\Release\Deployment\Test-DeploymentConfiguration.ps1 `
  -Path '<CONTROLLED-PATH>\DeploymentConfiguration.json'
```

Exit code `0` and `PASS` mean the JSON and cross-field checks passed. Exit code
`1` reports invalid JSON, missing/empty values, schema violations, invalid SQL
or WinRM ports, duplicate server roles, or missing/duplicate runtime accounts.
Validation reads the file only and never contacts an environment.

## Properties

| Property | Allowed value and example |
|---|---|
| `Deployment.EnvironmentName` | 1-64 safe label characters; `LAB` |
| `Deployment.ProductVersion` | Semantic version; `1.0.0` |
| `Deployment.ReleaseVersion` | Semantic bundle version; `1.0.0` |
| `Deployment.GitCommit` | Full 40-character hexadecimal manifest commit |
| `SqlServer.Server` | DNS/server name without port or connection syntax; `sql.example.invalid` |
| `SqlServer.Instance` | Named instance or `MSSQLSERVER` |
| `SqlServer.Port` | Integer 1-65535; normally `1433` |
| `SqlServer.Database` | Safe database identifier; `PSMOperationsPlatform` |
| `SqlServer.DataPath` | SQL service-accessible data directory |
| `SqlServer.LogPath` | SQL service-accessible transaction-log directory |
| `SqlServer.CompatibilityLevel` | Exactly `160` |
| `SqlServer.RecoveryModel` | `SIMPLE`, `FULL`, or `BULK_LOGGED` |
| `SqlServer.Collation` | Exact DBA-approved SQL collation |
| `Collector.Server` | Dedicated Windows Collector installation server |
| `Collector.ServiceAccount` | Domain-qualified gMSA ending `$`; `EXAMPLE\gmsa-collector$` |
| `Collector.LogPath` | Collector log directory |
| `Portal.Server` | Dedicated Portal installation server |
| `Portal.ServiceAccount` | Domain-qualified Portal gMSA ending `$` |
| `SqlCollector.Server` | Dedicated SQL Collector installation server |
| `SqlCollector.ServiceAccount` | Domain-qualified SQL Collector gMSA ending `$` |
| `Security.WindowsAuthentication` | Boolean; must be `true` |
| `Security.KerberosOnly` | Boolean controlling whether NTLM fallback is rejected |
| `Security.WinRMPort` | Integer 1-65535; normally `5985` or `5986` |
| `Security.IncludePortInSPN` | Boolean matching the approved HTTP SPN form |
| `Security.UseTLS` | Boolean matching the approved transport design |
| `Validation.RunSchemaValidation` | Boolean gate for WP-009.3 |
| `Validation.RunPermissionValidation` | Boolean gate for WP-009.4 |
| `Validation.RunReleaseAcceptanceTest` | Boolean gate for WP-009.7 |

Every section and property is required. Unknown properties are prohibited by
the JSON Schema. Server roles and runtime service accounts must be distinct to
preserve security boundaries. The file stores references and policy choices
only: passwords, connection strings, certificates, private keys, and other
secrets are prohibited.

## Consumers

- WP-009.2 obtains database target and deployment parameters.
- WP-009.3 obtains schema-validation SQLCMD values.
- WP-009.4 obtains runtime database-principal inputs derived from the three
  service accounts.
- WP-009.5 obtains SQL, WinRM, SPN, gMSA, network, and security inputs.
- WP-009.7 obtains release identity and validation gates.
- WP-007.Z.2 and later database deployment evidence use the same approved file.
- Future Windows Collector, SQL Collector, and Portal installation tooling
  must consume this contract rather than introduce parallel environment files.

Current WP-009 scripts retain their existing command-line interfaces. Wiring
those scripts to load this file is future implementation work and must retain
read-only verification and runtime security boundaries.
