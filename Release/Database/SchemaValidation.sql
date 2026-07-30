:setvar ExpectedDatabaseName "PSMOperationsPlatform"
:setvar ExpectedCompatibilityLevel "160"
:setvar ExpectedCollation "__REQUIRED__"
:setvar ExpectedRecoveryModel "__REQUIRED__"
:setvar ExpectedSchemaVersion "20260729191745_WP0088ExecutionHistory"

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ExpectedDatabaseName sysname = N'$(ExpectedDatabaseName)';
DECLARE @ExpectedCompatibilityLevel int = TRY_CONVERT(int, N'$(ExpectedCompatibilityLevel)');
DECLARE @ExpectedCollation sysname = N'$(ExpectedCollation)';
DECLARE @ExpectedRecoveryModel nvarchar(60) = UPPER(N'$(ExpectedRecoveryModel)');
DECLARE @ExpectedSchemaVersion nvarchar(150) = N'$(ExpectedSchemaVersion)';

DECLARE @Diagnostics table
(
    Category nvarchar(40) NOT NULL,
    ObjectName nvarchar(776) NOT NULL,
    ExpectedValue nvarchar(2048) NULL,
    ActualValue nvarchar(2048) NULL,
    Diagnostic nvarchar(2048) NOT NULL
);

IF @ExpectedDatabaseName = N'' OR @ExpectedDatabaseName LIKE N'%[^0-9A-Za-z_.-]%'
    INSERT @Diagnostics VALUES
        (N'Configuration', N'ExpectedDatabaseName', N'non-empty safe identifier', @ExpectedDatabaseName,
         N'ExpectedDatabaseName is missing or contains unsupported characters.');

IF @ExpectedCompatibilityLevel IS NULL
    INSERT @Diagnostics VALUES
        (N'Configuration', N'ExpectedCompatibilityLevel', N'integer', N'$(ExpectedCompatibilityLevel)',
         N'ExpectedCompatibilityLevel is not an integer.');

IF @ExpectedCollation IN (N'', N'__REQUIRED__')
    INSERT @Diagnostics VALUES
        (N'Configuration', N'ExpectedCollation', N'DBA-approved collation', @ExpectedCollation,
         N'ExpectedCollation must be supplied explicitly.');

IF @ExpectedRecoveryModel NOT IN (N'FULL', N'SIMPLE', N'BULK_LOGGED')
    INSERT @Diagnostics VALUES
        (N'Configuration', N'ExpectedRecoveryModel', N'FULL, SIMPLE, or BULK_LOGGED', @ExpectedRecoveryModel,
         N'ExpectedRecoveryModel must be supplied explicitly.');

IF DB_ID(@ExpectedDatabaseName) IS NULL
    INSERT @Diagnostics VALUES
        (N'Database', @ExpectedDatabaseName, N'database exists', N'missing',
         N'The expected database does not exist or is not visible to the executing identity.');

IF DB_NAME() <> @ExpectedDatabaseName
    INSERT @Diagnostics VALUES
        (N'Database', N'DB_NAME()', @ExpectedDatabaseName, DB_NAME(),
         N'The validation session is connected to the wrong database.');

DECLARE @ActualCompatibilityLevel int;
DECLARE @ActualCollation sysname;
DECLARE @ActualRecoveryModel nvarchar(60);

SELECT
    @ActualCompatibilityLevel = compatibility_level,
    @ActualCollation = collation_name,
    @ActualRecoveryModel = recovery_model_desc
FROM sys.databases
WHERE name = @ExpectedDatabaseName;

IF @ActualCompatibilityLevel IS NOT NULL
   AND @ActualCompatibilityLevel <> @ExpectedCompatibilityLevel
    INSERT @Diagnostics VALUES
        (N'Database', N'CompatibilityLevel', CONVERT(nvarchar(20), @ExpectedCompatibilityLevel),
         CONVERT(nvarchar(20), @ActualCompatibilityLevel), N'Compatibility level does not match.');

IF @ActualCollation IS NOT NULL AND @ExpectedCollation <> N'__REQUIRED__'
   AND @ActualCollation COLLATE Latin1_General_100_BIN2 <> @ExpectedCollation COLLATE Latin1_General_100_BIN2
    INSERT @Diagnostics VALUES
        (N'Database', N'Collation', @ExpectedCollation, @ActualCollation, N'Database collation does not match.');

