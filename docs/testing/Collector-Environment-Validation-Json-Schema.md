---
title: Collector Environment Validation JSON Schema
version: 1.0.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# Collector Environment Validation JSON Schema

Schema version `1.0` uses PascalCase, matching repository .NET conventions.
Property order is code-owned and checks are sorted by category then ID.

```json
{
  "SchemaVersion": "1.0",
  "FrameworkName": "PSM Collector Environment Validation",
  "FrameworkVersion": "1.0.0",
  "GeneratedAt": "2026-07-27T12:00:00.000+03:00",
  "GeneratedOnMachine": "COLLECTOR01",
  "ExecutingIdentity": "EXAMPLE\\gmsaCollector$",
  "PowerShellVersion": "5.1.0",
  "OperatingSystem": "Microsoft Windows ...",
  "Mode": "SmokeTest",
  "CollectorVersion": null,
  "CollectorServiceName": "PSMWindowsCollector",
  "CollectorInstallPath": "C:\\PSM\\WindowsCollector",
  "TargetFqdn": "lab-target.example.local",
  "TransportPolicy": "Auto",
  "SqlServer": "sql-lab.example.local",
  "DatabaseName": "PSMOperationsPlatform_SmokeTest",
  "Categories": [{ "Name": "Runtime", "Status": "READY" }],
  "OverallStatus": "READY",
  "ExitCode": 0,
  "Checks": [{
    "CheckId": "DOTNET.RUNTIME.REQUIRED",
    "Category": "Runtime",
    "Name": "Required .NET runtime",
    "Status": "PASS",
    "Severity": "INFO",
    "Summary": ".NET 10 runtime is present.",
    "Evidence": "Microsoft.NETCore.App 10.x",
    "Recommendation": null,
    "IsBlocking": true,
    "IsMandatory": true,
    "DurationMilliseconds": 20
  }]
}
```

Allowed check statuses: `PASS`, `WARNING`, `FAIL`, `SKIPPED`,
`NOT_APPLICABLE`. Category/overall statuses use `READY`, `WARNING`,
`NOT_READY`, with `NOT_APPLICABLE` only for a category with no checks.

Raw exceptions, stack traces, credentials, tokens, connection strings, and
environment dumps are forbidden.
