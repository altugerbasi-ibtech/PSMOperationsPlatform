#requires -Version 5.1
Set-StrictMode -Version Latest

function Get-SqlTargetValidationQueries {
    [CmdletBinding()]
    param()
    [ordered]@{
        CoreMetadata=@'
SELECT CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) AS MachineName,
       CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) AS ServerName,
       COALESCE(CAST(SERVERPROPERTY('InstanceName') AS nvarchar(128)),N'MSSQLSERVER') AS InstanceName,
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
       CAST(SERVERPROPERTY('ProductLevel') AS nvarchar(128)) AS ProductLevel,
       CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
       CONVERT(int,SERVERPROPERTY('EngineEdition')) AS EngineEdition,
       CONVERT(bit,SERVERPROPERTY('IsClustered')) AS IsClustered,
       CONVERT(bit,SERVERPROPERTY('IsHadrEnabled')) AS IsHadrEnabled,
       SUSER_SNAME() AS LoginName,c.auth_scheme,c.net_transport,c.encrypt_option,
       c.local_net_address,c.local_tcp_port,c.client_net_address,
       COALESCE(si.sqlserver_start_time,CONVERT(datetime2,'19000101')) AS SqlServerStartTime
FROM sys.dm_exec_connections c CROSS JOIN sys.dm_os_sys_info si
WHERE c.session_id=@@SPID;
'@
        HostPlatform=@'
SELECT TOP (1) host_platform,host_distribution,host_release
FROM sys.dm_os_host_info;
'@
        ServerConfiguration=@'
SELECT name,CONVERT(bigint,value_in_use) AS ValueInUse
FROM sys.configurations
WHERE name IN (N'max server memory (MB)',N'min server memory (MB)',N'max degree of parallelism',
 N'cost threshold for parallelism',N'remote admin connections',N'backup compression default',
 N'optimize for ad hoc workloads',N'blocked process threshold (s)',N'default trace enabled',
 N'contained database authentication');
'@
        DatabaseInventory=@'
SELECT d.name,d.database_id,d.state_desc,d.user_access_desc,d.is_read_only,
       d.recovery_model_desc,d.compatibility_level,d.collation_name,d.containment_desc,
       SUSER_SNAME(d.owner_sid) AS OwnerName,d.create_date,d.is_trustworthy_on,d.is_db_chaining_on,
       SUM(CASE WHEN mf.type=0 THEN 1 ELSE 0 END) AS DataFileCount,
       SUM(CASE WHEN mf.type=1 THEN 1 ELSE 0 END) AS LogFileCount
FROM sys.databases d LEFT JOIN sys.master_files mf ON mf.database_id=d.database_id
WHERE d.name IN (SELECT value FROM STRING_SPLIT(@DatabaseNames,NCHAR(31)))
GROUP BY d.name,d.database_id,d.state_desc,d.user_access_desc,d.is_read_only,
 d.recovery_model_desc,d.compatibility_level,d.collation_name,d.containment_desc,
 d.owner_sid,d.create_date,d.is_trustworthy_on,d.is_db_chaining_on;
'@
        DatabaseFiles=@'
SELECT d.name AS DatabaseName,mf.type_desc,mf.name AS LogicalName,mf.physical_name,
       CONVERT(bigint,mf.size)*8192 AS SizeBytes,mf.max_size,mf.growth,mf.is_percent_growth
FROM sys.master_files mf JOIN sys.databases d ON d.database_id=mf.database_id
WHERE d.name IN (SELECT value FROM STRING_SPLIT(@DatabaseNames,NCHAR(31)));
'@
        BackupMetadata=@'
SELECT d.name AS DatabaseName,MAX(CASE WHEN b.type='D' THEN b.backup_finish_date END) AS LastFull,
       MAX(CASE WHEN b.type='I' THEN b.backup_finish_date END) AS LastDifferential,
       MAX(CASE WHEN b.type='L' THEN b.backup_finish_date END) AS LastLog
FROM sys.databases d LEFT JOIN msdb.dbo.backupset b ON b.database_name=d.name
WHERE d.name IN (SELECT value FROM STRING_SPLIT(@DatabaseNames,NCHAR(31)))
GROUP BY d.name;
'@
        Permissions=@'
SELECT HAS_PERMS_BY_NAME(NULL,NULL,N'CONNECT SQL') AS ConnectSql,
       HAS_PERMS_BY_NAME(NULL,NULL,N'VIEW SERVER STATE') AS ViewServerState,
       HAS_PERMS_BY_NAME(NULL,NULL,N'VIEW SERVER PERFORMANCE STATE') AS ViewServerPerformanceState,
       HAS_PERMS_BY_NAME(NULL,NULL,N'VIEW ANY DATABASE') AS ViewAnyDatabase,
       HAS_PERMS_BY_NAME(NULL,NULL,N'CONNECT ANY DATABASE') AS ConnectAnyDatabase,
       IS_SRVROLEMEMBER(N'sysadmin') AS IsSysadmin;
'@
        PerformanceMetadata=@'
SELECT OBJECT_ID(N'sys.dm_exec_connections') AS Connections,
       OBJECT_ID(N'sys.dm_exec_sessions') AS Sessions,
       OBJECT_ID(N'sys.dm_exec_requests') AS Requests,
       OBJECT_ID(N'sys.dm_os_performance_counters') AS PerformanceCounters,
       OBJECT_ID(N'sys.dm_os_sys_info') AS SystemInfo,
       OBJECT_ID(N'sys.dm_os_process_memory') AS ProcessMemory,
       OBJECT_ID(N'sys.dm_os_wait_stats') AS WaitStats,
       OBJECT_ID(N'sys.dm_io_virtual_file_stats') AS VirtualFileStats,
       OBJECT_ID(N'sys.databases') AS Databases,
       OBJECT_ID(N'sys.master_files') AS MasterFiles;
'@
        Availability=@'
SELECT CONVERT(bit,SERVERPROPERTY('IsClustered')) AS IsClustered,
       CONVERT(bit,SERVERPROPERTY('IsHadrEnabled')) AS IsHadrEnabled,
       (SELECT COUNT_BIG(*) FROM sys.availability_groups) AS AvailabilityGroupCount,
       (SELECT COUNT_BIG(*) FROM sys.database_mirroring WHERE mirroring_guid IS NOT NULL) AS MirroredDatabaseCount;
'@
    }
}

function Test-SqlTargetQuerySafety {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Query)
    $withoutComments=[regex]::Replace($Query,'(?s)/\*.*?\*/|--[^\r\n]*',' ')
    $prohibited='(?im)(^|;)\s*(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|DENY|REVOKE|RECONFIGURE|BACKUP|RESTORE|SHRINK|CHECKPOINT|KILL)\b|\bEXECUTE\s+AS\b|\bDBCC\s+(FREEPROCCACHE|DROPCLEANBUFFERS)\b'
    return -not [regex]::IsMatch($withoutComments,$prohibited)
}
