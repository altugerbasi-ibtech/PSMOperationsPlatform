---
title: Windows Collector Operator Guide
version: 1.3.0
status: Draft
owner: Operations
last_updated: 2026-07-28
product: PSM Operations Platform
---
# Windows Collector Operator Guide

> **Deployment authority:** WP-006.8 supersedes the older WP-006.2A publish,
> install, update, and remote-orchestration commands retained later in this
> historical guide. Use `scripts\deployment\New-CollectorDeploymentPackage.ps1`
> and run `scripts\deployment\Install-CollectorPackage.ps1` locally as described
> in
> [`../deployment/WP-006.8-Safe-Collector-Deployment.md`](../deployment/WP-006.8-Safe-Collector-Deployment.md).

## Purpose

This guide is the operational handbook for the PSM Windows Collector. It
explains how an approved operator publishes, installs, configures, validates,
starts, observes, upgrades, troubleshoots, and removes the Collector safely.

The Windows Collector runs as a Windows Service. It loads enabled and due
Windows targets from OperationsDatabase, opens bounded read-only WinRM
sessions, collects the seven implemented Windows inventory boundaries, and
persists current state to SQL Server. The implemented inventory boundaries are
Computer, OperatingSystem, Memory, Processor, Disk, Volume, and
NetworkAdapter/IPv4.

This document is for operations. It does not replace architecture decisions,
work-package acceptance criteria, detailed execution runbooks, SQL query
references, or change approval. Follow the linked authoritative document when
a procedure below delegates to it.

## Intended Audience

This guide is intended for:

- Windows administrators who manage Collector hosts and Windows Services.
- Platform and DevOps engineers who publish and deploy approved artifacts.
- Operations engineers who run readiness, validation, and controlled
  execution procedures.
- Support engineers who investigate deployment, configuration, identity,
  WinRM, SQL, and evidence failures.

Operators are expected to understand Windows Services, PowerShell, gMSAs,
Windows Integrated Authentication, environment variables, basic TLS and WinRM
concepts, SQL Server connection metadata, change control, and protected
evidence handling. Database or Active Directory changes require the appropriate
authorized owner; this guide does not grant those permissions.

## Related Documents

Use these documents as the detailed sources of truth:

| Document | Use |
|---|---|
| [README](../../README.md) | Current repository and release status |
| [RELEASE](../../RELEASE.md) | Current release summary and known limitations |
| [Product Roadmap](../project/Roadmap.md) | Delivery sequence and certification status |
| [WP-004 Windows Collector Foundation](../tasks/WP-004-Windows-Collector-Foundation-and-Target-Connectivity.md) | Implemented service, connectivity, fallback, and safety behavior |
| [WP-005 Windows Inventory Framework](../tasks/WP-005-Windows-Inventory-Framework.md) | Implemented inventory boundaries and operating behavior |
| [WP-006 Production Validation](../tasks/WP-006-Windows-Collector-Production-Validation.md) | Validation scope, evidence requirements, decisions, and certification rules |
| [Windows Collector Prerequisites](../deployment/WP-004-Windows-Collector-Prerequisites.md) | Host, target, identity, network, and runtime prerequisites |
| [WP-006.2A Deployment Runbook](../deployment/WP-006.2A-Windows-Collector-Deployment-Runbook.md) | Authoritative deployment, upgrade, rollback, and uninstall procedure |
| [WP-006.8 Safe Collector Deployment](../deployment/WP-006.8-Safe-Collector-Deployment.md) | Package-first build and authoritative local update automation for an existing service |
| [Collector Readiness Usage](../testing/Collector-Environment-Validation-Usage.md) | Readiness command parameters and report behavior |
| [Collector Readiness Troubleshooting](../testing/Collector-Environment-Validation-Troubleshooting.md) | Readiness result interpretation |
| [Collector Readiness Checklist](../testing/Collector-Readiness-Checklist.md) | Pre-execution and completion checklist |
| [WP-006.2 Controlled Execution Runbook](../testing/WP-006.2-Controlled-Single-Target-Execution-Runbook.md) | Authoritative single-target execution procedure |
| [WP-005 Database Verification Queries](../testing/WP-005-Database-Verification-Queries.md) | Detailed read-only SQL verification |

Do not copy detailed runbook steps into a local procedure and let them drift.
Record the repository commit used for every deployment or controlled execution.

## High-Level Lifecycle

```text
Development
    |
    v
Publish approved artifact
    |
    v
Install without starting
    |
    v
Configure Windows Integrated SQL access
    |
    v
Validate Operations Database schema
    |
    v
CollectorHost readiness
    |
    v
Deployment validation
    |
    v
Approved service start
    |
    v
Controlled single-target execution and evidence
    |
    v
Routine operation
    |
    +-----------------------+
    |                       |
    v                       v
Upgrade -> readiness -> validation    Conservative uninstall
```

Publishing and tooling availability are not deployment evidence. Deployment
must be completed and reviewed before WP-006.2 controlled execution begins.

## Repository Tool Overview

“Read only” means the tool does not change the assessed service, target, AD, or
SQL state. Some read-only tools still write reports to an operator-selected
directory.

### Deployment tools

