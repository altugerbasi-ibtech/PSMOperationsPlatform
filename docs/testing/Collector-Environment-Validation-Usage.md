---
title: Collector Environment Validation Usage
version: 1.0.0
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
