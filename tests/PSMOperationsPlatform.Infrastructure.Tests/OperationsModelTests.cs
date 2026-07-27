using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class OperationsModelTests
{
    private static readonly IReadOnlyDictionary<Type, (string Schema, string Table)> Mappings =
        new Dictionary<Type, (string, string)>
        {
            [typeof(ManagedServer)] = ("configuration", "ManagedServer"),
            [typeof(CollectorNode)] = ("collection", "CollectorNode"),
            [typeof(CollectorHeartbeat)] = ("monitoring", "CollectorHeartbeat"),
            [typeof(CollectorRun)] = ("collection", "CollectorRun"),
            [typeof(InventorySnapshot)] = ("inventory", "InventorySnapshot"),
            [typeof(CommandQueueItem)] = ("operations", "CommandQueueItem"),
            [typeof(AuditLog)] = ("audit", "AuditLog")
        };

    private readonly IModel model = CreateContext()
        .GetService<IDesignTimeModel>()
        .Model;

    [Fact]
    public void ModelContainsApprovedTablesSchemasAndApplicationGeneratedKeys()
    {
        foreach ((Type clrType, (string schema, string table)) in Mappings)
        {
            IEntityType entityType = AssertEntity(clrType);
            Assert.Equal(schema, entityType.GetSchema());
            Assert.Equal(table, entityType.GetTableName());
            IKey key = Assert.Single(entityType.GetKeys());
            IProperty id = Assert.Single(key.Properties);
            Assert.Equal(nameof(ManagedServer.Id), id.Name);
            Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        }
    }

    [Fact]
    public void AllRelationshipsUseRestrictiveDeleteBehavior()
    {
        IForeignKey[] foreignKeys = model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .ToArray();

        Assert.Equal(6, foreignKeys.Length);
        Assert.All(foreignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Theory]
    [InlineData(typeof(ManagedServer), "UX_ManagedServer_Fqdn")]
    [InlineData(typeof(CollectorNode), "UX_CollectorNode_Registration")]
    [InlineData(typeof(InventorySnapshot), "UX_InventorySnapshot_RunContract")]
    public void ApprovedUniqueIndexesAreConfigured(Type entityType, string indexName)
    {
        IIndex index = Assert.Single(
            AssertEntity(entityType).GetIndexes(),
            candidate => candidate.GetDatabaseName() == indexName);

        Assert.True(index.IsUnique);
    }

    [Theory]
    [InlineData(typeof(CollectorNode))]
    [InlineData(typeof(CommandQueueItem))]
    public void CoordinationEntitiesUseSqlServerRowVersion(Type entityType)
    {
        IProperty property = AssertEntity(entityType).FindProperty("RowVersion")!;

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        Assert.Equal("rowversion", property.GetColumnType());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void AllDateTimePropertiesUseMillisecondPrecision()
    {
        IProperty[] dateProperties = model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property =>
                property.ClrType == typeof(DateTime)
                || property.ClrType == typeof(DateTime?))
            .ToArray();

        Assert.NotEmpty(dateProperties);
        Assert.All(dateProperties, property => Assert.Equal("datetime2(3)", property.GetColumnType()));
    }

    [Fact]
    public void JsonAndRangeCheckConstraintsAreConfigured()
    {
        string[] names = model.GetEntityTypes()
            .SelectMany(entity => entity.GetCheckConstraints())
            .Select(constraint => constraint.Name!)
            .Order()
            .ToArray();

        Assert.Equal(
            new[]
            {
                "CK_AuditLog_DetailJson_IsJson",
                "CK_CommandQueueItem_PayloadJson_IsJson",
                "CK_CommandQueueItem_Priority_NonNegative",
                "CK_InventorySnapshot_PayloadJson_IsJson",
                "CK_InventorySnapshot_SchemaVersion_Positive",
                "CK_ManagedServer_ConnectivityFailures_NonNegative",
                "CK_ManagedServer_LastConnectivityFailureCategory",
                "CK_ManagedServer_LastConnectivityState",
                "CK_ManagedServer_LastSuccessfulTransport",
                "CK_ManagedServer_WinRmHttpPort_Range",
                "CK_ManagedServer_WinRmHttpsPort_Range",
                "CK_ManagedServer_WinRmProbeTimeout_Positive",
                "CK_ManagedServer_WinRmTransportMode"
            },
            names);
    }

    [Fact]
    public void QueryIndexesHaveApprovedNamesAndDescendingSegments()
    {
        Dictionary<string, IReadOnlyList<bool>> expected = new()
        {
            ["IX_CollectorHeartbeat_Collector_ObservedAt"] = [false, true],
            ["IX_CollectorRun_Server_CreatedAt"] = [false, true],
            ["IX_CollectorRun_Collector_Status_CreatedAt"] = [false, false, false],
            ["IX_InventorySnapshot_Server_Type_CapturedAt"] = [false, false, true],
            ["IX_CommandQueue_Status_Target_Priority"] = [false, false, false, false, false],
            ["IX_AuditLog_OccurredAt"] = [true],
            ["IX_AuditLog_Entity"] = [false, false, true],
            ["IX_AuditLog_CorrelationId"] = [false]
        };

        Dictionary<string, IReadOnlyList<bool>> actual = model.GetEntityTypes()
            .SelectMany(entity => entity.GetIndexes())
            .Where(index => expected.ContainsKey(index.GetDatabaseName()!))
            .ToDictionary(
                index => index.GetDatabaseName()!,
                index => index.IsDescending
                    is { Count: > 0 } directions
                        ? directions
                        : index.IsDescending is not null
                            ? Enumerable.Repeat(true, index.Properties.Count).ToArray()
                            : Enumerable.Repeat(false, index.Properties.Count).ToArray());

        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach ((string name, IReadOnlyList<bool> directions) in expected)
        {
            Assert.Equal(directions, actual[name]);
        }
    }

    [Fact]
    public void ManagedServerEligibilityUsesNullableMillisecondTimeAndQueryIndex()
    {
        IEntityType managedServer = AssertEntity(typeof(ManagedServer));
        IProperty nextAttempt =
            managedServer.FindProperty(nameof(ManagedServer.NextConnectivityAttemptAt))!;
        IIndex eligibilityIndex = Assert.Single(
            managedServer.GetIndexes(),
            index =>
                index.GetDatabaseName() == "IX_ManagedServer_Eligibility");

        Assert.True(nextAttempt.IsNullable);
        Assert.Equal("datetime2(3)", nextAttempt.GetColumnType());
        Assert.Equal(
            new[]
            {
                nameof(ManagedServer.IsEnabled),
                nameof(ManagedServer.NextConnectivityAttemptAt),
            },
            eligibilityIndex.Properties.Select(property => property.Name));
    }

    [Fact]
    public void ManagedServerWinRmConfigurationUsesRequiredDeterministicMappings()
    {
        IEntityType managedServer = AssertEntity(typeof(ManagedServer));

        Assert.Equal(
            typeof(string),
            managedServer.FindProperty(nameof(ManagedServer.WinRmTransportMode))!
                .GetProviderClrType());
        Assert.False(
            managedServer.FindProperty(nameof(ManagedServer.WinRmTransportMode))!
                .IsNullable);
        Assert.All(
            new[]
            {
                nameof(ManagedServer.WinRmHttpsPort),
                nameof(ManagedServer.WinRmHttpPort),
                nameof(ManagedServer.WinRmProbeTimeoutSeconds),
            },
            propertyName =>
                Assert.False(managedServer.FindProperty(propertyName)!.IsNullable));
    }

    [Fact]
    public void ManagedServerConnectivityStateUsesSafeMappingsAndConcurrency()
    {
        IEntityType managedServer = AssertEntity(typeof(ManagedServer));

        Assert.Equal(
            typeof(string),
            managedServer.FindProperty(nameof(ManagedServer.LastConnectivityState))!
                .GetProviderClrType());
        Assert.Equal(
            "datetime2(3)",
            managedServer.FindProperty(nameof(ManagedServer.LastConnectivityAttemptAt))!
                .GetColumnType());
        Assert.Equal(
            "datetime2(3)",
            managedServer.FindProperty(nameof(ManagedServer.LastConnectivitySuccessAt))!
                .GetColumnType());
        Assert.True(
            managedServer.FindProperty(nameof(ManagedServer.LastSuccessfulTransport))!
                .IsNullable);
        Assert.True(
            managedServer.FindProperty(
                nameof(ManagedServer.LastConnectivityFailureCategory))!
                .IsNullable);

        IProperty rowVersion =
            managedServer.FindProperty(nameof(ManagedServer.RowVersion))!;
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.Equal("rowversion", rowVersion.GetColumnType());
    }

    private IEntityType AssertEntity(Type clrType) =>
        model.FindEntityType(clrType)
        ?? throw new Xunit.Sdk.XunitException($"Entity {clrType.Name} is missing.");

    private static OperationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MetadataOnly;Integrated Security=true")
            .Options;
        return new OperationsDbContext(options);
    }
}