| Tool | Purpose | Typical Use | Read Only | Changes System | Output |
|---|---|---|---:|---:|---|
| `scripts/deployment/New-CollectorDeploymentPackage.ps1` | Builds and validates a transport-neutral Collector package | Approved build machine | No | Package output only | Deployment package, manifest, optional ZIP |
| `scripts/deployment/Install-CollectorPackage.ps1` | Validates and locally updates an existing Collector service with rollback | Elevated local Collector-server session | No | Backup, managed files, existing service state | Backup manifest, console result, JSON log |
| `tools/deployment/Invoke-PSMWindowsCollectorDeployment.ps1` | Orchestrates publish, install, upgrade, validate, uninstall, or full deployment | Standard deployment entry point | Mode-dependent | Mode-dependent | Deployment JSON and Markdown |
| `tools/deployment/Publish-PSMWindowsCollector.ps1` | Publishes the approved .NET project and hashes artifacts | Build immutable deployment package | No | Publish directory | `PSMWindowsCollector.publish-manifest.json` and result |
| `tools/deployment/Install-PSMWindowsCollector.ps1` | Installs files, configuration, ACL, and service | Direct first installation | No | Files, environment, ACL, service | Deployment result |
| `tools/deployment/Update-PSMWindowsCollector.ps1` | Legacy WP-006.2A in-place upgrade helper; not the WP-006.8 authoritative update path | Existing WP-006.2A procedure only | No | Backup, files, service state/configuration | Deployment result |
| `tools/deployment/Uninstall-PSMWindowsCollector.ps1` | Removes the service conservatively | Approved removal | No | Service; optional files/configuration/backups | Deployment result |
| `tools/deployment/Set-PSMWindowsCollectorConfiguration.ps1` | Sets the redacted OperationsDatabase environment configuration | Secure configuration change | No | Selected environment-variable scope | Redacted configuration result |
| `tools/deployment/Test-PSMWindowsCollectorDeployment.ps1` | Validates files, hash, service, configuration, ACL, runtime, manifest, readiness, and backup | Post-install or post-upgrade validation | Yes | Report files only | Deployment validation JSON and Markdown |
| `tools/deployment/Test-PSMOperationsDatabaseSchema.ps1` | Validates migrations, schemas, tables, constraints, indexes, and effective runtime permissions | Required database gate before readiness/start | Yes | Report files only; contacts approved SQL Server | Database validation JSON and Markdown |
| `tools/deployment/Initialize-PSMSmokeTestDatabase.ps1` | Creates/verifies a non-production database principal and reader/writer memberships | Separately approved smoke-test database preparation | No | SQL database/security principals | Console manifest and optional Markdown |
| `tools/deployment/PSMWindowsCollectorDeployment.Common.ps1` | Shared implementation used by deployment entry points | Internal support; do not invoke directly | N/A | N/A | None directly |
| `tools/deployment/PSMOperationsDatabaseValidation.Common.ps1` | Shared read-only database validation implementation | Internal support; do not invoke directly | N/A | N/A | None directly |

`Initialize-PSMSmokeTestDatabase.ps1` is not part of normal installation. It
contacts SQL Server and can create a database, login, user, and role
memberships. Use it only under its separate approval and documentation.

### Readiness tools

| Tool | Purpose | Typical Use | Read Only | Changes System | Output |
|---|---|---|---:|---:|---|
| `tools/readiness/Invoke-CollectorReadiness.ps1` | Runs CollectorHost or SmokeTest readiness | Deployment or controlled-execution gate | Yes | Report files only | `collector-readiness.json`, `collector-readiness.md`, exit code |
| `tools/readiness/CollectorHostValidation.ps1` | Checks host platform and local conditions | Called by readiness entry point | Yes | No | Readiness checks |
| `tools/readiness/DotNetValidation.ps1` | Checks framework-dependent .NET 10 runtime | Called by readiness entry point | Yes | No | Runtime checks |
| `tools/readiness/CollectorFilesValidation.ps1` | Checks required files and version evidence | Called by readiness entry point | Yes | No | File checks |
| `tools/readiness/ConfigurationValidation.ps1` | Resolves supported configuration sources and validates redaction/authentication | Called by readiness entry point | Yes | No | Configuration checks |
| `tools/readiness/ServiceValidation.ps1` | Checks registration, path, account, state, and process | Called by readiness entry point | Yes | No | Service checks |
| `tools/readiness/GmsaValidation.ps1` | Checks applicable gMSA state | Called by readiness entry point | Yes | No | Identity checks |
| `tools/readiness/NetworkValidation.ps1` | Checks DNS and required TCP endpoints in SmokeTest mode | Called by readiness entry point | Yes | No | Network checks |
| `tools/readiness/WinRmValidation.ps1` | Checks policy-compliant WinRM authentication and fallback | Called by readiness entry point | Yes | No | WinRM checks |
| `tools/readiness/SqlValidation.ps1` | Checks Integrated SQL authentication, migration, tables, and permission metadata | Called by readiness entry point | Yes | No | SQL checks |
| `tools/readiness/Readiness.Common.ps1` | Shared result, report, aggregation, and redaction functions | Internal support; do not invoke directly | N/A | N/A | None directly |

Readiness never remediates a failure, creates its output directory, starts or
stops the service, applies a migration, modifies WinRM, or accepts credentials.

### WP-006.2 validation and evidence tools

| Tool | Purpose | Typical Use | Read Only | Changes System | Output |
|---|---|---|---:|---:|---|
| `tools/validation/Invoke-WP0062Preflight.ps1` | Validates local execution inputs, artifact, service, readiness, and evidence location | Before controlled execution | Yes | Evidence files and temporary local write probe | `WP-006.2-Preflight.json` and `.md` |
| `tools/validation/Get-WP0062SqlEvidence.ps1` | Captures target-scoped baseline, post-run, or verification evidence | Approved WP-006.2 SQL checkpoints | Yes | Evidence files; contacts approved SQL Server | `WP-006.2-Sql-<Phase>.json` and `.csv` |
| `tools/validation/Get-WP0062HostEvidence.ps1` | Captures service/process/logging metadata | PreStart, Running, and PostStop checkpoints | Yes | Evidence file only | `WP-006.2-Host-<Snapshot>.json` |
| `tools/validation/Test-WP0062Evidence.ps1` | Validates a completed execution bundle offline | Final evidence review | Yes | Validation report files only | `WP-006.2-Evidence-Validation.json` and `.md` |

## First-Time Installation

The [WP-006.2A Deployment Runbook](../deployment/WP-006.2A-Windows-Collector-Deployment-Runbook.md)
is authoritative. Obtain the approved host, change window, repository commit,
artifact, paths, service identity, SQL metadata, evidence location, operator,
approver, and rollback owner before proceeding.

### 1. Publish

Run the publish operation with `-WhatIf` first, then run it without `-WhatIf`
only after approval. The defaults are `Release`, `win-x64`, `net10.0`, and
framework-dependent output.

```powershell
.\tools\deployment\Publish-PSMWindowsCollector.ps1 `
  -RepositoryRoot '<REPOSITORY-ROOT>' `
  -ProjectPath '<WINDOWS-COLLECTOR-CSPROJ>' `
  -PublishOutputPath '<APPROVED-PUBLISH-PATH>' `
  -ExpectedRepositoryCommit '<APPROVED-40-CHAR-COMMIT>' `
  -WhatIf
