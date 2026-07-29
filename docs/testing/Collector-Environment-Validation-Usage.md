---
title: Collector Environment Validation Usage
version: 1.2.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# Collector Environment Validation Usage

Run only on an approved collector host after the applicable execution gate.
The values below are placeholders, not real infrastructure.

```powershell
.\tools\readiness\Invoke-CollectorReadiness.ps1 `
  -Mode SmokeTest `
  -CollectorInstallPath 'C:\PSM\WindowsCollector' `
  -CollectorServiceName 'PSMWindowsCollector' `
  -TargetFqdn 'lab-target.example.local' `
  -TransportPolicy Auto `
  -WinRmHttpsPort 5986 `
  -WinRmHttpPort 5985 `
  -SqlServer 'sql-lab.example.local' `
  -SqlPort 1433 `
  -DatabaseName 'PSMOperationsPlatform_SmokeTest' `
  -ExpectedServiceAccount 'EXAMPLE\gmsaCollector$' `
  -OutputDirectory 'C:\PSM\ReadinessReports'
```

The output directory must already exist. The tool never creates it. Default
fixed files are `collector-readiness.json` and `collector-readiness.md`.

For local-only checks use `-Mode CollectorHost` and omit target/SQL parameters.
`-GenerateJson:$false` or `-GenerateMarkdown:$false` disables that format.
Authentication skip switches exist for controlled diagnostics, but in
`SmokeTest` they produce mandatory `SKIPPED` and overall `NOT_READY`.

Exit codes:

| Code | Meaning |
|---:|---|
| 0 | READY |
| 1 | WARNING |
| 2 | NOT_READY |

The tool is non-interactive and accepts no credentials. It does not start or
stop the collector service.

## Configuration interpretation

The readiness report inspects optional `appsettings.json` and
`appsettings.{Environment}.json` files independently. It distinguishes a
missing optional file, an unreadable file, invalid JSON, and valid JSON. A
valid file does not need to contain `ConnectionStrings:OperationsDatabase`
when a higher-precedence provider supplies the value.

For providers visible to the readiness tool, precedence is base JSON,
environment-specific JSON, then the machine
`PSM__ConnectionStrings__OperationsDatabase` environment variable. This
matches the applicable portion of the runtime provider order. Reports identify
the selected source, provider and key, but never the connection-string value.
The complete runtime order remains JSON, environment-specific JSON,
Development User Secrets in Development, `PSM__` environment variables, then
command-line arguments.

## Platform interpretation

Production support and the active validation mode are reported separately.
Windows Server 2022 or later remains the minimum supported production
collector host. `CollectorHost` validates deployment usability; it is not
production certification. Controlled `CollectorHost` and `SmokeTest`
validation may run on 64-bit Windows Server 2019 or later so behavioral
evidence can be collected without claiming production support.

On Windows Server 2019, the production-support check is `WARNING`.
Both `CollectorHost` deployment validation and `SmokeTest` platform validation
are `PASS`. Because the support warning remains visible, an otherwise
successful Windows Server 2019 validation has overall `WARNING` and exit code
`1`, not `NOT_READY`. This host remains controlled-lab-only and unsupported for
production.
