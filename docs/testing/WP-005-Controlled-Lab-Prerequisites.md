---
title: WP-005 Controlled Lab Prerequisites
version: 1.0.0
status: Prepared
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# WP-005 Controlled Lab Prerequisites

Run checks only after the execution gate is satisfied. Replace every
placeholder explicitly; never infer it. Commands are read-only.

The official automated prerequisite check is
`tools/readiness/Invoke-CollectorReadiness.ps1 -Mode SmokeTest`. The manual
commands below remain operator reference and evidence aids; they do not define
a second automation entry point.

## Topology and identity

| Component | Requirement |
|---|---|
| Collector | Domain-joined Windows Server, approved WP-005 build, test identity |
| Target | Dedicated non-production Windows Server 2022/2025, known inventory |
| Database | Dedicated non-production SQL database with WP-005 migration |

A single-server lab is a reported limitation. Use Windows Integrated
Authentication with a domain account or gMSA. Do not use passwords,
`PSCredential`, SQL authentication, or embedded credentials. Local
administrator is not assumed. On a permission failure, record the denied
namespace/class and stop; do not change permissions.

## Collector checks

```powershell
Get-ComputerInfo -Property WindowsProductName,WindowsVersion,OsBuildNumber
dotnet --info
Get-CimInstance Win32_ComputerSystem -Property Name,Domain
Get-Date
Get-TimeZone
Get-CimInstance Win32_Service -Filter "Name='<COLLECTOR_SERVICE_NAME>'" `
  -Property Name,State,StartName,PathName
Get-FileHash '<PUBLISHED_COLLECTOR_EXE>' -Algorithm SHA256
Resolve-DnsName '<LAB_TARGET_FQDN>' -Type A
Test-NetConnection '<TEST_SQL_HOST>' -Port <TEST_SQL_PORT> -InformationLevel Detailed
```

Record deployed version/hash and repository commit. Configuration precedence is
JSON, environment-specific JSON, Development User Secrets only in Development,
`PSM__` environment variables, then command line. The database key is
`ConnectionStrings:OperationsDatabase`. Do not print its value; an operator
must confirm the parsed server/database and Integrated Security.

## Target and network checks

```powershell
Resolve-DnsName '<LAB_TARGET_FQDN>' -Type A
Test-NetConnection '<LAB_TARGET_FQDN>' -Port <HTTPS_PORT> -InformationLevel Detailed
Test-NetConnection '<LAB_TARGET_FQDN>' -Port <HTTP_PORT> -InformationLevel Detailed
Test-WSMan -ComputerName '<LAB_TARGET_FQDN>' -UseSSL -Port <HTTPS_PORT> `
  -Authentication Kerberos
```

Run HTTP `Test-WSMan` only for approved `Auto`/`HttpOnly`. Never use certificate
bypass, TrustedHosts, or WinRM configuration commands. The collector and any
read-only remoting evidence must authenticate with Kerberos explicitly;
Negotiate is not proof of Kerberos, and an NTLM fallback invalidates readiness.
Do not retry with Default, Negotiate, Basic, or CredSSP authentication. DNS,
endpoint port, SPN and gMSA configuration are integration prerequisites.
Production remoting must retain `IncludePortInSPN`; authentication failure
causes a stop rather than an authentication downgrade.

Required sources are `root/cimv2` (`Win32_ComputerSystem`, `Win32_BIOS`,
`Win32_OperatingSystem`, `Win32_Processor`),
`root/Microsoft/Windows/Storage` (`MSFT_Disk`, `MSFT_Volume`), and
`root/StandardCimv2` (`MSFT_NetAdapter`, `MSFT_NetIPAddress`).

## Database checks

Use Integrated Authentication only:

```sql
SELECT DB_NAME() AS DatabaseName;
SELECT SUSER_SNAME() AS IntegratedIdentity;
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT OBJECT_ID(N'inventory.WindowsComputerInventory') AS InventoryObjectId;
```

Expected migration is
`20260727230000_AddWindowsInventoryCurrentState`. Missing schema/migration is a
stop condition; do not apply it.

Named owners must confirm target and database are non-production. Naming
conventions alone are not proof.
