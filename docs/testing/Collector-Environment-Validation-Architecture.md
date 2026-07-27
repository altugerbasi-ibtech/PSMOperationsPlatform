---
title: Collector Environment Validation Architecture
version: 1.0.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# Collector Environment Validation Architecture

## Structure

```text
Invoke-CollectorReadiness.ps1
  -> common result, aggregation, redaction and report functions
  -> CollectorHost validations
  -> SmokeTest-only Network, WinRM and SQL validations
  -> normalized/sorted checks
  -> category status, overall status and exit code
  -> console + fixed JSON/Markdown paths
```

Validation scripts are dot-sourced implementation modules. Their functions are
testable directly, but only the primary entry point is a public process
contract. No second orchestrator exists.

## Modes

`CollectorHost` runs host, runtime, deployed files, configuration, service, and
identity checks. `SmokeTest` runs all CollectorHost checks plus target/SQL
network, WinRM authentication/policy, database schema, migration, and effective
permission metadata. `Mode` is mandatory; there is no ambiguous default.

## Result and dependency model

Each check has `CheckId`, category, name, status, severity, summary, evidence,
recommendation, blocking/mandatory flags, and duration. Allowed status and
severity values are enforced with `ValidateSet`.

Checks sort by category and ID. DNS failure causes dependent TCP checks to be
`SKIPPED`. SQL authentication failure skips schema checks. Explicitly skipping
mandatory SmokeTest authentication produces mandatory `SKIPPED`, therefore
`NOT_READY`.

## External seams

Each validator accepts an internal operations hashtable. Production defaults
call allowlisted read-only APIs; tests inject deterministic scriptblocks for
filesystem, service, AD, DNS, TCP, WSMan, dotnet, and SQL behavior. This is a
small functional seam rather than an object framework.

## SQL boundary

The tool constructs an Integrated Security connection internally and never
reports it. Executable SQL consists only of `SELECT`, `EXISTS`,
`OBJECT_ID`, `HAS_PERMS_BY_NAME`, and migration-history reads. Collector write
readiness is inferred from permission metadata; no write is attempted.

## Versioning

Schema version is `1.0` and framework version is the single code-owned constant
`1.0.0` in `Readiness.Common.ps1`. Breaking report changes require a schema
version change.
