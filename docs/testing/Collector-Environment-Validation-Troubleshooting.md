---
title: Collector Environment Validation Troubleshooting
version: 1.2.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# Collector Environment Validation Troubleshooting

| Result | Meaning | Manual next step |
|---|---|---|
| Output directory failure | Directory is absent | Select/create it through a separately approved local process, then rerun |
| Runtime missing | Framework-dependent deployment lacks .NET 10 | Use approved runtime installation process |
| Version warning | File version metadata unavailable | Verify artifact provenance/hash externally |
| Configuration file not found | Optional JSON provider is absent | No action when a higher-precedence approved provider supplies the required value |
| Configuration file unreadable | File exists but cannot be opened | Verify deployed-file read access |
| Configuration JSON FAIL | An opened file is syntactically invalid JSON | Correct the JSON through the approved deployment process |
| OperationsDatabase FAIL | No inspected provider supplies a non-empty value | Configure the approved JSON or `PSM__` source |
| Windows Server 2019 support WARNING | Deployment validation can succeed, but the platform is outside production support | Use only for controlled `CollectorHost`/`SmokeTest` validation; use Windows Server 2022+ for production |
| Platform-policy FAIL | Host does not meet the active mode's controlled-validation minimum | Use 64-bit Windows Server 2019 or later for `CollectorHost` or `SmokeTest` validation |
| Service stopped WARNING | Registration exists but service is stopped | Start only under the separately approved S1 runbook |
| gMSA WARNING | AD module unavailable | Have an authorized operator validate the account |
| DNS FAIL | FQDN did not resolve | Correct DNS; do not use IP fallback |
| TCP FAIL | Endpoint unavailable | Review existing routing/firewall policy; tool does not change it |
| WinRM FAIL | Safe category reported | Review certificate/auth/policy without bypass or TrustedHosts |
| SQL authentication FAIL | Integrated open failed | Verify TLS, database existence and Windows permissions |
| Migration/schema FAIL | Expected migration/table missing | Review controlled migration plan; tool never applies it |
| Permission FAIL | Effective permission metadata insufficient | Database owner reviews least privilege |

Mandatory `SKIPPED` is intentionally `NOT_READY`. An internal-error check means
the environmental conclusion is unsafe; review the local framework and rerun.
Reports intentionally omit raw exception details.

## EF Core migration tooling

If `dotnet ef` reports that the Windows Collector startup project does not
reference `Microsoft.EntityFrameworkCore.Design`, restore and build the current
repository before retrying the commands in the deployment runbook. The package
is private tooling and uses the centrally managed EF Core version.

Run migration inspection and idempotent script generation on an approved
development or build machine containing the full repository. Supply
`ConnectionStrings__OperationsDatabase` to the tooling process using Windows
Integrated Authentication without exposing it. Use `--no-connect` for
`migrations list`. Do not use `dotnet ef database update`.

If design-time creation reports missing configuration, provide the approved
process environment value. If it rejects authentication, remove `User ID`,
`Password`, `UID`, and `PWD` and use Windows Integrated Authentication. After
an authorized DBA applies a separately reviewed script, rerun
`Test-PSMOperationsDatabaseSchema.ps1` and CollectorHost readiness before any
service start.
