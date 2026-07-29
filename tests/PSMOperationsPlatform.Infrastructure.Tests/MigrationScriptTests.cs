using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class MigrationScriptTests
{
    [Fact]
    public void InitialCreateGeneratesApprovedSqlServerObjects()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ScriptOnly;Integrated Security=true")
            .Options;
        using var context = new OperationsDbContext(options);

        string script = context.GetService<IMigrator>().GenerateScript();

        foreach (string schema in new[]
                 {
                     "configuration",
                     "collection",
                     "monitoring",
                     "inventory",
                     "operations",
                     "audit"
                 })
        {
            Assert.Contains($"CREATE SCHEMA [{schema}]", script, StringComparison.Ordinal);
        }

        foreach (string table in new[]
                 {
                     "[configuration].[ManagedServer]",
                     "[collection].[CollectorNode]",
                     "[monitoring].[CollectorHeartbeat]",
                     "[collection].[CollectorRun]",
                     "[inventory].[InventorySnapshot]",
                     "[operations].[CommandQueueItem]",
                     "[audit].[AuditLog]"
                 })
        {
            Assert.Equal(1, CountOccurrences(script, $"CREATE TABLE {table}"));
        }
        Assert.Contains("ISJSON([PayloadJson]) = 1", script, StringComparison.Ordinal);
        Assert.Contains("[RowVersion] rowversion NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains(
            "ADD [NextConnectivityAttemptAt] datetime2(3) NULL",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX [IX_ManagedServer_Eligibility]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "SET [WinRmTransportMode] = N''Auto''",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[WinRmHttpsPort] = 5986",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[WinRmHttpPort] = 5985",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[WinRmProbeTimeoutSeconds] = 10",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_ManagedServer_WinRmTransportMode",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "SET [LastConnectivityState] = N''Unknown''",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ConsecutiveConnectivityFailures] = 0",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "ADD [RowVersion] rowversion NOT NULL",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_ManagedServer_ConnectivityFailures_NonNegative",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_ManagedServer_LastConnectivityFailureCategory",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_ManagedServer_LastSuccessfulTransport",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ConnectivityHistory",
            script,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "RawException",
            script,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE LOGIN", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [configuration]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [collection]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [monitoring]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [inventory]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [operations]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [audit]", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdempotentScriptUsesBatchSafeBackfillsAndCorrectInventoryForeignKeys()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        int winRmBackfill = script.IndexOf(
            "SET [WinRmTransportMode] = N''Auto''",
            StringComparison.Ordinal);
        Assert.True(winRmBackfill >= 0);
        foreach (string column in new[]
                 {
                     "WinRmTransportMode",
                     "WinRmHttpsPort",
                     "WinRmHttpPort",
                     "WinRmProbeTimeoutSeconds"
                 })
        {
            int addColumn = script.LastIndexOf(
                $"ADD [{column}]",
                winRmBackfill,
                StringComparison.Ordinal);
            Assert.InRange(addColumn, 0, winRmBackfill - 1);
        }
        Assert.True(script.LastIndexOf(
            "EXEC(N'",
            winRmBackfill,
            StringComparison.Ordinal) >= 0);

        int connectivityBackfill = script.IndexOf(
            "SET [LastConnectivityState] = N''Unknown''",
            StringComparison.Ordinal);
        Assert.True(connectivityBackfill >= 0);
        foreach (string column in new[]
                 {
                     "LastConnectivityState",
                     "ConsecutiveConnectivityFailures"
                 })
        {
            int addColumn = script.LastIndexOf(
                $"ADD [{column}]",
                connectivityBackfill,
                StringComparison.Ordinal);
            Assert.InRange(addColumn, 0, connectivityBackfill - 1);
        }
        Assert.True(script.LastIndexOf(
            "EXEC(N'",
            connectivityBackfill,
            StringComparison.Ordinal) >= 0);

        Assert.DoesNotContain(
            "REFERENCES [Id].[configuration] ([ManagedServer])",
            script,
            StringComparison.Ordinal);

        foreach (string constraint in ManagedServerInventoryForeignKeys)
        {
            string definition = GetConstraintDefinition(script, constraint);
            Assert.Contains(
                "REFERENCES [configuration].[ManagedServer] ([Id])",
                definition,
                StringComparison.Ordinal);
        }

        string ipv4Definition = GetConstraintDefinition(
            script,
            "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId");
        Assert.Contains(
            "REFERENCES [inventory].[WindowsNetworkAdapterInventory] ([Id])",
            ipv4Definition,
            StringComparison.Ordinal);

        int lastMigrationPosition = -1;
        foreach (string migrationId in MigrationIds)
        {
            int position = script.IndexOf(migrationId, StringComparison.Ordinal);
            Assert.True(position > lastMigrationPosition);
            lastMigrationPosition = position;
        }

        Assert.Contains("__EFMigrationsHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Server=", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User ID=", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UID=", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PWD=", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wp0071IdempotentSqlDefersNewSchemaReferencesAndRetainsGuards()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        const string migrationId = "20260728093000_WP0071CoreInventoryReliability";
        int migrationStart = script.IndexOf(migrationId, StringComparison.Ordinal);
        Assert.True(migrationStart >= 0);
        string wp0071Script = script[migrationStart..];

        int memoryBackfill = wp0071Script.IndexOf(
            "UPDATE [inventory].[WindowsMemoryInventory]",
            StringComparison.Ordinal);
        Assert.True(memoryBackfill >= 0);
        int memoryBackfillExec = wp0071Script.LastIndexOf(
            "EXEC(N'",
            memoryBackfill,
            StringComparison.Ordinal);
        Assert.InRange(memoryBackfillExec, 0, memoryBackfill - 1);
        Assert.Contains(
            "[ModuleKey] = N''legacy-total'';",
            wp0071Script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "[ModuleKey] = N'legacy-total';",
            wp0071Script,
            StringComparison.Ordinal);
        int memoryPrimaryKey = wp0071Script.IndexOf(
            "ADD CONSTRAINT [PK_WindowsMemoryInventory]",
            StringComparison.Ordinal);
        Assert.True(memoryPrimaryKey > memoryBackfill);
        int memoryPrimaryKeyExec = wp0071Script.LastIndexOf(
            "EXEC(N'",
            memoryPrimaryKey,
            StringComparison.Ordinal);
        Assert.InRange(memoryPrimaryKeyExec, memoryBackfill + 1, memoryPrimaryKey - 1);

        foreach (string operation in Wp0071Operations)
        {
            Assert.Contains(operation, wp0071Script, StringComparison.Ordinal);
        }

        Assert.True(
            CountOccurrences(
                wp0071Script,
                "IF NOT EXISTS")
            >= Wp0071Operations.Length);
        Assert.Contains(
            $"VALUES (N'{migrationId}',",
            wp0071Script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ModelSnapshotMatchesTheCurrentModel()
    {
        using OperationsDbContext context = CreateContext();
        IModel snapshot = context.GetService<IMigrationsAssembly>()
            .ModelSnapshot!
            .Model;
        snapshot = context.GetService<IModelRuntimeInitializer>()
            .Initialize(snapshot, designTime: true);
        IModel current = context.GetService<IDesignTimeModel>().Model;
        IMigrationsModelDiffer differ = context.GetService<IMigrationsModelDiffer>();

        Assert.False(differ.HasDifferences(
            snapshot.GetRelationalModel(),
            current.GetRelationalModel()));
    }

    [Fact]
    public void Wp0071AIdempotentSqlUsesPerServerLegacyMappingAndDeferredSchemaReferences()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        const string migrationId = "20260728125759_WP0071ADurableInventoryRunCorrelation";
        int migrationStart = script.IndexOf(migrationId, StringComparison.Ordinal);
        Assert.True(migrationStart >= 0);
        int nextMigration = script.IndexOf(
            "20260728132820_WP0072ComputerOperatingSystemBiosInventory",
            migrationStart,
            StringComparison.Ordinal);
        string section = script[migrationStart..nextMigration];

        Assert.Contains("CREATE TABLE #LegacyInventoryRun", section, StringComparison.Ordinal);
        Assert.Contains("SELECT ManagedServerId, NEWID()", section, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE #LegacyInventoryRun", section, StringComparison.Ordinal);
        Assert.Contains("InventoryRunId IS NULL", section, StringComparison.Ordinal);
        Assert.Contains("THROW 51000", section, StringComparison.Ordinal);
        Assert.DoesNotContain("DEFAULT '00000000-0000-0000-0000-000000000000'", section, StringComparison.Ordinal);

        foreach (string table in CoreInventoryTables)
        {
            Assert.Contains($"ALTER TABLE [inventory].[{table}] ADD [InventoryRunId] uniqueidentifier NULL", section, StringComparison.Ordinal);
            Assert.Contains($"FROM [inventory].[{table}] AS target", section, StringComparison.Ordinal);
            Assert.Contains($"ALTER TABLE [inventory].[{table}] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL", section, StringComparison.Ordinal);
            Assert.Contains($"IX_{table}_ManagedServer_InventoryRun", section, StringComparison.Ordinal);
        }
        Assert.True(section.LastIndexOf("EXEC(N'", StringComparison.Ordinal)
            < section.IndexOf("InventoryRunId IS NULL", StringComparison.Ordinal));
        Assert.Contains($"VALUES (N'{migrationId}',", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Wp0072IdempotentSqlContainsBiosSchemaAndDefersItsNewIndex()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        const string migrationId = "20260728132820_WP0072ComputerOperatingSystemBiosInventory";
        int migrationStart = script.IndexOf(migrationId, StringComparison.Ordinal);
        Assert.True(migrationStart >= 0);
        string section = script[migrationStart..];

        Assert.Contains("CREATE TABLE [inventory].[WindowsBiosInventory]", section, StringComparison.Ordinal);
        Assert.Contains("[InventoryRunId] uniqueidentifier NOT NULL", section, StringComparison.Ordinal);
        Assert.Contains("FK_WindowsBiosInventory_ManagedServer_ManagedServerId", section, StringComparison.Ordinal);
        Assert.Contains("EXEC(N'", section, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX [IX_WindowsBiosInventory_ManagedServer_InventoryRun]", section, StringComparison.Ordinal);
        Assert.Contains($"VALUES (N'{migrationId}',", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Wp0073IdempotentSqlContainsForwardProcessorContract()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        const string migrationId = "20260728142340_WP0073ProcessorInventoryContract";
        int migrationStart = script.IndexOf(migrationId, StringComparison.Ordinal);
        Assert.True(migrationStart >= 0);
        string section = script[migrationStart..];

        Assert.Contains("sp_rename N'[inventory].[WindowsProcessorInventory].[StableSourceKey]'", section, StringComparison.Ordinal);
        Assert.Contains("N'ProcessorKey'", section, StringComparison.Ordinal);
        Assert.Contains("UX_WindowsProcessorInventory_ManagedServer_ProcessorKey", section, StringComparison.Ordinal);
        Assert.Contains("[RowVersion] rowversion NOT NULL", section, StringComparison.Ordinal);
        Assert.Contains("[VirtualizationFirmwareEnabled] bit NULL", section, StringComparison.Ordinal);
        Assert.Contains("[InventoryRunId]", script, StringComparison.Ordinal);
        Assert.Contains($"VALUES (N'{migrationId}',", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Wp0074IdempotentSqlContainsForwardStorageKeysAndGuards()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        const string migrationId = "20260728161243_WP0074PhysicalDiskVolumeInventory";
        int migrationStart = script.IndexOf(migrationId, StringComparison.Ordinal);
        Assert.True(migrationStart >= 0);
        string section = script[migrationStart..];

        Assert.Contains("sp_rename N'[inventory].[WindowsDiskInventory].[StableSourceKey]'", section, StringComparison.Ordinal);
        Assert.Contains("sp_rename N'[inventory].[WindowsVolumeInventory].[StableSourceKey]'", section, StringComparison.Ordinal);
        Assert.Contains("N'DiskKey'", section, StringComparison.Ordinal);
        Assert.Contains("N'VolumeKey'", section, StringComparison.Ordinal);
        Assert.Contains("UX_WindowsDiskInventory_ManagedServer_DiskKey", section, StringComparison.Ordinal);
        Assert.Contains("UX_WindowsVolumeInventory_ManagedServer_VolumeKey", section, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS (", script, StringComparison.Ordinal);
        Assert.Contains($"VALUES (N'{migrationId}',", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Wp0075IdempotentSqlContainsForwardNetworkContractAndDeferredBackfill()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        const string migrationId = "20260728175613_WP0075NetworkAdapterIpv4Inventory";
        int migrationStart = script.IndexOf(migrationId, StringComparison.Ordinal);
        Assert.True(migrationStart >= 0);
        string section = script[migrationStart..];

        Assert.Contains("sp_rename N'[inventory].[WindowsNetworkAdapterInventory].[StableSourceKey]'", section, StringComparison.Ordinal);
        Assert.Contains("sp_rename N'[inventory].[WindowsIpv4AddressInventory].[StableSourceKey]'", section, StringComparison.Ordinal);
        Assert.Contains("N'AdapterKey'", section, StringComparison.Ordinal);
        Assert.Contains("N'Ipv4Key'", section, StringComparison.Ordinal);
        Assert.Contains("EXEC(N'", section, StringComparison.Ordinal);
        Assert.Contains("SET [AdapterKey] = [adapter].[AdapterKey]", section, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN [AdapterKey] nvarchar(200) NOT NULL", section, StringComparison.Ordinal);
        Assert.Contains("[RowVersion] rowversion NOT NULL", section, StringComparison.Ordinal);
        Assert.Contains("UX_WindowsNetworkAdapterInventory_ManagedServer_AdapterKey", section, StringComparison.Ordinal);
        Assert.Contains("UX_WindowsIpv4AddressInventory_ManagedServer_Ipv4Key", section, StringComparison.Ordinal);
        Assert.Contains($"VALUES (N'{migrationId}',", section, StringComparison.Ordinal);
    }

    [Fact]
    public void WP0083ExecutionPlanMigrationHasCurrentStateConstraintsAndNoRuntimeState()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            "20260728195159_WP0082CollectorDecisionEngine",
            "20260728210750_WP0083ExecutionPlanEngine",
            MigrationsSqlGenerationOptions.Idempotent);

        foreach (string table in new[]
                 {
                     "ExecutionPlan",
                     "ExecutionPlanStep",
                     "ExecutionPlanExclusion",
                     "ExecutionPlanExclusionCapability"
                 })
            Assert.Contains($"CREATE TABLE [inventory].[{table}]", script, StringComparison.Ordinal);

        foreach (string value in new[]
                 {
                     "UX_ExecutionPlan_ManagedServer",
                     "IX_ExecutionPlan_DecisionPlanId",
                     "IX_ExecutionPlan_CapabilitySnapshotId",
                     "IX_ExecutionPlan_SourceInventoryRunId",
                     "IX_ExecutionPlanStep_ExecutionPlanId_StrategyCode",
                     "IX_ExecutionPlanExclusion_ExecutionPlanId_StrategyCode",
                     "CK_ExecutionPlanStep_PolicyVersions",
                     "CK_ExecutionPlanStep_PositiveValues",
                     "CK_ExecutionPlanStep_TimeoutBound",
                     "CK_ExecutionPlanStep_ReadOnly"
                 })
            Assert.Contains(value, script, StringComparison.Ordinal);

        Assert.DoesNotContain("[CurrentAttempt]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[RemainingRetries]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[StartedAt]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompletedAt]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionState", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WP0084CollectorRuntimeMigrationHasVersionedCurrentStateAndNoHistory()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            "20260728210750_WP0083ExecutionPlanEngine",
            "20260729041758_WP0084CollectorRuntime",
            MigrationsSqlGenerationOptions.Idempotent);

        foreach (string table in new[]
                 {
                     "ExecutionRunState", "ExecutionStepState", "ExecutionAttemptState"
                 })
            Assert.Contains($"CREATE TABLE [runtime].[{table}]", script, StringComparison.Ordinal);
        foreach (string value in new[]
                 {
                     "IX_ExecutionRunState_ExecutionPlanId",
                     "IX_ExecutionRunState_ManagedServerId_SourceInventoryRunId",
                     "IX_ExecutionStepState_ExecutionRunId_ExecutionPlanStepId",
                     "IX_ExecutionAttemptState_ExecutionStepStateId_AttemptNumber",
                     "CK_ExecutionRunState_Versions", "CK_ExecutionRunState_Metrics",
                     "CK_ExecutionStepState_Metrics", "CK_ExecutionAttemptState_Metrics",
                     "[RowVersion] rowversion NOT NULL"
                 })
            Assert.Contains(value, script, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionEventHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RemainingRetries", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WP0088ExecutionHistoryMigrationIsNormalizedIdempotentAndBounded()
    {
        using OperationsDbContext context = CreateContext();
        string script = context.GetService<IMigrator>().GenerateScript(
            "20260729041758_WP0084CollectorRuntime",
            "20260729191745_WP0088ExecutionHistory",
            MigrationsSqlGenerationOptions.Idempotent);

        foreach (string table in new[]
                 {
                     "ExecutionRunHistory", "ExecutionStepHistory",
                     "ExecutionAttemptHistory", "ExecutionStateTransitionHistory",
                     "ExecutionArtifactHistory", "ExecutionPolicyHistory"
                 })
            Assert.Contains($"CREATE TABLE [history].[{table}]", script,
                StringComparison.Ordinal);
        foreach (string value in new[]
                 {
                     "CK_ExecutionRunHistory_Versions",
                     "CK_ExecutionRunHistory_Counts",
                     "IX_ExecutionRunHistory_ExecutionRunId",
                     "IX_ExecutionStepHistory_ExecutionRunId_ExecutionStepId",
                     "IX_ExecutionAttemptHistory_ExecutionRunId_ExecutionStepId_AttemptNumber",
                     "IX_ExecutionArtifactHistory_ExecutionRunId_ExecutionStepId_ArtifactId",
                     "IX_ExecutionPolicyHistory_ExecutionRunId_ExecutionStepId"
                 })
            Assert.Contains(value, script, StringComparison.Ordinal);
        Assert.Contains("__EFMigrationsHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionEventHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("MonitoringHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("nvarchar(max)", script, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] MigrationIds =
    [
        "20260726133749_InitialCreate",
        "20260727120000_AddManagedServerConnectivityEligibility",
        "20260727160000_AddManagedServerWinRmConfiguration",
        "20260727190000_AddManagedServerConnectivityState",
        "20260727230000_AddWindowsInventoryCurrentState",
        "20260728093000_WP0071CoreInventoryReliability",
        "20260728125759_WP0071ADurableInventoryRunCorrelation",
        "20260728132820_WP0072ComputerOperatingSystemBiosInventory",
        "20260728142340_WP0073ProcessorInventoryContract",
        "20260728161243_WP0074PhysicalDiskVolumeInventory",
        "20260728175613_WP0075NetworkAdapterIpv4Inventory",
        "20260728184309_WP0076WindowsPlatformDiscovery",
        "20260728191433_WP0081CapabilityEngine",
        "20260728195159_WP0082CollectorDecisionEngine",
        "20260728210750_WP0083ExecutionPlanEngine",
        "20260729041758_WP0084CollectorRuntime",
        "20260729191745_WP0088ExecutionHistory"
    ];

    private static readonly string[] CoreInventoryTables =
    [
        "WindowsComputerInventory",
        "WindowsOperatingSystemInventory",
        "WindowsProcessorInventory",
        "WindowsMemoryInventory",
        "WindowsDiskInventory",
        "WindowsVolumeInventory",
        "WindowsNetworkAdapterInventory",
        "WindowsIpv4AddressInventory"
    ];

    private static readonly string[] Wp0071Operations =
    [
        "CK_ManagedServer_InventoryFailures_NonNegative",
        "CK_ManagedServer_InventoryVersion_NonNegative",
        "CK_ManagedServer_LastInventoryFailureCategory",
        "IX_ManagedServer_InventoryEligibility",
        "PK_WindowsMemoryInventory",
        "UX_WindowsMemoryInventory_ManagedServer_ModuleKey",
        "CK_WindowsMemoryInventory_CapacityBytes_Positive",
        "AK_WindowsNetworkAdapterInventory_Id_ManagedServerId",
        "IX_WindowsIpv4AddressInventory_NetworkAdapterInventoryId_ManagedServerId",
        "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId_ManagedServerId"
    ];

    private static readonly string[] ManagedServerInventoryForeignKeys =
    [
        "FK_WindowsComputerInventory_ManagedServer_ManagedServerId",
        "FK_WindowsOperatingSystemInventory_ManagedServer_ManagedServerId",
        "FK_WindowsMemoryInventory_ManagedServer_ManagedServerId",
        "FK_WindowsProcessorInventory_ManagedServer_ManagedServerId",
        "FK_WindowsDiskInventory_ManagedServer_ManagedServerId",
        "FK_WindowsVolumeInventory_ManagedServer_ManagedServerId",
        "FK_WindowsNetworkAdapterInventory_ManagedServer_ManagedServerId",
        "FK_WindowsIpv4AddressInventory_ManagedServer_ManagedServerId"
    ];

    private static OperationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=ScriptOnly;Integrated Security=true")
            .Options;

        return new OperationsDbContext(options);
    }

    private static string GetConstraintDefinition(string script, string constraint)
    {
        int start = script.IndexOf(
            $"CONSTRAINT [{constraint}]",
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        int end = script.IndexOf(
            "ON DELETE",
            start,
            StringComparison.Ordinal);
        Assert.True(end > start);
        return script[start..end];
    }

    private static int CountOccurrences(string value, string fragment)
    {
        int count = 0;
        int position = 0;
        while ((position = value.IndexOf(fragment, position, StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += fragment.Length;
        }

        return count;
    }
}
