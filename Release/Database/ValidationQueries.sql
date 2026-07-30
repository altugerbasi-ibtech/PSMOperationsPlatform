/*
PSM Operations Database deployment validation queries.
Read-only: SELECT statements and session-scoped settings only.
Run while connected to the approved Operations Database.
*/

SET NOCOUNT ON;

-- Server and database configuration.
SELECT
    CAST(SERVERPROPERTY(N'ProductVersion') AS nvarchar(128)) AS ProductVersion,
    CAST(SERVERPROPERTY(N'ProductLevel') AS nvarchar(128)) AS ProductLevel,
    CAST(SERVERPROPERTY(N'Edition') AS nvarchar(128)) AS Edition,
    CAST(SERVERPROPERTY(N'EngineEdition') AS int) AS EngineEdition;

SELECT
    d.name AS DatabaseName,
    d.state_desc AS DatabaseState,
    d.compatibility_level AS CompatibilityLevel,
    d.collation_name AS Collation,
    d.recovery_model_desc AS RecoveryModel,
    d.user_access_desc AS UserAccess
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

-- Current execution identity and metadata visibility.
SELECT
    ORIGINAL_LOGIN() AS OriginalLogin,
    SUSER_SNAME() AS LoginName,
    USER_NAME() AS DatabaseUser,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONNECT') AS HasConnect,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DEFINITION') AS HasViewDefinition,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER') AS HasDatabaseAlter,
    IS_ROLEMEMBER(N'db_owner') AS IsDatabaseOwnerRoleMember,
    IS_ROLEMEMBER(N'db_ddladmin') AS IsDdlAdminRoleMember;

-- Applied EF Core schema versions, newest first.
SELECT
    h.MigrationId,
    h.ProductVersion
FROM dbo.__EFMigrationsHistory AS h
ORDER BY h.MigrationId DESC;

-- Latest applied schema version.
SELECT
    MAX(h.MigrationId) AS LatestMigrationId,
    COUNT_BIG(*) AS AppliedMigrationCount
FROM dbo.__EFMigrationsHistory AS h;

-- User object inventory by schema and type.
SELECT
    s.name AS SchemaName,
    o.type_desc AS ObjectType,
    COUNT_BIG(*) AS ObjectCount
FROM sys.objects AS o
INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0
GROUP BY s.name, o.type_desc
ORDER BY s.name, o.type_desc;

-- User tables and approximate row counts for deployment comparison.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    SUM(p.rows) AS ApproximateRowCount
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.partitions AS p
    ON p.object_id = t.object_id
   AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name
ORDER BY s.name, t.name;

-- Disabled or untrusted relational constraints require DBA review.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    fk.name AS ForeignKeyName,
    fk.is_disabled AS IsDisabled,
    fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_keys AS fk
INNER JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE fk.is_disabled = 1 OR fk.is_not_trusted = 1
ORDER BY s.name, t.name, fk.name;

-- Disabled user indexes require DBA review.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName
FROM sys.indexes AS i
INNER JOIN sys.tables AS t ON t.object_id = i.object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE i.is_disabled = 1
  AND i.name IS NOT NULL
  AND t.is_ms_shipped = 0
ORDER BY s.name, t.name, i.name;

-- Effective database permissions visible to the current identity.
SELECT
    p.permission_name AS PermissionName
FROM sys.fn_my_permissions(NULL, N'DATABASE') AS p
ORDER BY p.permission_name;