```

Review `PSMWindowsCollector.publish-manifest.json`, the repository commit,
SDK version, executable SHA-256, primary DLL SHA-256, and immutable package
path. Do not deploy an unapproved or mutable package.

### 2. Review deployment with WhatIf

Use the orchestrator. Replace every placeholder with approved values; do not
put a connection string on a recorded command line.

```powershell
.\tools\deployment\Invoke-PSMWindowsCollectorDeployment.ps1 `
  -Mode Install `
  -CollectorHost '<COLLECTOR-HOST>' `
  -CollectorInstallPath '<INSTALL-PATH>' `
  -CollectorServiceName '<SCM-SERVICE-NAME>' `
  -ServiceAccount '<DOMAIN\GMSA$>' `
  -SqlServer '<SQL-SERVER>' `
  -DatabaseName '<DATABASE>' `
  -PackagePath '<APPROVED-PUBLISH-PATH>' `
  -ExpectedArtifactHash '<APPROVED-SHA256>' `
  -ExpectedRepositoryCommit '<APPROVED-40-CHAR-COMMIT>' `
  -ReadinessScriptPath '<REPOSITORY-ROOT>\tools\readiness\Invoke-CollectorReadiness.ps1' `
  -ReadinessOutputDirectory '<EXISTING-READINESS-DIRECTORY>' `
  -BackupRoot '<APPROVED-BACKUP-ROOT>' `
  -DeploymentManifestPath '<EVIDENCE-PATH>\deployment.json' `
  -Operator '<OPERATOR>' `
  -Approver '<APPROVER>' `
  -RollbackOwner '<ROLLBACK-OWNER>' `
  -ChangeReference '<CHANGE-REFERENCE>' `
  -RunReadiness `
  -StartService:$false `
  -WhatIf
```

Confirm all paths, the service account, artifact identity, configuration scope,
readiness output, report targets, and planned changes. A WhatIf result is not
proof of an installed system.

### 3. Install

After explicit approval, repeat the reviewed command without `-WhatIf` and
retain `-StartService:$false`. The deployment copies the package, configures
the approved environment value, grants inherited `ReadAndExecute` to the gMSA,
and creates a demand-start service. It does not start the service by default.

If the OperationsDatabase value is not already present, supply it as a
`SecureString` through the approved interactive or secret-delivery process.
Never store it in a script, transcript, report, manifest, shell history, or
ticket.

### 4. Validate the Operations Database

Run `Test-PSMOperationsDatabaseSchema.ps1` with an approved `SecureString`,
SQL/database metadata, repository root, and evidence path. Continue only for
`READY`, or an explicitly approved non-blocking permission-introspection
`WARNING`. `NOT_READY` cannot be overridden by `Force`.

If validation fails, keep the service stopped. An authorized DBA/deployment
identity must review and apply the approved migration script separately. Rerun
database validation and readiness before any start.

### 5. Run CollectorHost readiness

The deployment can invoke readiness with `-RunReadiness`. It can also be run
directly:

```powershell
.\tools\readiness\Invoke-CollectorReadiness.ps1 `
  -Mode CollectorHost `
  -CollectorInstallPath '<INSTALL-PATH>' `
  -CollectorServiceName '<SCM-SERVICE-NAME>' `
  -ExpectedServiceAccount '<DOMAIN\GMSA$>' `
  -OutputDirectory '<EXISTING-READINESS-DIRECTORY>'
```

Continue only for `READY`, or for an explicitly approved `WARNING` that the
applicable runbook permits. `NOT_READY` is a stop condition.

### 6. Validate deployment

```powershell
.\tools\deployment\Test-PSMWindowsCollectorDeployment.ps1 `
  -CollectorInstallPath '<INSTALL-PATH>' `
  -CollectorServiceName '<SCM-SERVICE-NAME>' `
  -ServiceAccount '<DOMAIN\GMSA$>' `
  -SqlServer '<SQL-SERVER>' `
  -DatabaseName '<DATABASE>' `
  -ExpectedArtifactHash '<APPROVED-SHA256>' `
  -ExpectedRepositoryCommit '<APPROVED-40-CHAR-COMMIT>' `
  -DeploymentManifestPath '<EVIDENCE-PATH>\deployment.json' `
  -ReadinessOutputDirectory '<EXISTING-READINESS-DIRECTORY>' `
  -ReportPath '<EVIDENCE-PATH>\deployment-validation.json'
```

Review files, hashes, service metadata, configuration presence and
authentication, ACL, .NET runtime, manifest consistency, and readiness.

### 7. Start the service

Start only during the approved execution checkpoint. Confirm the exact SCM
service name, executable path, gMSA, demand/manual start mode, change approval,
and rollback owner immediately before the action. Do not use `-StartService`
outside an approved window.

### 8. Perform controlled execution

Do not treat a successful service start as production certification. Follow
the [WP-006.2 Controlled Execution Runbook](../testing/WP-006.2-Controlled-Single-Target-Execution-Runbook.md)
against exactly one approved non-production target and retain the complete
evidence package.

## Upgrade Procedure

### Preparation

1. Confirm an approved package, commit, executable hash, change window, backup
   root, evidence directory, service name, service account, and rollback owner.
2. Run deployment validation and resolve every mismatch.
3. Confirm the existing service executable is under the approved install path.
4. Review the upgrade using orchestrator `-Mode Upgrade -WhatIf`.
5. Confirm the timestamped backup path does not already exist.

### Backup and upgrade

```powershell
.\tools\deployment\Invoke-PSMWindowsCollectorDeployment.ps1 `
  -Mode Upgrade `
  -CollectorHost '<COLLECTOR-HOST>' `
  -CollectorInstallPath '<INSTALL-PATH>' `
  -CollectorServiceName '<SCM-SERVICE-NAME>' `
  -ServiceAccount '<DOMAIN\GMSA$>' `
  -SqlServer '<SQL-SERVER>' `
  -DatabaseName '<DATABASE>' `
  -PackagePath '<APPROVED-PUBLISH-PATH>' `
  -ExpectedArtifactHash '<APPROVED-SHA256>' `
  -ExpectedRepositoryCommit '<APPROVED-40-CHAR-COMMIT>' `
  -BackupRoot '<APPROVED-BACKUP-ROOT>' `
  -DeploymentManifestPath '<EVIDENCE-PATH>\upgrade.json' `
  -Operator '<OPERATOR>' -Approver '<APPROVER>' `
  -RollbackOwner '<ROLLBACK-OWNER>' `
  -ChangeReference '<CHANGE-REFERENCE>' `
  -WhatIf
