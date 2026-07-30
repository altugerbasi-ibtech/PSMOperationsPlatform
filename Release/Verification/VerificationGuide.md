---
title: WP-009.5 Release Verification Guide
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
# WP-009.5 Release Verification Guide

## Purpose

These scripts collect read-only infrastructure evidence after an approved
deployment. They do not deploy, configure, register, repair, grant, restart,
or modify the environment. Running them does not constitute release approval.

Every script writes structured JSON to standard output and exits:

- `0` with `Status` equal to `PASS`;
- `1` with `Status` equal to `FAIL`.

Each result includes `Check`, safe `Target`, detailed `Diagnostics`, and
`TimestampUtc`. Capture standard output using the approved evidence mechanism;
the scripts do not create evidence files themselves.

## Prerequisites

- Windows PowerShell 5.1 or later on an approved administration host.
- Windows Integrated identities authorized only for the requested checks.
- Approved target names, ports, gMSA names, SPNs, database name, and expected
  migration from the deployment record.
- DNS, Kerberos, AD, WinRM, network, and SQL paths required by the checks.
- ActiveDirectory PowerShell module for gMSA verification.
- `setspn.exe`, `Test-WSMan`, and `Test-NetConnection`.
- SQL Server certificate trust when running SQL verification.

Do not pass passwords or connection strings. Do not run under an identity with
unnecessary administrative rights.

## Execution Order

Run in this order so foundational failures stop dependent checks:

1. `Verify-Network.ps1`
2. `Verify-SPN.ps1`
3. `Verify-gMSA.ps1`
4. `Verify-WinRM.ps1`
5. `Verify-SQL.ps1`
6. Run WP-009.3 `SchemaValidation.sql`.
7. Run WP-009.4 `PermissionValidation.sql`.

Do not hide or overwrite an earlier failure by continuing remediation during
verification. Preserve the failure evidence, stop the affected verification
path, and use separately approved operational procedures.

## Network Verification

Checks TCP connectivity only for explicitly supplied ports:

```powershell
.\Release\Verification\Verify-Network.ps1 `
  -ComputerName '<APPROVED-HOST>' `
  -Port 5986,1433
```

PASS means every requested TCP connection succeeded. It does not prove
application authentication or authorization.

## SPN Verification

Uses read-only `setspn.exe -Q` lookup:

```powershell
.\Release\Verification\Verify-SPN.ps1 `
  -ServiceClass HTTP `
  -HostName '<APPROVED-FQDN>' `
  -ExpectedAccount '<APPROVED-DOMAIN-ACCOUNT>'
```

For SQL Server, use the service class and host form approved for that
deployment. PASS means the SPN exists and, when supplied, its lookup output
contains the expected owner. The script never uses `setspn -S`, `-A`, or `-D`.

## gMSA Verification

Checks account visibility, enabled state, and whether the current host can use
the gMSA:

```powershell
.\Release\Verification\Verify-gMSA.ps1 `
  -Identity '<APPROVED-gMSA-NAME>'
```

The command uses `Get-ADServiceAccount` and `Test-ADServiceAccount` only. It
does not install an account, reset a password, or change AD.

## WinRM Verification

Performs an authenticated WSMan identification request:

```powershell
.\Release\Verification\Verify-WinRM.ps1 `
  -ComputerName '<APPROVED-FQDN>' `
  -Port 5986
```

HTTPS is the default. HTTP requires the explicit `-UseHttp` switch and an
approved target policy:

```powershell
.\Release\Verification\Verify-WinRM.ps1 `
  -ComputerName '<APPROVED-FQDN>' `
  -Port 5985 `
  -UseHttp
```

The script does not execute remote commands, alter TrustedHosts, change WinRM,
or disable certificate validation.

## SQL Verification

Uses Windows Integrated Authentication, encrypted SqlClient transport, and a
read-only metadata query:

```powershell
.\Release\Verification\Verify-SQL.ps1 `
  -Server '<APPROVED-SQL-SERVER>' `
  -Database '<APPROVED-DATABASE>' `
  -Port 1433 `
  -ExpectedMigration '<APPROVED-MIGRATION-ID>'
```

PASS requires an integrated connection, the expected database in `ONLINE`
state, and—when supplied—the expected latest EF migration. The output also
reports compatibility level, recovery model, and collation. It performs no
DDL, DML, permission change, or automatic migration.

## Diagnostics and Evidence

Each diagnostic has:

- `Code`
- `Status` (`PASS`, `FAIL`, or `INFO`)
- `Summary`
- `Evidence`

Raw exception messages, credentials, and connection strings are excluded.
Targets and approved identity names may be operationally sensitive; store
captured JSON only in the approved evidence location.

A PASS proves only the bounded check at that time. It does not prove backup
viability, capacity, performance, all permissions, application health, or
production certification.

## Failure Handling

On FAIL:

1. preserve the JSON and command parameters in the change record;
2. confirm target and approved input values;
3. classify the failed dependency;
4. stop dependent verification where the failed prerequisite makes results
   unreliable;
5. use separately approved remediation owned by Operations, Security, DBA, or
   AD teams; and
6. rerun the failed check and all dependent checks after remediation.

The verification package never authorizes firewall changes, SPN registration,
gMSA installation, WinRM changes, SQL grants, schema repair, migration
execution, service control, or deployment.

## Suggested Evidence Index

Record these files outside the repository using the approved evidence system:

```text
01-Network.json
02-SPN.json
03-gMSA.json
04-WinRM.json
05-SQL.json
06-SchemaValidation.txt
07-PermissionValidation.txt
```

Evidence acceptance and production certification remain under WP-007.Z.
