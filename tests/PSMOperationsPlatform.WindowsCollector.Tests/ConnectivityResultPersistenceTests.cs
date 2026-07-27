using Microsoft.Extensions.Options;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ConnectivityResultPersistenceTests
{
    private static readonly DateTimeOffset CompletedAt =
        new(2026, 7, 27, 18, 0, 0, TimeSpan.FromHours(3));

    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 300)]
    [InlineData(3, 900)]
    [InlineData(4, 1800)]
    [InlineData(5, 3600)]
    [InlineData(100, 3600)]
    [InlineData(int.MaxValue, 3600)]
    public void BackoffUsesDeterministicCappedTable(
        int failureCount,
        int expectedSeconds)
    {
        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            ConnectivityBackoff.Calculate(
                failureCount,
                TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public async Task SuccessAppliesReachableResetAndNormalEligibility()
    {
        ManagedServer server = Server();
        server.ApplyConnectivityFailure(
            CompletedAt.AddMinutes(-5).DateTime,
            ConnectivityFailureCategory.Timeout,
            CompletedAt.AddMinutes(-4).DateTime);
        var store = new TestStore(server);
        var persistence = CreatePersistence(store);

        ConnectivityPersistenceResult outcome = await persistence.ApplyAsync(
            Target(server),
            Success(server.Id, WinRmTransport.Http),
            CancellationToken.None);

        Assert.Equal(
            ConnectivityPersistenceOutcome.AppliedSuccess,
            outcome.Outcome);
        Assert.Equal(ConnectivityState.Reachable, server.LastConnectivityState);
        Assert.Equal(CompletedAt.DateTime, server.LastConnectivityAttemptAt);
        Assert.Equal(CompletedAt.DateTime, server.LastConnectivitySuccessAt);
        Assert.Equal(
            ConnectivityTransport.Http,
            server.LastSuccessfulTransport);
        Assert.Equal(0, server.ConsecutiveConnectivityFailures);
        Assert.Null(server.LastConnectivityFailureCategory);
        Assert.Equal(
            CompletedAt.AddMinutes(1).DateTime,
            server.NextConnectivityAttemptAt);
        Assert.Equal(WinRmTransportMode.Auto, server.WinRmTransportMode);
        Assert.Equal(5986, server.WinRmHttpsPort);
        Assert.Equal(5985, server.WinRmHttpPort);
        Assert.Equal(1, store.SaveAttempts);
    }

    [Fact]
    public async Task FailurePreservesLastSuccessAndUsesNewFailureCount()
    {
        ManagedServer server = Server();
        DateTime previousSuccess = CompletedAt.AddMinutes(-10).DateTime;
        server.ApplyConnectivitySuccess(
            previousSuccess,
            ConnectivityTransport.Https,
            previousSuccess.AddMinutes(1));
        server.ApplyConnectivityFailure(
            CompletedAt.AddMinutes(-5).DateTime,
            ConnectivityFailureCategory.Timeout,
            CompletedAt.DateTime);
        var store = new TestStore(server);

        ConnectivityPersistenceResult outcome = await CreatePersistence(store)
            .ApplyAsync(
                Target(server),
                Failure(server.Id, WinRmFailureCategory.ConnectionRefused),
                CancellationToken.None);

        Assert.Equal(
            ConnectivityPersistenceOutcome.AppliedFailure,
            outcome.Outcome);
        Assert.Equal(ConnectivityState.Unreachable, server.LastConnectivityState);
        Assert.Equal(CompletedAt.DateTime, server.LastConnectivityAttemptAt);
        Assert.Equal(previousSuccess, server.LastConnectivitySuccessAt);
        Assert.Equal(
            ConnectivityTransport.Https,
            server.LastSuccessfulTransport);
        Assert.Equal(2, server.ConsecutiveConnectivityFailures);
        Assert.Equal(
            ConnectivityFailureCategory.ConnectionRefused,
            server.LastConnectivityFailureCategory);
        Assert.Equal(
            CompletedAt.AddMinutes(5).DateTime,
            server.NextConnectivityAttemptAt);
    }

    [Fact]
    public async Task CancellationIsNoOpAndDoesNotLoadOrSave()
    {
        var store = new TestStore(Server());

        ConnectivityPersistenceResult outcome = await CreatePersistence(store)
            .ApplyAsync(
                Target(store.Current!),
                Failure(store.Current!.Id, WinRmFailureCategory.Cancelled),
                CancellationToken.None);

        Assert.Equal(
            ConnectivityPersistenceOutcome.SkippedCancelled,
            outcome.Outcome);
        Assert.Equal(0, store.LoadAttempts);
        Assert.Equal(0, store.SaveAttempts);
    }

    [Fact]
    public async Task DisabledStalePolicyChangedAndMissingTargetsAreSkipped()
    {
        ManagedServer disabled = Server();
        disabled.SetEnabled(false, CompletedAt.AddMinutes(-1).DateTime);
        Assert.Equal(
            ConnectivityPersistenceOutcome.SkippedDisabled,
            (await CreatePersistence(new TestStore(disabled)).ApplyAsync(
                Target(disabled),
                Success(disabled.Id),
                CancellationToken.None)).Outcome);

        ManagedServer stale = Server();
        stale.ApplyConnectivitySuccess(
            CompletedAt.DateTime,
            ConnectivityTransport.Https,
            CompletedAt.AddMinutes(1).DateTime);
        Assert.Equal(
            ConnectivityPersistenceOutcome.SkippedStale,
            (await CreatePersistence(new TestStore(stale)).ApplyAsync(
                Target(stale),
                Success(stale.Id),
                CancellationToken.None)).Outcome);

        ManagedServer changed = Server();
        WindowsTarget oldPolicy = Target(changed) with { HttpsPort = 15986 };
        Assert.Equal(
            ConnectivityPersistenceOutcome.SkippedStale,
            (await CreatePersistence(new TestStore(changed)).ApplyAsync(
                oldPolicy,
                Success(changed.Id),
                CancellationToken.None)).Outcome);

        Assert.Equal(
            ConnectivityPersistenceOutcome.TargetNotFound,
            (await CreatePersistence(new TestStore()).ApplyAsync(
                Target(Server()),
                Success(Guid.NewGuid()),
                CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task OneConcurrencyRetryReappliesFreshStateOnlyOnce()
    {
        ManagedServer first = Server();
        ManagedServer fresh = Server(first.Id);
        var store = new TestStore(first, fresh)
        {
            ConcurrencyFailuresRemaining = 1
        };

        ConnectivityPersistenceResult outcome = await CreatePersistence(store)
            .ApplyAsync(
                Target(first),
                Failure(first.Id, WinRmFailureCategory.Timeout),
                CancellationToken.None);

        Assert.Equal(
            ConnectivityPersistenceOutcome.AppliedFailure,
            outcome.Outcome);
        Assert.Equal(2, store.SaveAttempts);
        Assert.Equal(1, store.ClearCalls);
        Assert.Equal(1, fresh.ConsecutiveConnectivityFailures);
    }

    [Fact]
    public async Task SecondConcurrencyConflictStopsRetry()
    {
        ManagedServer first = Server();
        var store = new TestStore(first, Server(first.Id))
        {
            ConcurrencyFailuresRemaining = 2
        };

        ConnectivityPersistenceResult outcome = await CreatePersistence(store)
            .ApplyAsync(
                Target(first),
                Failure(first.Id, WinRmFailureCategory.Timeout),
                CancellationToken.None);

        Assert.Equal(
            ConnectivityPersistenceOutcome.ConcurrencyConflict,
            outcome.Outcome);
        Assert.Equal(2, store.SaveAttempts);
        Assert.Equal(1, store.ClearCalls);
    }

    [Fact]
    public async Task RetryRechecksStaleAndDisabledState()
    {
        ManagedServer first = Server();
        ManagedServer newer = Server(first.Id);
        newer.ApplyConnectivitySuccess(
            CompletedAt.DateTime,
            ConnectivityTransport.Https,
            CompletedAt.AddMinutes(1).DateTime);
        var staleStore = new TestStore(first, newer)
        {
            ConcurrencyFailuresRemaining = 1
        };
        Assert.Equal(
            ConnectivityPersistenceOutcome.SkippedStale,
            (await CreatePersistence(staleStore).ApplyAsync(
                Target(first),
                Failure(first.Id, WinRmFailureCategory.Timeout),
                CancellationToken.None)).Outcome);
        Assert.Equal(1, staleStore.SaveAttempts);

        ManagedServer enabled = Server();
        ManagedServer disabled = Server(enabled.Id);
        disabled.SetEnabled(false, CompletedAt.AddMinutes(-1).DateTime);
        var disabledStore = new TestStore(enabled, disabled)
        {
            ConcurrencyFailuresRemaining = 1
        };
        Assert.Equal(
            ConnectivityPersistenceOutcome.SkippedDisabled,
            (await CreatePersistence(disabledStore).ApplyAsync(
                Target(enabled),
                Failure(enabled.Id, WinRmFailureCategory.Timeout),
                CancellationToken.None)).Outcome);
        Assert.Equal(1, disabledStore.SaveAttempts);
    }

    [Fact]
    public async Task PersistenceFailureIsSafeOutcome()
    {
        ManagedServer server = Server();
        var store = new TestStore(server)
        {
            PersistenceFailure = new PersistenceUnavailableException(
                new TimeoutException("SENSITIVE-SENTINEL"))
        };

        ConnectivityPersistenceResult outcome = await CreatePersistence(store)
            .ApplyAsync(
                Target(server),
                Failure(server.Id, WinRmFailureCategory.Timeout),
                CancellationToken.None);

        Assert.Equal(
            ConnectivityPersistenceOutcome.PersistenceFailed,
            outcome.Outcome);
        Assert.Equal(1, store.SaveAttempts);
    }

    [Fact]
    public async Task SaveReceivesCallerCancellationToken()
    {
        ManagedServer server = Server();
        var store = new TestStore(server);
        using var cancellation = new CancellationTokenSource();

        await CreatePersistence(store).ApplyAsync(
            Target(server),
            Success(server.Id),
            cancellation.Token);

        Assert.Equal(cancellation.Token, store.LastSaveToken);
    }

    private static ConnectivityResultPersistence CreatePersistence(
        TestStore store) =>
        new(
            store,
            Options.Create(
                new WindowsCollectorOptions
                {
                    PollingInterval = TimeSpan.FromMinutes(1)
                }));

    private static ManagedServer Server(Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            "server.example.invalid",
            CompletedAt.AddHours(-1).DateTime);

    private static WindowsTarget Target(ManagedServer server) =>
        new(
            server.Id,
            server.Fqdn,
            server.WinRmTransportMode,
            server.WinRmHttpsPort,
            server.WinRmHttpPort,
            TimeSpan.FromSeconds(server.WinRmProbeTimeoutSeconds),
            server.RowVersion);

    private static WindowsConnectivityProbeResult Success(
        Guid targetId,
        WinRmTransport transport = WinRmTransport.Https) =>
        new(
            targetId,
            true,
            [transport],
            transport,
            WinRmFailureCategory.None,
            TimeSpan.FromMilliseconds(10),
            CompletedAt);

    private static WindowsConnectivityProbeResult Failure(
        Guid targetId,
        WinRmFailureCategory category) =>
        new(
            targetId,
            false,
            [WinRmTransport.Https],
            null,
            category,
            TimeSpan.FromMilliseconds(10),
            CompletedAt);

    private sealed class TestStore(params ManagedServer[] loads)
        : IManagedServerConnectivityStore
    {
        private readonly Queue<ManagedServer> remainingLoads = new(loads);

        public int ConcurrencyFailuresRemaining { get; init; }

        public Exception? PersistenceFailure { get; init; }

        public int LoadAttempts { get; private set; }

        public int SaveAttempts { get; private set; }

        public int ClearCalls { get; private set; }

        public CancellationToken LastSaveToken { get; private set; }

        public ManagedServer? Current { get; private set; } =
            loads.FirstOrDefault();

        public Task<ManagedServer?> FindAsync(
            Guid targetId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadAttempts++;
            Current = remainingLoads.Count > 0
                ? remainingLoads.Dequeue()
                : null;
            return Task.FromResult(Current);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            LastSaveToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            SaveAttempts++;
            if (SaveAttempts <= ConcurrencyFailuresRemaining)
            {
                throw new PersistenceConcurrencyException(
                    new InvalidOperationException("SENSITIVE-SENTINEL"));
            }

            if (PersistenceFailure is not null)
            {
                throw PersistenceFailure;
            }

            return Task.CompletedTask;
        }

        public void Clear() => ClearCalls++;
    }
}