```

After approval, remove `-WhatIf`. If the service is running, the upgrade stops
it with a bounded wait, creates a timestamped backup, copies the package,
reapplies the ACL and service configuration, and preserves the backup.

`Update-PSMWindowsCollector.ps1` defaults
`RestorePreviousRunningState` to true. The orchestrator uses explicit switches;
request the desired final running state in the reviewed command. Never assume a
service restart.

### Validation and rollback

1. Run Operations Database schema validation.
2. Run CollectorHost readiness.
3. Run deployment validation with expected hash, commit, manifest, and
   `-RequireUpgradeBackup` when applicable.
4. Verify service state, account, executable path, file hashes, configuration
   presence, ACL, and report redaction.
5. Review the rollback status in the deployment report.

An upgrade failure attempts automatic file rollback only after a valid backup
exists. Exit code `4` or `ROLLBACK_FAILED` requires an immediate stop. Preserve
the backup and all reports, do not retry changes, and execute only the rollback
owner’s approved recovery plan.

## Uninstall Procedure

The default uninstall is conservative: it gracefully stops a running service
and removes the service definition. It preserves installed files, backups,
environment configuration, logs/evidence, database objects, and the gMSA.

1. Capture final service, deployment, readiness, artifact, and evidence state.
2. Review `-Mode Uninstall -WhatIf`.
3. Confirm service-control and removal approval.
4. Run without `-WhatIf`. Without `-Force`, confirmation is required.
5. Verify the service definition is absent and preserved items remain.

```powershell
.\tools\deployment\Invoke-PSMWindowsCollectorDeployment.ps1 `
  -Mode Uninstall `
  -CollectorHost '<COLLECTOR-HOST>' `
  -CollectorInstallPath '<INSTALL-PATH>' `
  -CollectorServiceName '<SCM-SERVICE-NAME>' `
  -ServiceAccount '<DOMAIN\GMSA$>' `
  -SqlServer '<SQL-SERVER>' `
  -DatabaseName '<DATABASE>' `
  -BackupRoot '<APPROVED-BACKUP-ROOT>' `
  -DeploymentManifestPath '<EVIDENCE-PATH>\uninstall.json' `
  -Operator '<OPERATOR>' -Approver '<APPROVER>' `
  -RollbackOwner '<ROLLBACK-OWNER>' `
  -ChangeReference '<CHANGE-REFERENCE>' `
  -WhatIf
```

Use `-RemoveFiles`, `-RemoveConfiguration`, or `-RemoveBackups` only when each
destructive cleanup item is separately approved. Never delete validation
evidence as cleanup. The uninstall does not modify AD or database objects.

## Configuration Management

The required runtime key is
`ConnectionStrings:OperationsDatabase`. The authoritative deployment
environment name is:

```text
PSM__ConnectionStrings__OperationsDatabase
```

The supported runtime provider order, from lower to higher precedence, is:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Development User Secrets, only in Development
4. `PSM__`-prefixed environment variables
5. command-line arguments

Production deployment uses a Machine environment variable by default.
Configuration is read at process startup; a running service must be restarted
under approval to inherit a changed Machine value.

The connection string must use Windows Integrated Authentication and identify
the approved SQL Server and database. SQL Authentication keys, usernames, and
passwords are prohibited. Readiness and deployment reports record the source,
scope, key, and presence only; they never record the value.

Use `Set-PSMWindowsCollectorConfiguration.ps1` with a `SecureString` when a
separate configuration action is required:

```powershell
.\tools\deployment\Set-PSMWindowsCollectorConfiguration.ps1 `
  -OperationsDatabaseConnectionString $approvedSecureString `
  -SqlServer '<SQL-SERVER>' `
  -DatabaseName '<DATABASE>' `
  -EnvironmentVariableTarget Machine `
  -WhatIf
```

Repeat without `-WhatIf` only after approval. Do not print, serialize, or log
`$approvedSecureString` or its plaintext value.

## Windows Service Management

| Property | Expected value |
|---|---|
| Executable | `PSMOperationsPlatform.WindowsCollector.exe` under the approved install path |
| Display name | `PSM Operations Platform Windows Collector` |
| Description | `PSM Operations Platform read-only Windows inventory collector` |
| SCM service name | Deployment-defined and recorded in the manifest |
| Identity | Pre-existing dedicated Windows Collector `DOMAIN\gMSA$` |
| Password | None supplied |
| Start mode | Demand/manual |
| File permission | Inherited `ReadAndExecute` for the gMSA on the install directory |

Inspect service state without mutation:

```powershell
Get-CimInstance Win32_Service -Filter "Name='<SCM-SERVICE-NAME>'" |
  Select-Object Name,DisplayName,State,StartMode,StartName,PathName,ProcessId
