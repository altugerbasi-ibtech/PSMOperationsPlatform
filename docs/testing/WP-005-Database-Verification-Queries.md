---
title: WP-005 Database Verification Queries
version: 1.0.0
status: Prepared
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# WP-005 Database Verification Queries

Run with Integrated Authentication only against the approved test database.
Set the known `ManagedServer.Id`; do not infer it by hostname.

```sql
SET NOCOUNT ON;
DECLARE @ManagedServerId uniqueidentifier = '<APPROVED-MANAGED-SERVER-ID>';
SELECT DB_NAME() DatabaseName, SUSER_SNAME() IntegratedIdentity;
SELECT MigrationId FROM dbo.__EFMigrationsHistory
WHERE MigrationId=N'20260727230000_AddWindowsInventoryCurrentState';

SELECT Id,Fqdn,IsEnabled,WinRmTransportMode,WinRmHttpsPort,WinRmHttpPort,
 LastConnectivityState,LastConnectivityFailureCategory,
 ConsecutiveConnectivityFailures,LastConnectivityAttemptAt,
 LastConnectivitySuccessAt,LastSuccessfulTransport,NextConnectivityAttemptAt
FROM configuration.ManagedServer WHERE Id=@ManagedServerId;

SELECT ManagedServerId,ComputerName,Fqdn,DomainName,Manufacturer,Model,
 SerialNumber,CapturedAt FROM inventory.WindowsComputerInventory
WHERE ManagedServerId=@ManagedServerId;
SELECT ManagedServerId,Caption,Version,BuildNumber,Architecture,InstallDate,
 LastBootTime,CapturedAt FROM inventory.WindowsOperatingSystemInventory
WHERE ManagedServerId=@ManagedServerId;
SELECT ManagedServerId,TotalPhysicalMemoryBytes,CapturedAt
FROM inventory.WindowsMemoryInventory WHERE ManagedServerId=@ManagedServerId;
SELECT StableSourceKey,Name,Manufacturer,CoreCount,LogicalProcessorCount,
 MaxClockSpeedMhz,CapturedAt FROM inventory.WindowsProcessorInventory
WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey;
SELECT StableSourceKey,DiskNumber,FriendlyName,SerialNumber,SizeBytes,BusType,
 PartitionStyle,CapturedAt FROM inventory.WindowsDiskInventory
WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey;
SELECT StableSourceKey,DriveLetter,FileSystem,Label,SizeBytes,FreeSpaceBytes,
 CapturedAt FROM inventory.WindowsVolumeInventory
WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey;
SELECT StableSourceKey,Name,InterfaceDescription,MacAddress,
 LinkSpeedBitsPerSecond,OperationalStatus,CapturedAt
FROM inventory.WindowsNetworkAdapterInventory
WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey;
SELECT ip.StableSourceKey,ip.Address,ip.PrefixLength,ip.CapturedAt,
 adapter.StableSourceKey AdapterStableSourceKey
FROM inventory.WindowsIpv4AddressInventory ip
JOIN inventory.WindowsNetworkAdapterInventory adapter
 ON adapter.Id=ip.NetworkAdapterInventoryId
 AND adapter.ManagedServerId=ip.ManagedServerId
WHERE ip.ManagedServerId=@ManagedServerId ORDER BY ip.StableSourceKey;
```

## Integrity assertions

Rows returned are defects:

```sql
DECLARE @ManagedServerId uniqueidentifier = '<APPROVED-MANAGED-SERVER-ID>';
SELECT N'Computer duplicate' Defect,COUNT_BIG(*) Actual
FROM inventory.WindowsComputerInventory WHERE ManagedServerId=@ManagedServerId
HAVING COUNT_BIG(*)>1;
SELECT N'OS duplicate' Defect,COUNT_BIG(*) Actual
FROM inventory.WindowsOperatingSystemInventory WHERE ManagedServerId=@ManagedServerId
HAVING COUNT_BIG(*)>1;
SELECT N'Memory duplicate' Defect,COUNT_BIG(*) Actual
FROM inventory.WindowsMemoryInventory WHERE ManagedServerId=@ManagedServerId
HAVING COUNT_BIG(*)>1;

SELECT N'Processor' Defect,StableSourceKey,COUNT_BIG(*) Actual
FROM inventory.WindowsProcessorInventory WHERE ManagedServerId=@ManagedServerId
GROUP BY StableSourceKey HAVING COUNT_BIG(*)>1;
SELECT N'Disk' Defect,StableSourceKey,COUNT_BIG(*) Actual
FROM inventory.WindowsDiskInventory WHERE ManagedServerId=@ManagedServerId
GROUP BY StableSourceKey HAVING COUNT_BIG(*)>1;
SELECT N'Volume' Defect,StableSourceKey,COUNT_BIG(*) Actual
FROM inventory.WindowsVolumeInventory WHERE ManagedServerId=@ManagedServerId
GROUP BY StableSourceKey HAVING COUNT_BIG(*)>1;
SELECT N'Adapter' Defect,StableSourceKey,COUNT_BIG(*) Actual
FROM inventory.WindowsNetworkAdapterInventory WHERE ManagedServerId=@ManagedServerId
GROUP BY StableSourceKey HAVING COUNT_BIG(*)>1;
SELECT N'IPv4' Defect,StableSourceKey,COUNT_BIG(*) Actual
FROM inventory.WindowsIpv4AddressInventory WHERE ManagedServerId=@ManagedServerId
GROUP BY StableSourceKey HAVING COUNT_BIG(*)>1;

SELECT ip.Id,ip.StableSourceKey FROM inventory.WindowsIpv4AddressInventory ip
LEFT JOIN inventory.WindowsNetworkAdapterInventory adapter
 ON adapter.Id=ip.NetworkAdapterInventoryId
 AND adapter.ManagedServerId=ip.ManagedServerId
WHERE ip.ManagedServerId=@ManagedServerId AND adapter.Id IS NULL;
SELECT Id,StableSourceKey,Address FROM inventory.WindowsIpv4AddressInventory
WHERE ManagedServerId=@ManagedServerId AND Address LIKE N'%:%';
```

Compare pre/post failure exports to prove preservation and another approved
target's counts to prove isolation. Since `CapturedAt` has no offset, compare
against the recorded collector clock; use SQL server local time only if its
timezone is also confirmed as Türkiye.