IF @ActualRecoveryModel IS NOT NULL
   AND @ExpectedRecoveryModel IN (N'FULL', N'SIMPLE', N'BULK_LOGGED')
   AND @ActualRecoveryModel <> @ExpectedRecoveryModel
    INSERT @Diagnostics VALUES
        (N'Database', N'RecoveryModel', @ExpectedRecoveryModel, @ActualRecoveryModel,
         N'Database recovery model does not match.');

DECLARE @ExpectedMigrations table (Ordinal int PRIMARY KEY, MigrationId nvarchar(150) UNIQUE);
INSERT @ExpectedMigrations VALUES
(1,N'20260726133749_InitialCreate'),
(2,N'20260727120000_AddManagedServerConnectivityEligibility'),
(3,N'20260727160000_AddManagedServerWinRmConfiguration'),
(4,N'20260727190000_AddManagedServerConnectivityState'),
(5,N'20260727230000_AddWindowsInventoryCurrentState'),
(6,N'20260728093000_WP0071CoreInventoryReliability'),
(7,N'20260728125759_WP0071ADurableInventoryRunCorrelation'),
(8,N'20260728132820_WP0072ComputerOperatingSystemBiosInventory'),
(9,N'20260728142340_WP0073ProcessorInventoryContract'),
(10,N'20260728161243_WP0074PhysicalDiskVolumeInventory'),
(11,N'20260728175613_WP0075NetworkAdapterIpv4Inventory'),
(12,N'20260728184309_WP0076WindowsPlatformDiscovery'),
(13,N'20260728191433_WP0081CapabilityEngine'),
(14,N'20260728195159_WP0082CollectorDecisionEngine'),
(15,N'20260728210750_WP0083ExecutionPlanEngine'),
(16,N'20260729041758_WP0084CollectorRuntime'),
(17,N'20260729191745_WP0088ExecutionHistory');

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
    INSERT @Diagnostics VALUES
        (N'Migration', N'dbo.__EFMigrationsHistory', N'table exists', N'missing',
         N'EF Core migration history table is missing.');
ELSE
BEGIN
    INSERT @Diagnostics
    SELECT N'Migration', e.MigrationId, N'applied', N'missing', N'Expected migration is not applied.'
    FROM @ExpectedMigrations e
    WHERE NOT EXISTS
        (SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId = e.MigrationId);

    INSERT @Diagnostics
    SELECT N'Migration', h.MigrationId, N'not present', N'applied', N'Unexpected migration is applied.'
    FROM dbo.__EFMigrationsHistory h
    WHERE NOT EXISTS
        (SELECT 1 FROM @ExpectedMigrations e WHERE e.MigrationId = h.MigrationId);

    IF NOT EXISTS
        (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = @ExpectedSchemaVersion)
        INSERT @Diagnostics VALUES
            (N'Migration', N'ExpectedSchemaVersion', @ExpectedSchemaVersion, N'not applied',
             N'The expected schema-version migration is not applied.');
END;

DECLARE @ExpectedTables table (SchemaName sysname, TableName sysname, PRIMARY KEY(SchemaName,TableName));
INSERT @ExpectedTables VALUES
(N'audit',N'AuditLog'),
(N'collection',N'CollectorNode'),(N'collection',N'CollectorRun'),
(N'configuration',N'ManagedServer'),
(N'history',N'ExecutionArtifactHistory'),(N'history',N'ExecutionAttemptHistory'),
(N'history',N'ExecutionPolicyHistory'),(N'history',N'ExecutionRunHistory'),
(N'history',N'ExecutionStateTransitionHistory'),(N'history',N'ExecutionStepHistory'),
(N'inventory',N'CollectorDecisionCapabilityReference'),(N'inventory',N'CollectorDecisionPlan'),
(N'inventory',N'CollectorStrategyDecision'),(N'inventory',N'ExecutionPlan'),
(N'inventory',N'ExecutionPlanExclusion'),(N'inventory',N'ExecutionPlanExclusionCapability'),
(N'inventory',N'ExecutionPlanStep'),(N'inventory',N'InventorySnapshot'),
(N'inventory',N'WindowsBiosInventory'),(N'inventory',N'WindowsCapabilityEntry'),
(N'inventory',N'WindowsCapabilityProvenance'),(N'inventory',N'WindowsCapabilitySnapshot'),
(N'inventory',N'WindowsComputerInventory'),(N'inventory',N'WindowsDiskInventory'),
(N'inventory',N'WindowsDotNetPlatformInventory'),(N'inventory',N'WindowsFeatureInventory'),
(N'inventory',N'WindowsIisPlatformInventory'),(N'inventory',N'WindowsIpv4AddressInventory'),
(N'inventory',N'WindowsMemoryInventory'),(N'inventory',N'WindowsNetworkAdapterInventory'),
(N'inventory',N'WindowsOperatingSystemInventory'),(N'inventory',N'WindowsPowerShellPlatformInventory'),
(N'inventory',N'WindowsProcessorInventory'),(N'inventory',N'WindowsRoleInventory'),
(N'inventory',N'WindowsVolumeInventory'),(N'monitoring',N'CollectorHeartbeat'),
(N'operations',N'CommandQueueItem'),(N'runtime',N'ExecutionAttemptState'),
(N'runtime',N'ExecutionRunState'),(N'runtime',N'ExecutionStepState');

