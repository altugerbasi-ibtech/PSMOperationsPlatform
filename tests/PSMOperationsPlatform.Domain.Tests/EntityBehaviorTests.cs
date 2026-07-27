using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.Domain.Tests;

public sealed class EntityBehaviorTests
{
    [Fact]
    public void ManagedServerConnectivityTransitionsPreserveApprovedState()
    {
        DateTime createdAt = new(2026, 7, 27, 10, 0, 0);
        var server = new ManagedServer(
            Guid.NewGuid(),
            "server.example.invalid",
            createdAt);
        DateTime successAt = createdAt.AddMinutes(1);

        server.ApplyConnectivitySuccess(
            successAt,
            ConnectivityTransport.Http,
            successAt.AddMinutes(1));

        Assert.Equal(ConnectivityState.Reachable, server.LastConnectivityState);
        Assert.Equal(successAt, server.LastConnectivityAttemptAt);
        Assert.Equal(successAt, server.LastConnectivitySuccessAt);
        Assert.Equal(ConnectivityTransport.Http, server.LastSuccessfulTransport);
        Assert.Equal(0, server.ConsecutiveConnectivityFailures);
        Assert.Null(server.LastConnectivityFailureCategory);

        DateTime failureAt = successAt.AddMinutes(2);
        server.ApplyConnectivityFailure(
            failureAt,
            ConnectivityFailureCategory.Timeout,
            failureAt.AddMinutes(5));

        Assert.Equal(ConnectivityState.Unreachable, server.LastConnectivityState);
        Assert.Equal(failureAt, server.LastConnectivityAttemptAt);
        Assert.Equal(successAt, server.LastConnectivitySuccessAt);
        Assert.Equal(ConnectivityTransport.Http, server.LastSuccessfulTransport);
        Assert.Equal(1, server.ConsecutiveConnectivityFailures);
        Assert.Equal(
            ConnectivityFailureCategory.Timeout,
            server.LastConnectivityFailureCategory);
    }

    [Fact]
    public void EndpointAndEnablementChangesResetEligibilityWithoutErasingHistory()
    {
        DateTime createdAt = new(2026, 7, 27, 10, 0, 0);
        var server = new ManagedServer(
            Guid.NewGuid(),
            "server.example.invalid",
            createdAt);
        DateTime successAt = createdAt.AddMinutes(1);
        server.ApplyConnectivitySuccess(
            successAt,
            ConnectivityTransport.Https,
            successAt.AddMinutes(1));

        server.ChangeFqdn("new-server.example.invalid", successAt.AddMinutes(2));

        Assert.Equal(ConnectivityState.Unknown, server.LastConnectivityState);
        Assert.Null(server.NextConnectivityAttemptAt);
        Assert.Equal(successAt, server.LastConnectivitySuccessAt);
        Assert.Equal(
            ConnectivityTransport.Https,
            server.LastSuccessfulTransport);

        server.SetEnabled(false, successAt.AddMinutes(3));
        server.SetEnabled(true, successAt.AddMinutes(4));

        Assert.Equal(ConnectivityState.Unknown, server.LastConnectivityState);
        Assert.Equal(0, server.ConsecutiveConnectivityFailures);
        Assert.Null(server.NextConnectivityAttemptAt);
    }

    [Fact]
    public void ConnectivityFailureCountSaturatesAtIntegerMaximum()
    {
        DateTime createdAt = new(2026, 7, 27, 10, 0, 0);
        var server = new ManagedServer(
            Guid.NewGuid(),
            "server.example.invalid",
            createdAt);
        typeof(ManagedServer)
            .GetProperty(nameof(ManagedServer.ConsecutiveConnectivityFailures))!
            .SetValue(server, int.MaxValue);

        server.ApplyConnectivityFailure(
            createdAt.AddMinutes(1),
            ConnectivityFailureCategory.Timeout,
            createdAt.AddHours(1));

        Assert.Equal(int.MaxValue, server.ConsecutiveConnectivityFailures);
    }
    private static readonly DateTime CreatedAt = new(2026, 7, 26, 12, 0, 0);

