:setvar CollectorPrincipal "__REQUIRED__"
:setvar PortalPrincipal "__REQUIRED__"
:setvar SqlCollectorPrincipal "__REQUIRED__"

SET NOCOUNT ON;

DECLARE @Results table
(
    PrincipalRole nvarchar(40) NOT NULL,
    PrincipalName sysname NOT NULL,
    Securable nvarchar(776) NOT NULL,
    PermissionName nvarchar(60) NOT NULL,
    ExpectedValue bit NOT NULL,
    ActualValue int NULL
);

DECLARE @Diagnostics table
(
    PrincipalRole nvarchar(40) NOT NULL,
    PrincipalName sysname NOT NULL,
    Securable nvarchar(776) NOT NULL,
    PermissionName nvarchar(60) NOT NULL,
    ExpectedValue nvarchar(20) NOT NULL,
    ActualValue nvarchar(20) NOT NULL,
    Diagnostic nvarchar(2048) NOT NULL
);

DECLARE @CollectorPrincipal sysname=N'$(CollectorPrincipal)';
DECLARE @PortalPrincipal sysname=N'$(PortalPrincipal)';
DECLARE @SqlCollectorPrincipal sysname=N'$(SqlCollectorPrincipal)';

IF @CollectorPrincipal=N'__REQUIRED__' OR USER_ID(@CollectorPrincipal) IS NULL
    INSERT @Diagnostics VALUES
    (N'Collector',@CollectorPrincipal,N'DATABASE_USER',N'EXISTS',N'1',N'0',
     N'Collector database user is missing or was not supplied.');
ELSE
BEGIN
    EXECUTE AS USER=N'$(CollectorPrincipal)';
    INSERT @Results VALUES
    (N'Collector',USER_NAME(),DB_NAME(),N'CONNECT',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONNECT')),
    (N'Collector',USER_NAME(),DB_NAME(),N'SELECT',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'SELECT')),
    (N'Collector',USER_NAME(),DB_NAME(),N'INSERT',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'INSERT')),
    (N'Collector',USER_NAME(),DB_NAME(),N'UPDATE',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'UPDATE')),
    (N'Collector',USER_NAME(),DB_NAME(),N'DELETE',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'DELETE')),
    (N'Collector',USER_NAME(),DB_NAME(),N'EXECUTE',0,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'EXECUTE')),
    (N'Collector',USER_NAME(),DB_NAME(),N'VIEW DATABASE STATE',0,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'VIEW DATABASE STATE'));
    INSERT @Results
    SELECT N'Collector',USER_NAME(),v.SchemaName,v.PermissionName,1,
           HAS_PERMS_BY_NAME(v.SchemaName,N'SCHEMA',v.PermissionName)
    FROM (VALUES
      (N'configuration',N'SELECT'),(N'configuration',N'INSERT'),(N'configuration',N'UPDATE'),(N'configuration',N'DELETE'),
      (N'collection',N'SELECT'),(N'collection',N'INSERT'),(N'collection',N'UPDATE'),(N'collection',N'DELETE'),
      (N'inventory',N'SELECT'),(N'inventory',N'INSERT'),(N'inventory',N'UPDATE'),(N'inventory',N'DELETE'),
      (N'monitoring',N'SELECT'),(N'monitoring',N'INSERT'),(N'monitoring',N'UPDATE'),(N'monitoring',N'DELETE'),
      (N'operations',N'SELECT'),(N'operations',N'INSERT'),(N'operations',N'UPDATE'),(N'operations',N'DELETE'),
      (N'runtime',N'SELECT'),(N'runtime',N'INSERT'),(N'runtime',N'UPDATE'),(N'runtime',N'DELETE'),
      (N'history',N'SELECT'),(N'history',N'INSERT'),(N'history',N'UPDATE'),(N'history',N'DELETE')
    )v(SchemaName,PermissionName);
    REVERT;
END;

IF @PortalPrincipal=N'__REQUIRED__' OR USER_ID(@PortalPrincipal) IS NULL
    INSERT @Diagnostics VALUES
    (N'Portal',@PortalPrincipal,N'DATABASE_USER',N'EXISTS',N'1',N'0',
     N'Portal database user is missing or was not supplied.');