INSERT @Diagnostics
SELECT N'Table', QUOTENAME(e.SchemaName)+N'.'+QUOTENAME(e.TableName), N'exists', N'missing',
       N'Required table is missing.'
FROM @ExpectedTables e
WHERE OBJECT_ID(QUOTENAME(e.SchemaName)+N'.'+QUOTENAME(e.TableName), N'U') IS NULL;

DECLARE @ExpectedIndexes table
(SchemaName sysname, TableName sysname, IndexName sysname, PRIMARY KEY(SchemaName,TableName,IndexName));
INSERT @ExpectedIndexes VALUES
(N'configuration',N'ManagedServer',N'UX_ManagedServer_Fqdn'),
(N'inventory',N'ExecutionPlan',N'UX_ExecutionPlan_ManagedServer'),
(N'inventory',N'WindowsCapabilitySnapshot',N'UX_WindowsCapabilitySnapshot_ManagedServer'),
(N'inventory',N'CollectorDecisionPlan',N'UX_CollectorDecisionPlan_ManagedServer'),
(N'runtime',N'ExecutionRunState',N'IX_ExecutionRunState_ExecutionPlanId'),
(N'runtime',N'ExecutionStepState',N'IX_ExecutionStepState_ExecutionRunId_ExecutionPlanStepId'),
(N'runtime',N'ExecutionAttemptState',N'IX_ExecutionAttemptState_ExecutionStepStateId_AttemptNumber'),
(N'history',N'ExecutionRunHistory',N'IX_ExecutionRunHistory_ExecutionRunId'),
(N'history',N'ExecutionStepHistory',N'IX_ExecutionStepHistory_ExecutionRunId_ExecutionStepId'),
(N'history',N'ExecutionAttemptHistory',N'IX_ExecutionAttemptHistory_ExecutionRunId_ExecutionStepId_AttemptNumber');

INSERT @Diagnostics
SELECT N'Index', QUOTENAME(e.SchemaName)+N'.'+QUOTENAME(e.TableName)+N'.'+QUOTENAME(e.IndexName),
       N'exists and enabled', N'missing or disabled', N'Required index is missing or disabled.'
FROM @ExpectedIndexes e
WHERE NOT EXISTS
(
    SELECT 1 FROM sys.indexes i
    JOIN sys.tables t ON t.object_id=i.object_id
    JOIN sys.schemas s ON s.schema_id=t.schema_id
    WHERE s.name=e.SchemaName AND t.name=e.TableName AND i.name=e.IndexName AND i.is_disabled=0
);

DECLARE @ExpectedForeignKeys table
(SchemaName sysname, TableName sysname, ConstraintName sysname,
 PRIMARY KEY(SchemaName,TableName,ConstraintName));
INSERT @ExpectedForeignKeys VALUES
(N'collection',N'CollectorRun',N'FK_CollectorRun_ManagedServer_ManagedServerId'),
(N'runtime',N'ExecutionRunState',N'FK_ExecutionRunState_ExecutionPlan_ExecutionPlanId'),
(N'runtime',N'ExecutionStepState',N'FK_ExecutionStepState_ExecutionRunState_ExecutionRunId'),
(N'runtime',N'ExecutionAttemptState',N'FK_ExecutionAttemptState_ExecutionStepState_ExecutionStepStateId'),
(N'history',N'ExecutionStepHistory',N'FK_ExecutionStepHistory_ExecutionRunHistory_ExecutionRunId'),
(N'history',N'ExecutionAttemptHistory',N'FK_ExecutionAttemptHistory_ExecutionRunHistory_ExecutionRunId');

INSERT @Diagnostics
SELECT N'ForeignKey', QUOTENAME(e.SchemaName)+N'.'+QUOTENAME(e.TableName)+N'.'+QUOTENAME(e.ConstraintName),
       N'exists, enabled, trusted', N'missing, disabled, or untrusted', N'Required foreign key is invalid.'