    [Fact]
    public void ManagedServer_NormalizesFqdnAndProtectsCreationTime()
    {
        var server = new ManagedServer(Guid.NewGuid(), " APP01.Example.Local. ", CreatedAt);

        Assert.Equal("app01.example.local", server.Fqdn);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => server.SetEnabled(false, CreatedAt.AddMilliseconds(-1)));
    }

    [Fact]
    public void ManagedServerUsesApprovedWinRmDefaults()
    {
        var server = new ManagedServer(
            Guid.NewGuid(),
            "app01.example.local",
            CreatedAt);

        Assert.Equal(WinRmTransportMode.Auto, server.WinRmTransportMode);
        Assert.Equal(5986, server.WinRmHttpsPort);
        Assert.Equal(5985, server.WinRmHttpPort);
        Assert.Equal(10, server.WinRmProbeTimeoutSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void ManagedServerRejectsInvalidHttpsPort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ManagedServer(
                Guid.NewGuid(),
                "app01.example.local",
                CreatedAt,
                winRmHttpsPort: port));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void ManagedServerRejectsInvalidHttpPort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ManagedServer(
                Guid.NewGuid(),
                "app01.example.local",
                CreatedAt,
                winRmHttpPort: port));
    }

    [Fact]
    public void ManagedServerRejectsNonPositiveProbeTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ManagedServer(
                Guid.NewGuid(),
                "app01.example.local",
                CreatedAt,
                winRmProbeTimeoutSeconds: 0));
    }

    [Fact]
    public void ManagedServerRejectsUndefinedWinRmTransportMode()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ManagedServer(
                    Guid.NewGuid(),
                    "app01.example.local",
                    CreatedAt,
                    winRmTransportMode: (WinRmTransportMode)999));

        Assert.Equal("winRmTransportMode", exception.ParamName);
    }

    [Fact]
    public void ManagedServerConnectivityConfigurationContainsNoSecurityBypass()
    {
        string[] propertyNames = typeof(ManagedServer)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(
            propertyNames,
            name =>
                name.Contains("Credential", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || name.Contains("UserName", StringComparison.OrdinalIgnoreCase)
                || name.Contains("TrustedHosts", StringComparison.OrdinalIgnoreCase)
                || name.Contains("SkipCA", StringComparison.OrdinalIgnoreCase)
                || name.Contains("SkipCN", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Thumbprint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CollectorRun_RequiresValidStateTransitions()
    {
        var run = new CollectorRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CollectionType.Windows,
            CreatedAt);

        Assert.Throws<InvalidOperationException>(() => run.Succeed(CreatedAt));

        run.Start(CreatedAt.AddSeconds(1));
        run.Succeed(CreatedAt.AddSeconds(2));

        Assert.Equal(CollectorRunStatus.Succeeded, run.Status);
        Assert.NotNull(run.CompletedAt);
        Assert.Throws<InvalidOperationException>(() => run.Cancel(CreatedAt.AddSeconds(3)));
    }

    [Fact]
    public void CommandQueueItem_RejectsInvalidJsonAndNegativePriority()
    {
        Assert.Throws<ArgumentException>(
            () => CreateCommand(payloadJson: "{invalid"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateCommand(priority: -1));
    }

    [Fact]
    public void InventorySnapshot_RequiresPositiveVersionAndValidJson()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new InventorySnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Windows.Inventory.v1",
                0,
                CreatedAt,
                "{}"));
        Assert.Throws<ArgumentException>(
            () => new InventorySnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Windows.Inventory.v1",
                1,
                CreatedAt,
                "not-json"));
    }

    [Fact]
    public void AppendOnlyEntitiesExposeNoPublicSetters()
    {
        Type[] appendOnlyTypes =
        [
            typeof(CollectorHeartbeat),
            typeof(InventorySnapshot),
            typeof(AuditLog)
        ];

        Assert.All(
            appendOnlyTypes.SelectMany(type => type.GetProperties()),
            property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    [Fact]
    public void CollectorNodeRejectsUndefinedCollectorType()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CollectorNode(
                Guid.NewGuid(),
                "Collector",
                (CollectorType)999,
                "collector01.ae.local",
                "default",
                CreatedAt));

        Assert.Equal("collectorType", exception.ParamName);
    }

    [Fact]
    public void CollectorHeartbeatRejectsUndefinedHealthStatus()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CollectorHeartbeat(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreatedAt,
                (CollectorHealthStatus)999));

        Assert.Equal("status", exception.ParamName);
    }

    [Fact]
    public void CollectorRunRejectsUndefinedCollectionType()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CollectorRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                (CollectionType)999,
                CreatedAt));

        Assert.Equal("collectionType", exception.ParamName);
    }

    [Fact]
    public void AuditLogRejectsUndefinedOutcome()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AuditLog(
                Guid.NewGuid(),
                CreatedAt,
                @"AE\operator",
                "Audit.Test",
                (AuditOutcome)999));

        Assert.Equal("outcome", exception.ParamName);
    }

    [Fact]
    public void CommandQueueItemRejectsUndefinedCollectorType()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CommandQueueItem(
                Guid.NewGuid(),
                "Inventory.Refresh.v1",
                (CollectorType)999,
                "{}",
                0,
                CreatedAt,
                @"AE\operator"));

        Assert.Equal("targetCollectorType", exception.ParamName);
    }

    [Fact]
    public void DefinedEnumValuesAreAccepted()
    {
        var collector = new CollectorNode(
            Guid.NewGuid(),
            "Collector",
            CollectorType.Windows,
            "collector01.ae.local",
            "default",
            CreatedAt);
        var heartbeat = new CollectorHeartbeat(
            Guid.NewGuid(),
            collector.Id,
            CreatedAt,
            CollectorHealthStatus.Healthy);
        var run = new CollectorRun(
            Guid.NewGuid(),
            collector.Id,
            Guid.NewGuid(),
            CollectionType.Windows,
            CreatedAt);
        var audit = new AuditLog(
            Guid.NewGuid(),
            CreatedAt,
            @"AE\operator",
            "Audit.Test",
            AuditOutcome.Succeeded);
        var command = CreateCommand();

        Assert.Equal(CollectorType.Windows, collector.CollectorType);
        Assert.Equal(CollectorHealthStatus.Healthy, heartbeat.Status);
        Assert.Equal(CollectionType.Windows, run.CollectionType);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(CollectorType.Windows, command.TargetCollectorType);
    }

    private static CommandQueueItem CreateCommand(
        string payloadJson = "{}",
        int priority = 0) =>
        new(
            Guid.NewGuid(),
            "Inventory.Refresh.v1",
            CollectorType.Windows,
            payloadJson,
            priority,
            CreatedAt,
            @"AE\operator");
}