ELSE
BEGIN
    EXECUTE AS USER=N'$(PortalPrincipal)';
    INSERT @Results VALUES
    (N'Portal',USER_NAME(),DB_NAME(),N'CONNECT',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONNECT')),
    (N'Portal',USER_NAME(),DB_NAME(),N'SELECT',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'SELECT')),
    (N'Portal',USER_NAME(),DB_NAME(),N'INSERT',0,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'INSERT')),
    (N'Portal',USER_NAME(),DB_NAME(),N'UPDATE',0,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'UPDATE')),
    (N'Portal',USER_NAME(),DB_NAME(),N'DELETE',0,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'DELETE')),
    (N'Portal',USER_NAME(),DB_NAME(),N'EXECUTE',0,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'EXECUTE')),
    (N'Portal',USER_NAME(),DB_NAME(),N'VIEW DATABASE STATE',0,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'VIEW DATABASE STATE'));
    INSERT @Results
    SELECT N'Portal',USER_NAME(),v.SchemaName,N'SELECT',1,
           HAS_PERMS_BY_NAME(v.SchemaName,N'SCHEMA',N'SELECT')
    FROM (VALUES(N'audit'),(N'collection'),(N'configuration'),(N'history'),
      (N'inventory'),(N'monitoring'),(N'operations'),(N'runtime'))v(SchemaName);
    REVERT;
END;

IF @SqlCollectorPrincipal=N'__REQUIRED__' OR USER_ID(@SqlCollectorPrincipal) IS NULL
    INSERT @Diagnostics VALUES
    (N'SqlCollector',@SqlCollectorPrincipal,N'DATABASE_USER',N'EXISTS',N'1',N'0',
     N'SQL Collector database user is missing or was not supplied.');
ELSE
BEGIN
    EXECUTE AS USER=N'$(SqlCollectorPrincipal)';
    INSERT @Results VALUES
    (N'SqlCollector',USER_NAME(),DB_NAME(),N'CONNECT',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONNECT')),
    (N'SqlCollector',USER_NAME(),DB_NAME(),N'SELECT',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'SELECT')),
    (N'SqlCollector',USER_NAME(),DB_NAME(),N'INSERT',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'INSERT')),
    (N'SqlCollector',USER_NAME(),DB_NAME(),N'UPDATE',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'UPDATE')),
    (N'SqlCollector',USER_NAME(),DB_NAME(),N'DELETE',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'DELETE')),
    (N'SqlCollector',USER_NAME(),DB_NAME(),N'EXECUTE',0,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'EXECUTE')),
    (N'SqlCollector',USER_NAME(),DB_NAME(),N'VIEW DATABASE STATE',1,HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'VIEW DATABASE STATE'));
    INSERT @Results
    SELECT N'SqlCollector',USER_NAME(),v.SchemaName,v.PermissionName,1,
           HAS_PERMS_BY_NAME(v.SchemaName,N'SCHEMA',v.PermissionName)
    FROM (VALUES
      (N'collection',N'SELECT'),(N'collection',N'INSERT'),(N'collection',N'UPDATE'),(N'collection',N'DELETE'),
      (N'inventory',N'SELECT'),(N'inventory',N'INSERT'),(N'inventory',N'UPDATE'),(N'inventory',N'DELETE'),
      (N'monitoring',N'SELECT'),(N'monitoring',N'INSERT'),(N'monitoring',N'UPDATE'),(N'monitoring',N'DELETE')
    )v(SchemaName,PermissionName);
    REVERT;
END;

INSERT @Diagnostics
SELECT PrincipalRole,PrincipalName,Securable,PermissionName,
       CONVERT(nvarchar(20),ExpectedValue),
       COALESCE(CONVERT(nvarchar(20),ActualValue),N'NULL'),
       CASE WHEN ExpectedValue=1
            THEN N'Required effective permission is missing.'
            ELSE N'Prohibited effective permission is present.' END
FROM @Results
WHERE ActualValue IS NULL OR ActualValue<>CONVERT(int,ExpectedValue);

SELECT CASE WHEN EXISTS(SELECT 1 FROM @Diagnostics) THEN N'FAIL' ELSE N'PASS' END AS OverallResult;

IF EXISTS(SELECT 1 FROM @Diagnostics)
    SELECT PrincipalRole,PrincipalName,Securable,PermissionName,
           ExpectedValue,ActualValue,Diagnostic
    FROM @Diagnostics
    ORDER BY PrincipalRole COLLATE Latin1_General_100_BIN2,
             PrincipalName COLLATE Latin1_General_100_BIN2,
             Securable COLLATE Latin1_General_100_BIN2,
             PermissionName COLLATE Latin1_General_100_BIN2;