```

Starting, stopping, or restarting is a privileged operational action. Perform
it only through the approved SCM procedure and record the approval, request
time, resulting state, process identity, and evidence. Never kill the
Collector process. An unexpected service account correction requires explicit
`-Force` approval in deployment tooling.

## Readiness Framework

Readiness has two modes:

- `CollectorHost` performs local deployment-usability checks. It does not
  contact a target or SQL Server.
- `SmokeTest` adds target DNS/TCP/WinRM and SQL authentication, schema,
  migration, and effective-permission checks.

| Result | Exit code | Operator action |
|---|---:|---|
| `READY` | 0 | Continue to the next approved checkpoint |
| `WARNING` | 1 | Record and review; continue only when the applicable exception is explicitly approved |
| `NOT_READY` | 2 | Stop, remediate through an approved owner/process, and rerun |

Mandatory skipped checks result in `NOT_READY`. Authentication skip switches
are diagnostic controls, not a way to pass SmokeTest readiness.

Windows Server 2022 or later is required for production support and
certification. A 64-bit Windows Server 2019 Collector host may be used only for
approved controlled-lab behavior evidence. Its readiness result remains
`WARNING`/exit `1`, and it cannot provide production certification.

The output directory must already exist. Readiness writes fixed
`collector-readiness.json` and `collector-readiness.md` files unless a format
is explicitly disabled.

## Deployment Validation

Deployment safety uses four separate forms of evidence:

1. `WhatIf` shows planned changes and must precede mutation.
2. Infrastructure/deployment validation reads installed files, hashes, service metadata,
   configuration, ACL, runtime, manifest, readiness, and optional backup state.
3. Operations Database validation proves the existing schema and effective
   runtime permissions without mutation.
4. Readiness establishes host usability and, in SmokeTest mode, approved
   external prerequisites.
5. The deployment manifest records identity, approvals, artifact, paths,
   redacted configuration status, rollback, readiness, warnings, errors, and
   changed files.

The deployment orchestrator writes JSON and Markdown using the base path passed
to `-DeploymentManifestPath`. Deployment validation similarly writes JSON and
Markdown using `-ReportPath`. Store them in a restricted evidence directory.

WhatIf does not prove that files, ACLs, service configuration, identity, target
connectivity, schema, or SQL access are correct. Database validation and
readiness are reported as planned during WhatIf and execute only in the
approved non-WhatIf flow.

## Controlled Execution

WP-006.2 validates the existing continuous Collector against exactly one
approved non-production target. The Collector has no one-shot mode. Exactly
one active Collector instance is allowed during initial validation.

The controlled happy path is:

1. Complete the execution manifest from
   [WP-006.2 Execution Manifest Template](../testing/templates/WP-006.2-Execution-Manifest.template.json).
2. Run local preflight and review `READY` or an approved `WARNING`.
3. Prove exactly one enabled and due target using SQL baseline evidence.
4. Back up and temporarily enable the approved lab Debug logging profile.
5. Obtain explicit approval and start the existing service.
6. Observe one cycle and all seven ordered inventory modules.
7. Obtain explicit approval and request a graceful stop.
8. Capture post-run and verification SQL plus host and log evidence.
9. Restore the original logging bytes/hash and prior service state.
10. Run offline evidence validation and complete the
    [Evidence Index](../testing/templates/WP-006.2-Evidence-Index.template.md).

All seven modules must succeed for the qualifying happy path. A failed
ownership boundary is partial target success, not a clean pass. Modules persist
independent ownership boundaries; earlier successful commits may remain after
a later module fails.

Failure injection and production certification are later, separately approved
activities. Do not begin WP-006.3 or failure injection until the happy-path
evidence has been reviewed.

## SQL Verification

SQL verification uses Windows Integrated Authentication and the approved
`ManagedServerId`. Never infer identity by hostname and never print a
connection string.

The three evidence phases are:

| Phase | Purpose |
|---|---|
| `Baseline` | Prove migration, approved target identity, isolation, existing state, duplicates, and orphans before execution |
| `PostRun` | Capture target-scoped current state, stable keys, row counts, and capture ranges after execution |
| `Verification` | Confirm singular uniqueness, stable-key uniqueness, IPv4 ownership, current-state replacement, and repeat behavior |

Use `Get-WP0062SqlEvidence.ps1` as shown in the controlled runbook. Use
[WP-005 Database Verification Queries](../testing/WP-005-Database-Verification-Queries.md)
for the detailed query and integrity definitions; do not maintain a separate
query copy in this guide.

Rows returned by duplicate or orphan assertions are defects. Generated row
GUIDs are not stable business identity. Compare singular target keys and
collection `StableSourceKey` values. Since `CapturedAt` has no offset, compare
it with the recorded Collector clock.

## Logging

Default application logging is `Information`. Service startup and shutdown are
Information events `2300` and `2301`. Successful cycle and module detail,
including module start/completion events `2351` and `2352`, requires Debug.

The repository does not define a dedicated production file-log sink or its
retention policy. Use only the approved lab capture method and do not claim
production logging certification from it.

Temporary lab Debug procedure:

1. Record the active configuration source and original file SHA-256.
2. Copy the original file to restricted backup storage.
3. Record approval for the temporary Debug values.
4. Ensure no secret is introduced.
5. Validate JSON and record the changed hash.
6. Restart only at the approved execution checkpoint.
7. Capture the required events and correlation IDs.
8. Restore the exact original bytes and verify the original hash.
9. Restore the prior service state and record rollback-owner signoff.

Logs and evidence must exclude connection strings, credentials, raw exception
messages, sensitive payloads, tokens, and unnecessary topology.

## Routine Operational Tasks

### Check service

Use the read-only `Get-CimInstance` command in the Windows Service Management
section. Confirm the exact SCM name, state, start mode, gMSA, quoted executable
path, and process ID.

### Start, stop, or restart service

Obtain explicit service-control approval. Record before/after service and
process evidence. Use the organization’s supported SCM procedure. For restart
after configuration change, confirm the new value was delivered securely,
then run readiness and deployment validation after the service returns.

### Run readiness

Run `Invoke-CollectorReadiness.ps1 -Mode CollectorHost` for local deployment
checks. Use `-Mode SmokeTest` only within an approved execution gate because it
contacts the specified target and SQL Server.

### Validate deployment

Run `Test-PSMWindowsCollectorDeployment.ps1` after installation, upgrade,
configuration-related restart, unexpected service metadata change, or artifact
replacement.

### Verify configuration

Use readiness or deployment validation. Confirm provider, key, scope, presence,
Integrated Authentication, and approved SQL/database metadata. Do not retrieve
or display the value for evidence.

### Review evidence

Check deployment JSON/Markdown, readiness JSON/Markdown, hashes, service
metadata, WP-006.2 manifest, evidence index, host snapshots, SQL evidence,
logs, restoration proof, deviations, and redaction review.

### Check current version

Review Collector file version evidence, approved executable SHA-256, publish
manifest, deployment manifest, and repository commit. A missing file-version
value is a warning; artifact provenance and hash remain required.

### Confirm gMSA

Review service `StartName` and readiness identity results. If the AD module is
unavailable or `Test-ADServiceAccount` fails, stop and ask the authorized AD
owner to correct host/account authorization. Deployment tooling never modifies
AD.

## Upgrade Checklist

- [ ] Approved change window, operator, approver, and rollback owner recorded.
- [ ] Current deployment validation reviewed.
- [ ] New repository commit, package, executable hash, and publish manifest approved.
- [ ] Service path and gMSA match the manifest.
- [ ] Backup root exists and new timestamped path does not.
- [ ] Upgrade WhatIf reviewed.
- [ ] Desired final service state explicitly recorded.
- [ ] Upgrade performed only after approval.
- [ ] Backup path and rollback result recorded.
- [ ] CollectorHost readiness passed or warning explicitly approved.
- [ ] Deployment validation passed with expected artifact and backup.
- [ ] Service state, configuration presence, ACL, and hashes verified.
- [ ] Reports reviewed for secrets and retained in restricted evidence storage.

## Troubleshooting

### Service will not start or Collector crashes

Verify executable path, file presence/hash, .NET 10 runtime, gMSA, Machine
configuration presence, Integrated Authentication metadata, and startup logs.
Run CollectorHost readiness and deployment validation. Do not repeatedly start,
change identity, or kill a process without approval. Preserve crash and event
evidence without raw secrets.

### Readiness returns WARNING

Read the specific check and recommendation. A stopped service, unavailable file
version, unavailable AD module, or Server 2019 support policy can produce a
warning. Continue only when the applicable runbook permits and the approver
records acceptance. Server 2019 remains non-certifying.

### Readiness returns NOT_READY

Stop. Mandatory failure or skipped checks make the conclusion unsafe. Correct
the reported prerequisite through its authorized owner and rerun. Do not bypass
checks, disable certificate validation, change TrustedHosts, or use an
authentication skip to claim readiness.

### gMSA validation fails

Confirm the service uses the approved `DOMAIN\gMSA$`, the ActiveDirectory
module is available where required, the account is installed and usable on the
host, and it has the separately managed logon authorization. Escalate AD
changes to the AD owner.

### Environment variable is missing

Confirm `PSM__ConnectionStrings__OperationsDatabase` exists at the approved
scope or another higher-precedence approved provider supplies the value. Set it
only through the secure configuration procedure. Restart under approval because
Machine configuration is read at startup.

### Configuration is invalid

Readiness distinguishes missing optional JSON, unreadable JSON, invalid JSON,
and a missing OperationsDatabase value. A missing optional JSON file is
acceptable when a higher-precedence approved provider supplies the value.
Reject SQL Authentication and mismatched SQL/database metadata.

### SQL is unreachable or permission validation fails

In SmokeTest mode, review DNS, TCP, TLS, database existence, Windows identity,
migration, required tables, and effective permissions. The readiness tool does
not remediate or apply migrations. Database owners correct schema or
least-privilege permissions under separate approval.

### Operations Database schema validation fails

Keep the service stopped and preserve the database validation reports. Do not
use the Collector runtime as a schema test and do not use `Force`. Generate or
obtain the approved idempotent migration script from the actual Infrastructure
project, have an authorized DBA/deployment identity review and apply it, verify
`dbo.__EFMigrationsHistory`, then rerun database validation and CollectorHost
readiness.

If the failed target is the disposable smoke-test database and may be partially
modified, first inspect `dbo.__EFMigrationsHistory`. Prefer recreating that
disposable database through its separately approved procedure; do not attempt
automated schema repair. Have the authorized DBA apply the newly generated,
reviewed idempotent script, then rerun
`Test-PSMOperationsDatabaseSchema.ps1`. This guidance does not authorize
recreating or repairing a production database.

Generate the script only on a development or build machine containing the full
repository, .NET SDK, and `dotnet-ef`. The design-time process requires
`ConnectionStrings__OperationsDatabase` with Windows Integrated
Authentication; never expose that value. Do not run `dotnet ef database
update`.

### Deployment rollback fails

Stop all changes. Preserve the original error, deployment reports, immutable
backup, service metadata, and rollback status. Do not retry or delete the
backup. Escalate to the named rollback owner.

### Duplicate or orphan inventory is detected

Stop certification. Preserve Baseline/PostRun/Verification exports and the
approved target ID. Use the repository SQL verification definitions. Do not
delete or repair rows through these tools.

### WinRM is unavailable

Review DNS, configured port, listener, firewall policy, certificate trust,
authentication, authorization, and transport policy. Do not use IP fallback,
TrustedHosts, certificate bypass, or credentials.

### Kerberos error 0x80090322

The Collector classifies structured error code `0x80090322` as
`KerberosSpnMismatch`. This can indicate an HTTP SPN owned by another
principal, duplicate registration, a missing port-qualified SPN, or wider
domain trust/Kerberos configuration problems. It does not prove that IIS is
the cause.

IIS application pools may legitimately use a domain account or gMSA that owns
`HTTP/server` and `HTTP/server.fqdn`. Moving those SPNs can break IIS Kerberos.
The Collector uses explicit Kerberos and a port-qualified SPN instead:
`HTTP/server.fqdn:<selected-port>`. HTTPS WinRM also uses the `HTTP` service
class. Existing IIS SPNs normally need no change.

Do not copy or run `setspn` commands before an authorized AD owner checks
current ownership and duplicates. The Collector neither queries nor modifies
AD. Do not use TrustedHosts, Basic authentication, credentials, NTLM fallback,
or certificate-validation bypass as remediation.

Until a separately approved schema migration exists, structured diagnostics
retain `KerberosSpnMismatch` while
`ManagedServer.LastConnectivityFailureCategory` stores the compatible
`AuthenticationFailure` value.

### HTTP fallback did or did not occur

`Auto` starts with HTTPS. HTTP fallback is allowed only after an eligible
HTTPS-specific failure such as TLS validation, unavailable listener,
connection refusal, timeout, network/firewall, or WSMan negotiation failure.
There is no fallback for DNS, authentication, authorization, cancellation,
invalid target configuration, or unexpected internal failure. `HttpsOnly`
never falls back; `HttpOnly` starts with HTTP. HTTP use requires explicit
approval.

### Partial target success

Each inventory ownership boundary commits independently. A later failure does
not erase earlier successful boundaries. Record the failed boundary and mixed
freshness as a deviation, preserve evidence, and perform an approved recovery
rerun. Do not record it as the qualifying clean happy path.

## Operational Best Practices

- Use WhatIf before every deployment, upgrade, uninstall, or configuration
  mutation.
- Record exact repository commit and artifact SHA-256.
- Install without starting; start only at an approved checkpoint.
- Capture and review CollectorHost readiness after deployment or upgrade.
- Preserve deployment manifests, readiness reports, backups, execution
  manifests, SQL evidence, logs, and restoration evidence.
- Keep all evidence access-controlled and secret-free.
- Use one active Collector during initial WP-006 validation.
- Prove exactly one enabled/due non-production target for controlled execution.
- Use Windows Integrated Authentication and the dedicated Windows Collector
  gMSA.
- Treat every unapproved warning, identity mismatch, secret exposure,
  duplicate/orphan, rollback failure, or unexpected mutation as a stop
  condition.

## Decision Tree

```text
NEW COLLECTOR HOST
  |
  +-> Prerequisites and approvals complete?
        |-- No -> STOP and resolve with the owning team
        `-- Yes
             -> Publish and approve artifact
             -> Install WhatIf
             -> Install without start
             -> CollectorHost readiness
             -> Deployment validation
             -> Approved controlled execution

UPGRADE
  |
  +-> Current validation and rollback plan acceptable?
        |-- No -> STOP
        `-- Yes
             -> Publish/approve new artifact
             -> Upgrade WhatIf
             -> Backup and upgrade
             -> Readiness
             -> Deployment validation
             -> Retain backup and evidence