FROM @ExpectedForeignKeys e
WHERE NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys f
    JOIN sys.tables t ON t.object_id=f.parent_object_id
    JOIN sys.schemas s ON s.schema_id=t.schema_id
    WHERE s.name=e.SchemaName AND t.name=e.TableName AND f.name=e.ConstraintName
      AND f.is_disabled=0 AND f.is_not_trusted=0
);

DECLARE @ExpectedPrimaryKeys table
(SchemaName sysname, TableName sysname, ConstraintName sysname,
 PRIMARY KEY(SchemaName,TableName,ConstraintName));
INSERT @ExpectedPrimaryKeys VALUES
(N'configuration',N'ManagedServer',N'PK_ManagedServer'),
(N'inventory',N'ExecutionPlan',N'PK_ExecutionPlan'),
(N'runtime',N'ExecutionRunState',N'PK_ExecutionRunState'),
(N'history',N'ExecutionRunHistory',N'PK_ExecutionRunHistory'),
(N'history',N'ExecutionStepHistory',N'PK_ExecutionStepHistory'),
(N'history',N'ExecutionAttemptHistory',N'PK_ExecutionAttemptHistory');

INSERT @Diagnostics
SELECT N'PrimaryKey',
       QUOTENAME(e.SchemaName)+N'.'+QUOTENAME(e.TableName)+N'.'+QUOTENAME(e.ConstraintName),
       N'exists', N'missing', N'Required primary key is missing.'
FROM @ExpectedPrimaryKeys e
WHERE NOT EXISTS
(
    SELECT 1 FROM sys.key_constraints k
    JOIN sys.tables t ON t.object_id=k.parent_object_id
    JOIN sys.schemas s ON s.schema_id=t.schema_id
    WHERE s.name=e.SchemaName AND t.name=e.TableName AND k.name=e.ConstraintName AND k.type=N'PK'
);

DECLARE @ExpectedUniqueConstraints table
(SchemaName sysname, TableName sysname, ConstraintName sysname,
 PRIMARY KEY(SchemaName,TableName,ConstraintName));
INSERT @ExpectedUniqueConstraints VALUES
(N'inventory',N'WindowsNetworkAdapterInventory',N'AK_WindowsNetworkAdapterInventory_Id_ManagedServerId');

INSERT @Diagnostics
SELECT N'UniqueConstraint',
       QUOTENAME(e.SchemaName)+N'.'+QUOTENAME(e.TableName)+N'.'+QUOTENAME(e.ConstraintName),
       N'exists', N'missing', N'Required unique constraint is missing.'
FROM @ExpectedUniqueConstraints e
WHERE NOT EXISTS
(
    SELECT 1 FROM sys.key_constraints k
    JOIN sys.tables t ON t.object_id=k.parent_object_id
    JOIN sys.schemas s ON s.schema_id=t.schema_id
    WHERE s.name=e.SchemaName AND t.name=e.TableName AND k.name=e.ConstraintName AND k.type=N'UQ'
);

DECLARE @ExpectedDefaults table
(SchemaName sysname, TableName sysname, ColumnName sysname,
 PRIMARY KEY(SchemaName,TableName,ColumnName));
INSERT @ExpectedDefaults VALUES
(N'configuration',N'ManagedServer',N'ConsecutiveInventoryFailures'),
(N'configuration',N'ManagedServer',N'InventoryVersion'),
(N'inventory',N'WindowsMemoryInventory',N'Id'),
(N'inventory',N'WindowsMemoryInventory',N'ModuleKey');

INSERT @Diagnostics
SELECT N'DefaultConstraint',
       QUOTENAME(e.SchemaName)+N'.'+QUOTENAME(e.TableName)+N'.'+QUOTENAME(e.ColumnName),
       N'exists', N'missing', N'Required default constraint is missing.'
FROM @ExpectedDefaults e
WHERE NOT EXISTS
(
    SELECT 1 FROM sys.default_constraints d
    JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id
    JOIN sys.tables t ON t.object_id=d.parent_object_id
    JOIN sys.schemas s ON s.schema_id=t.schema_id
    WHERE s.name=e.SchemaName AND t.name=e.TableName AND c.name=e.ColumnName
);

SELECT CASE WHEN EXISTS(SELECT 1 FROM @Diagnostics) THEN N'FAIL' ELSE N'PASS' END AS OverallResult;

IF EXISTS(SELECT 1 FROM @Diagnostics)
    SELECT Category, ObjectName, ExpectedValue, ActualValue, Diagnostic
    FROM @Diagnostics
    ORDER BY Category COLLATE Latin1_General_100_BIN2,
             ObjectName COLLATE Latin1_General_100_BIN2;
