---
title: WP-009.6 Release Bundle Generation Guide
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
# WP-009.6 Release Bundle Generation Guide

## Generate

From a clean, committed repository revision:

```powershell
.\Build-Release.ps1 -Version 1.0.0
```

The command validates required sources, generates the idempotent database
package, publishes the Windows Collector and Portal in Release configuration,
collects verification scripts and documentation, verifies required outputs,
and writes integrity metadata.

The database-package stage intentionally rejects a dirty source tree so the
Git commit and generated artifacts remain reproducible.

## Bundle Layout

```text
Release/
  Database/
  Verification/
  Collector/
  Portal/
  Documentation/
  Manifest.json
  Checksums.sha256
  Version.txt
  ReleaseGuide.md
```

`Documentation/DeploymentSummary.md` is generated with the product version,
Git commit, source-derived UTC build date, included components, and the
deployment/verification handoff.

## Manifest and Checksums

Root `Manifest.json` records the product/version, Git commit, source-derived
UTC build date, and every payload artifact's relative path, size, and uppercase
SHA256.

Root `Checksums.sha256` covers every file in the bundle except itself,
including root `Manifest.json`. The checksum catalog cannot include its own
hash. Database-level `Manifest.json` and `Checksums.sha256` remain the
independent WP-009.1 SQL package contract.

## Verify Checksums

Run from the repository root:

```powershell
Push-Location .\Release
try {
    Get-Content .\Checksums.sha256 | ForEach-Object {
        if ($_ -notmatch '^([0-9A-F]{64}) \*(.+)$') {
            throw "Invalid checksum entry: $_"
        }
        $actual=(Get-FileHash -LiteralPath $Matches[2] -Algorithm SHA256).Hash
        if ($actual -cne $Matches[1]) {
            throw "Checksum mismatch: $($Matches[2])"
        }
    }
} finally {
    Pop-Location
}
```

## Failure Behavior

Generation fails on an invalid version, missing source, dirty/unresolved
revision, database-package failure, pending model changes, Collector/Portal
publish failure, missing generated artifact, or metadata/checksum failure.

Do not distribute a partial `Release` directory after failure. Resolve the
source problem, return to a clean immutable revision, and rebuild.

## Safety Boundary

The command builds and copies repository artifacts only. It contacts no target
server, executes no SQL, applies no migration, changes no permission, and runs
no verification against infrastructure. It changes no runtime behavior or
feature implementation.

Deployment, live verification, evidence acceptance, signing policy, and
production certification remain separately controlled.

The final controlled-deployment assessment is performed by
`tools/validation/production/Invoke-ProductionReadinessValidation.ps1`. It
validates bundle and evidence alignment but does not alter or certify a release
without all mandatory live evidence and human approvals.