PROBLEM
  |
  -> Preserve current state and evidence
  -> CollectorHost readiness
  -> Deployment validation
  -> Review service/log/configuration evidence
  -> SmokeTest or SQL evidence only under approval
  -> Correct through the authorized owner
  -> Rerun validation

REMOVE
  |
  -> Capture final evidence
  -> Uninstall WhatIf
  -> Conservative uninstall
  -> Verify preserved files/configuration/backups/evidence
  -> Optional cleanup only with separate approval
```

## Frequently Used Commands

### CollectorHost readiness

```powershell
.\tools\readiness\Invoke-CollectorReadiness.ps1 `
  -Mode CollectorHost `
  -CollectorInstallPath '<INSTALL-PATH>' `
  -CollectorServiceName '<SCM-SERVICE-NAME>' `
  -ExpectedServiceAccount '<DOMAIN\GMSA$>' `
  -OutputDirectory '<EXISTING-READINESS-DIRECTORY>'
```

### SmokeTest readiness

```powershell
.\tools\readiness\Invoke-CollectorReadiness.ps1 `
  -Mode SmokeTest `
  -CollectorInstallPath '<INSTALL-PATH>' `
  -CollectorServiceName '<SCM-SERVICE-NAME>' `
  -TargetFqdn '<APPROVED-TARGET-FQDN>' `
  -TransportPolicy '<Auto|HttpsOnly|HttpOnly>' `
  -WinRmHttpsPort 5986 `
  -WinRmHttpPort 5985 `
  -SqlServer '<SQL-SERVER>' `
  -SqlPort 1433 `
  -DatabaseName '<DATABASE>' `
  -ExpectedServiceAccount '<DOMAIN\GMSA$>' `
  -OutputDirectory '<EXISTING-READINESS-DIRECTORY>'
```

