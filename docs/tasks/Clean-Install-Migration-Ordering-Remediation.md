---
title: Clean-Install Migration Ordering Remediation
version: 1.0.0
status: Ready for Review
owner: Database
last_updated: 2026-08-01
product: PSM Operations Platform
---

# Clean-Install Migration Ordering Remediation

## Scope and pre-production exception

The first live clean-database deployment stopped in
`20260728142340_WP0073ProcessorInventoryContract` because SQL Server does not
allow `sp_rename` to rename `StableSourceKey` while the unique index on that
column still exists. The same unsafe ordering was present in WP-007.4 and
WP-007.5 for disk, volume, network-adapter, and IPv4 keys.

No production deployment has completed. A later repair migration cannot fix a
clean-install failure because execution never reaches it. The narrow approved
pre-production exception therefore corrects the original affected migrations
without changing their identifiers, the 17-migration manifest, or the final
model snapshot. Each affected migration now drops the dependent index, renames
the column, and recreates the unique index using the final column name.

The final indexes have no filter predicate. Their authoritative shape is:

| Table | Final column | Final unique index | Columns |
|---|---|---|---|
| `WindowsProcessorInventory` | `ProcessorKey` | `UX_WindowsProcessorInventory_ManagedServer_ProcessorKey` | `ManagedServerId, ProcessorKey` |
| `WindowsDiskInventory` | `DiskKey` | `UX_WindowsDiskInventory_ManagedServer_DiskKey` | `ManagedServerId, DiskKey` |
| `WindowsVolumeInventory` | `VolumeKey` | `UX_WindowsVolumeInventory_ManagedServer_VolumeKey` | `ManagedServerId, VolumeKey` |
| `WindowsNetworkAdapterInventory` | `AdapterKey` | `UX_WindowsNetworkAdapterInventory_ManagedServer_AdapterKey` | `ManagedServerId, AdapterKey` |
| `WindowsIpv4AddressInventory` | `Ipv4Key` | `UX_WindowsIpv4AddressInventory_ManagedServer_Ipv4Key` | `ManagedServerId, Ipv4Key` |

The release packager also prepends the SQL Server indexed-object session SET
options. Deployment examples use `sqlcmd -I` in addition to the in-artifact
preamble.

## Disposable SQL Server 2022 validation

This validation is operator-controlled and destructive only to the explicitly
named disposable database. Replace every placeholder, use an authorized SQL
Server 2022 instance, and retain sanitized output as deployment evidence.

```powershell
$disposableDatabase = 'PSMOperations_CleanInstall_Disposable'
$sqlInstance = '<APPROVED-DISPOSABLE-SQL2022-INSTANCE>'
$releaseSql = '.\Release\Database\PSMOperations-v1.0.0-rc1.sql'

sqlcmd -S $sqlInstance -d master -E -I -b -V 16 `
  -Q "CREATE DATABASE [$disposableDatabase];"
sqlcmd -S $sqlInstance -d $disposableDatabase -E -I -b -V 16 -i $releaseSql
sqlcmd -S $sqlInstance -d $disposableDatabase -E -I -b -V 16 -i $releaseSql
sqlcmd -S $sqlInstance -d $disposableDatabase -E -I -b -V 16 `
  -Q "SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;"
sqlcmd -S $sqlInstance -d $disposableDatabase -E -I -b -V 16 `
  -v ExpectedDatabaseName=$disposableDatabase `
     ExpectedCompatibilityLevel=160 `
     ExpectedCollation='<APPROVED-DISPOSABLE-COLLATION>' `
     ExpectedRecoveryModel='<FULL|SIMPLE|BULK_LOGGED>' `
     ExpectedSchemaVersion='20260729191745_WP0088ExecutionHistory' `
  -i '.\Release\Database\SchemaValidation.sql'
sqlcmd -S $sqlInstance -d master -E -I -b -V 16 `
  -Q "ALTER DATABASE [$disposableDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$disposableDatabase];"
```

## Sanitized live validation outcome

Operator-controlled validation against a disposable SQL Server 2022 database
completed successfully:

- first clean-database deployment: **PASS**
- applied migration count: **17**
- latest migration: `20260729191745_WP0088ExecutionHistory`
- `SchemaValidation.sql`: **PASS**
- second execution of the same idempotent release SQL: **PASS**
- migration count after the second execution: **17**
- processor, disk, volume, network-adapter, and IPv4 unique indexes reference
  their final key columns: **PASS**

No server identity, address, operator identity, credentials, local paths, or raw
environment output is retained in this repository record.
