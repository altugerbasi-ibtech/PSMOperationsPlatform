---
title: WP-009.7 Release Acceptance Test Guide
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
# WP-009.7 Release Acceptance Test Guide

WP-009.7 consumes the approved
`Release/Deployment/DeploymentConfiguration.json` for release identity and
validation gates. Missing mandatory evidence still forces `FAIL`; disabling a
configuration gate does not manufacture a passing result.

## Purpose

The RAT reporting layer combines supplied read-only validation results and
produces console, JSON, HTML, and Markdown reports. It does not execute
environment checks or remediation.

## Input

Supply a JSON file:

```json
{
  "ProductVersion": "1.0.0",
  "GitCommit": "0123456789abcdef0123456789abcdef01234567",
  "ExecutionTime": "00:01:42",
  "ReadOnlyValidation": true,
  "Checks": [
    {
      "Name": "Database Schema",
      "Result": "PASS",
      "Diagnostics": "SchemaValidation.sql returned PASS."
    }
  ]
}
```

`ReadOnlyValidation` is mandatory. A false value adds a failing check and
forces `NOT_READY_FOR_PRODUCTION`.

## Execute

```powershell
.\Release\Acceptance\Invoke-ReleaseAcceptanceTest.ps1 `
  -InputPath '<APPROVED-RAT-INPUT>.json' `
  -OutputDirectory '<APPROVED-EVIDENCE-DIRECTORY>'
```

The command writes `RATReport.json`, `RATReport.html`, and `RATReport.md`, and
prints the same decision to the console.

## Production Readiness Mapping

| Overall | Status | Message | Exit |
|---|---|---|---|
| PASS | `READY_FOR_PRODUCTION` | `PSM Release Status: READY FOR PRODUCTION` | 0 |
| WARNING | `READY_WITH_WARNINGS` | `PSM Release Status: READY WITH WARNINGS` | 1 |
| FAIL | `NOT_READY_FOR_PRODUCTION` | `PSM Release Status: NOT READY FOR PRODUCTION` | 2 |

Overall result is calculated from checks. Any FAIL wins over WARNING and PASS.
Any WARNING wins over PASS. A FAIL cannot produce a ready decision.

## Output Interpretation

JSON exposes the machine-readable fields:

```json
{
  "OverallResult": "PASS",
  "ProductionReadinessStatus": "READY_FOR_PRODUCTION",
  "ProductionReadinessMessage": "PSM Release Status: READY FOR PRODUCTION"
}
```

HTML displays the explicit message in the page header and final summary; color
is supplementary only. Markdown begins with the production decision. Console
ends with:

```text
====================================================
PSM Release Status: READY FOR PRODUCTION
====================================================
```

The corresponding warning or failure text is used for those outcomes.

## Safety and Approval Boundary

RAT report generation writes evidence files only. It modifies no runtime,
service, directory identity, SPN, network, database, permission, or
infrastructure state. A READY decision is release evidence for human review;
it does not deploy the product or replace required approval.

WP-007.Z.10 consumes a RAT report as one mandatory production gate. Repository
RAT tests are not a live RAT, and even a passing live RAT cannot override a
missing mandatory capability or blocking security defect.