### WP-006.2 preflight

Use the complete parameterized command in the
[Controlled Execution Runbook](../testing/WP-006.2-Controlled-Single-Target-Execution-Runbook.md#parameterized-command-patterns).
Every value is mandatory and must come from the approved execution manifest.

### SQL evidence

```powershell
.\tools\validation\Get-WP0062SqlEvidence.ps1 `
  -SqlServer '<SQL-SERVER>' `
  -DatabaseName '<DATABASE>' `
  -ManagedServerId '<APPROVED-MANAGED-SERVER-GUID>' `
  -TargetFqdn '<APPROVED-TARGET-FQDN>' `
  -Phase '<Baseline|PostRun|Verification>' `
  -ExpectedMigrationId '20260727230000_AddWindowsInventoryCurrentState' `
  -EvidenceRoot '<EVIDENCE-ROOT>'
```

### Operations Database schema validation

```powershell
.\tools\deployment\Test-PSMOperationsDatabaseSchema.ps1 `
  -OperationsDatabaseConnectionString $approvedSecureString `
  -SqlServer '<SQL-SERVER>' `
  -DatabaseName '<DATABASE>' `
  -RepositoryRoot '<REPOSITORY-ROOT>' `
  -ReportPath '<EVIDENCE-ROOT>\PSMOperationsDatabaseValidation.json'
```

### Generate an idempotent migration script

Run from the repository root on an approved development or build machine, not
from a normal Collector host:

```powershell
dotnet ef migrations list `
  --no-connect `
  --project .\src\PSMOperationsPlatform.Infrastructure\PSMOperationsPlatform.Infrastructure.csproj `
  --startup-project .\src\PSMOperationsPlatform.WindowsCollector\PSMOperationsPlatform.WindowsCollector.csproj `
  --context OperationsDbContext

dotnet ef migrations script `
  --idempotent `
  --project .\src\PSMOperationsPlatform.Infrastructure\PSMOperationsPlatform.Infrastructure.csproj `
  --startup-project .\src\PSMOperationsPlatform.WindowsCollector\PSMOperationsPlatform.WindowsCollector.csproj `
  --context OperationsDbContext `
  --output '<APPROVED-OUTPUT-PATH>\OperationsDatabase-Migrations.sql'
```

Review the SQL for migration order, `dbo.__EFMigrationsHistory`, and absence of
secrets. An authorized DBA/deployment identity applies approved SQL under a
separate change. Rerun database validation and readiness before starting the
Collector. Script generation never applies a migration.

### Host evidence

```powershell
.\tools\validation\Get-WP0062HostEvidence.ps1 `
  -CollectorHost '<COLLECTOR-HOST>' `
  -CollectorServiceName '<SCM-SERVICE-NAME>' `
  -CollectorExecutablePath '<COLLECTOR-EXE>' `
  -LoggingConfigurationPath '<APPSETTINGS-JSON>' `
  -Snapshot '<PreStart|Running|PostStop>' `
  -EvidenceRoot '<EVIDENCE-ROOT>'
```

### Offline evidence validation

```powershell
.\tools\validation\Test-WP0062Evidence.ps1 `
  -ManifestPath '<COMPLETED-MANIFEST-JSON>' `
  -OutputRoot '<EVIDENCE-ROOT>'
```

## Daily Operations Cheat Sheet

| Need | Action | Continue when | Retain |
|---|---|---|---|
| Check service | Inspect Win32_Service metadata | Name, path, account, mode, state are expected | Service query output |
| Check deployment | Run deployment validation | No failed checks | JSON/Markdown and hashes |
| Check host readiness | Run CollectorHost readiness | READY or approved WARNING | Fixed readiness reports |
| Investigate target/SQL | Run SmokeTest only under approval | READY or approved lab WARNING | Readiness and approval |
| Install | Publish, WhatIf, install stopped, readiness, validate | All identities and hashes match | Publish/deployment/readiness reports |
| Upgrade | Validate, publish, WhatIf, backup, upgrade, readiness, validate | Backup and post-state verified | Backup plus all reports |
| Controlled run | Follow WP-006.2 checkpoints | One target; seven modules succeed | Full execution evidence bundle |
| Troubleshoot | Preserve state, readiness, validation, logs/evidence | Root condition corrected and validations pass | Before/after evidence |
| Remove | WhatIf, conservative uninstall, verify preservation | Approved service removal succeeds | Final/uninstall evidence |

## Security Considerations

- Run deployment only as an approved operator with required local
  administrative rights.
- Use the dedicated Windows Collector gMSA. Never supply a gMSA password.
- Keep Windows Collector and SQL Collector permissions under separate
  identities.
- Use Windows Integrated SQL authentication only.
- Grant the gMSA only inherited `ReadAndExecute` on the install directory.
- Protect deployment, readiness, backup, and evidence directories.
- Redact connection strings, credentials, tokens, raw exceptions, sensitive
  payloads, and unnecessary topology.
- Preserve certificate validation and avoid TrustedHosts.
- Require explicit approval for service control, filesystem/configuration
  changes, SQL preparation, HTTP fallback, temporary Debug logging, and
  cleanup.

## Operational Boundaries

Operators must not:

- Modify AD, create/install a gMSA, or change group membership through this
  package.
- Combine Windows and SQL target permissions under one identity.
- Modify the Collector runtime or inventory persistence semantics.
- Change SQL schema, run migrations, or modify `ManagedServer` through
  deployment/readiness/validation tooling.
- Treat the smoke-test database initializer as a migration tool.
- Bypass readiness, certificate validation, authentication, authorization, or
  transport policy.
- Ignore a `WARNING` without explicit approval.
- Start, stop, restart, kill, or remove a service without the applicable
  change approval.
- Run more than one Collector during initial WP-006 validation.
- Run controlled execution against production or more than one target.
- Claim production certification from a Server 2019 lab result.
- Delete backups or validation evidence as routine cleanup.

## Version Compatibility

| Component | Supported operational baseline |
|---|---|
| Collector host | 64-bit Windows Server 2022 or later for production |
| Lab exception | 64-bit Windows Server 2019 or later for controlled behavior validation; warning required |
| Windows targets | Windows Server 2016, 2019, 2022, or 2025, subject to approved WinRM prerequisites |
| Collector framework | .NET 10 |
| Publish runtime identifier | `win-x64` |
| Publish configuration/framework | `Release` / `net10.0` |
| PowerShell for deployment/readiness | PowerShell 7 or Windows PowerShell 5.1 |
| SQL Server baseline | SQL Server 2022 or later |
| SQL authentication | Windows Integrated Authentication only |

Framework-dependent deployments require the .NET 10 runtime on the Collector
host. Publishing requires the .NET 10 SDK. The Collector hosts PowerShell in
process and does not require PowerShell 7 on a target.

## Appendix

### Glossary

| Term | Meaning |
|---|---|
| CollectorHost | Local-only readiness mode and the Windows Server hosting the Collector |
| SmokeTest | Readiness mode that adds approved target and SQL checks |
| gMSA | Group Managed Service Account used as the Collector service identity |
| OperationsDatabase | Central SQL Server database used for target configuration and current-state persistence |
| Ownership boundary | Independently persisted inventory area such as Disk or Volume |
| StableSourceKey | Stable business key used to compare plural inventory across runs |
| WhatIf | PowerShell preview that reports planned mutations without performing them |
| Deployment manifest | WP-006.2A record of deployment identity, approvals, artifact, changes, readiness, and rollback |
| Execution manifest | WP-006.2 record correlating the controlled run and its evidence |
| Evidence index | Human-reviewed index of approvals, reports, logs, SQL, restoration, and deviations |

### Abbreviations

| Abbreviation | Meaning |
|---|---|
| AD | Active Directory |
| FQDN | Fully Qualified Domain Name |
| SCM | Windows Service Control Manager |
| SQL | Microsoft SQL Server in this guide |
| TLS | Transport Layer Security |
| WinRM | Windows Remote Management |
| WP | Work Package |

### Templates

- [WP-006.2A Deployment Manifest](../deployment/templates/WP-006.2A-Deployment-Manifest.template.json)
- [WP-006.2 Execution Manifest](../testing/templates/WP-006.2-Execution-Manifest.template.json)
- [WP-006.2 Evidence Index](../testing/templates/WP-006.2-Evidence-Index.template.md)

### Operator stop conditions

Stop and preserve evidence for an unapproved host or target, unexpected commit
or hash, identity/path mismatch, missing migration, unproven target isolation,
`NOT_READY`, unapproved warning, secret disclosure, duplicate/orphan data,
Collector crash, rollback failure, restoration failure, more than one target,
or any unexpected mutation.
