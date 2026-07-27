using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class WindowsCollectorCycleTests
{
    [Fact]
    public async Task UsesOnePlatformTimeForTheEligibilityQuery()
    {
        var provider = new CapturingTargetProvider();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 27, 14, 30, 0, TimeSpan.FromHours(3)));
        var cycle = new WindowsCollectorCycle(
            provider,
            new SuccessfulProbe(),
            CreateScopeFactory(),
            timeProvider,
            NullLogger<WindowsCollectorCycle>.Instance);

        await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(1, timeProvider.UtcNowCalls);
        Assert.Equal(
            new DateTime(2026, 7, 27, 14, 30, 0),
            provider.CurrentTime);
    }

    [Fact]
    public async Task QueryCancellationRemainsNormalCancellation()
    {
        var cycle = new WindowsCollectorCycle(
            new CancelledTargetProvider(),
            new SuccessfulProbe(),
            CreateScopeFactory(),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cycle.RunAsync(cancellation.Token));
    }

    [Fact]
    public async Task QueryFailureIsMappedToSafeCycleSignal()
    {
        var cycle = new WindowsCollectorCycle(
            new FailingTargetProvider(),
            new SuccessfulProbe(),
            CreateScopeFactory(),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

        WindowsTargetLoadException exception =
            await Assert.ThrowsAsync<WindowsTargetLoadException>(
                () => cycle.RunAsync(CancellationToken.None));

        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("SENSITIVE-SENTINEL", exception.Message);
    }

    [Fact]
    public async Task ProbeConcurrencyIsBoundedAtTwenty()
    {
        var probe = new BlockingProbe();
        var cycle = CreateCycle(21, probe);

        Task runningCycle = cycle.RunAsync(CancellationToken.None);
        await probe.FirstTwentyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(20, probe.Started);
        Assert.Equal(20, probe.MaximumActive);

        probe.Release.TrySetResult();
        await runningCycle;

        Assert.Equal(21, probe.Started);
        Assert.Equal(20, probe.MaximumActive);
    }

    [Fact]
    public async Task CancellationStopsWaitingTargetsAndRunningProbes()
    {
        var probe = new BlockingProbe();
        var cycle = CreateCycle(40, probe);
        using var cancellation = new CancellationTokenSource();

        Task runningCycle = cycle.RunAsync(cancellation.Token);
        await probe.FirstTwentyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runningCycle);
        Assert.Equal(20, probe.Started);
    }

    [Fact]
    public async Task TargetFailureReleasesSlotAndDoesNotStopOtherTargets()
    {
        var probe = new OneFailureProbe();
        var cycle = CreateCycle(21, probe);

        await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(21, probe.Attempts);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotStopOtherTargets()
    {
        var persistence = new OneFailurePersistence();
        var cycle = new WindowsCollectorCycle(
            new StaticTargetProvider([Target(1), Target(2)]),
            new SuccessfulProbe(),
            CreateScopeFactory(persistence),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

        await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(2, persistence.Attempts);
    }

    [Fact]
    public async Task ParallelTargetsResolveInventoryOrchestratorsFromSeparateScopes()
    {
        var instances = new ConcurrentBag<ScopeTrackingInventoryOrchestrator>();
        var services = new ServiceCollection();
        services.AddScoped<IConnectivityResultPersistence, SuccessfulPersistence>();
        services.AddScoped<IWindowsInventoryOrchestrator>(_ =>
        {
            var instance = new ScopeTrackingInventoryOrchestrator();
            instances.Add(instance);
            return instance;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        var cycle = new WindowsCollectorCycle(
            new StaticTargetProvider([Target(1), Target(2)]),
            new SuccessfulProbe(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

        await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.Equal(1, instance.Attempts));
    }

    [Fact]
    public async Task SuccessfulSessionIsDisposedOnceAfterEmptyInventory()
    {
        var session = new CountingSession();
        var inventory = new CapturingInventoryOrchestrator();
        var cycle = new WindowsCollectorCycle(
            new StaticTargetProvider([Target(1)]),
            new SessionProbe(session),
            CreateScopeFactory(inventory: inventory),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

        await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(1, inventory.Attempts);
        Assert.Same(session, inventory.Session);
        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public async Task PersistenceRejectionDisposesSessionWithoutInventory()
    {
        var session = new CountingSession();
        var inventory = new CapturingInventoryOrchestrator();
        var cycle = new WindowsCollectorCycle(
            new StaticTargetProvider([Target(1)]),
            new SessionProbe(session),
            CreateScopeFactory(new SkippedPersistence(), inventory),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

        await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(0, inventory.Attempts);
        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public async Task InventoryFailureDisposesSuccessfulSessionOnce()
    {
        var session = new CountingSession();
        var cycle = new WindowsCollectorCycle(
            new StaticTargetProvider([Target(1)]),
            new SessionProbe(session),
            CreateScopeFactory(inventory: new FailingInventoryOrchestrator()),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

        await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public async Task InventoryCancellationDisposesSuccessfulSessionOnce()
    {
        var session = new CountingSession();
        using var cancellation = new CancellationTokenSource();
        var cycle = new WindowsCollectorCycle(
            new StaticTargetProvider([Target(1)]),
            new SessionProbe(session),
            CreateScopeFactory(
                inventory: new CancellingInventoryOrchestrator(cancellation)),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cycle.RunAsync(cancellation.Token));

        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public async Task CleanupFailureDoesNotReplaceInventoryCancellation()
    {
        var session = new CountingSession(throwOnDispose: true);
        using var cancellation = new CancellationTokenSource();
        var cycle = new WindowsCollectorCycle(
            new StaticTargetProvider([Target(1)]),
            new SessionProbe(session),
            CreateScopeFactory(
                inventory: new CancellingInventoryOrchestrator(cancellation)),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cycle.RunAsync(cancellation.Token));

        Assert.Equal(1, session.DisposeCount);
    }

    private static WindowsCollectorCycle CreateCycle(
        int targetCount,
        IWindowsConnectivityProbe probe) =>
        new(
            new StaticTargetProvider(
                Enumerable.Range(1, targetCount)
                    .Select(index => Target(index))
                    .ToArray()),
            probe,
            CreateScopeFactory(),
            TimeProvider.System,
            NullLogger<WindowsCollectorCycle>.Instance);

    private static IServiceScopeFactory CreateScopeFactory(
        IConnectivityResultPersistence? persistence = null,
        IWindowsInventoryOrchestrator? inventory = null)
    {
        var services = new ServiceCollection();
        if (persistence is null)
        {
            services.AddScoped<
                IConnectivityResultPersistence,
                SuccessfulPersistence>();
        }
        else
        {
            services.AddSingleton(persistence);
        }

        if (inventory is null)
        {
            services.AddScoped<
                IWindowsInventoryOrchestrator,
                NoOpInventoryOrchestrator>();
        }
        else
        {
            services.AddSingleton(inventory);
        }

        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    private static WindowsTarget Target(int index) =>
        new(
            Guid.NewGuid(),
            $"server-{index}.example.invalid",
            WinRmTransportMode.Auto,
            5986,
            5985,
            TimeSpan.FromSeconds(10));

    private sealed class CapturingTargetProvider : IWindowsTargetProvider
    {
        public DateTime CurrentTime { get; private set; }

        public Task<IReadOnlyList<WindowsTarget>> LoadEligibleAsync(
            DateTime currentTime,
            CancellationToken cancellationToken)
        {
            CurrentTime = currentTime;
            return Task.FromResult<IReadOnlyList<WindowsTarget>>([]);
        }
    }

    private sealed class CancelledTargetProvider : IWindowsTargetProvider
    {
        public Task<IReadOnlyList<WindowsTarget>> LoadEligibleAsync(
            DateTime currentTime,
            CancellationToken cancellationToken) =>
            Task.FromCanceled<IReadOnlyList<WindowsTarget>>(cancellationToken);
    }

    private sealed class FailingTargetProvider : IWindowsTargetProvider
    {
        public Task<IReadOnlyList<WindowsTarget>> LoadEligibleAsync(
            DateTime currentTime,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SENSITIVE-SENTINEL");
    }

    private sealed class StaticTargetProvider(
        IReadOnlyList<WindowsTarget> targets) : IWindowsTargetProvider
    {
        public Task<IReadOnlyList<WindowsTarget>> LoadEligibleAsync(
            DateTime currentTime,
            CancellationToken cancellationToken) =>
            Task.FromResult(targets);
    }

    private sealed class BlockingProbe : IWindowsConnectivityProbe
    {
        private int active;
        private int maximumActive;
        private int started;

        public TaskCompletionSource FirstTwentyStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Started => Volatile.Read(ref started);

        public int MaximumActive => Volatile.Read(ref maximumActive);

        public async Task<WindowsConnectivityProbeResult> ProbeAsync(
            WindowsTarget target,
            CancellationToken cancellationToken)
        {
            int currentActive = Interlocked.Increment(ref active);
            int observedMaximum;
            do
            {
                observedMaximum = maximumActive;
                if (currentActive <= observedMaximum)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(
                ref maximumActive,
                currentActive,
                observedMaximum) != observedMaximum);

            int currentStarted = Interlocked.Increment(ref started);
            if (currentStarted == 20)
            {
                FirstTwentyStarted.TrySetResult();
            }

            try
            {
                await Release.Task.WaitAsync(cancellationToken);
                return Success(target.TargetId);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }
    }

    private sealed class OneFailureProbe : IWindowsConnectivityProbe
    {
        private int attempts;

        public int Attempts => Volatile.Read(ref attempts);

        public Task<WindowsConnectivityProbeResult> ProbeAsync(
            WindowsTarget target,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new InvalidOperationException("SENSITIVE-SENTINEL");
            }

            return Task.FromResult(Success(target.TargetId));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset localNow)
        : TimeProvider
    {
        public int UtcNowCalls { get; private set; }

        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone(
                "Test-Turkiye",
                TimeSpan.FromHours(3),
                "Test Turkiye",
                "Test Turkiye");

        public override DateTimeOffset GetUtcNow()
        {
            UtcNowCalls++;
            return localNow.ToUniversalTime();
        }
    }

    private sealed class SuccessfulProbe : IWindowsConnectivityProbe
    {
        public Task<WindowsConnectivityProbeResult> ProbeAsync(
            WindowsTarget target,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new WindowsConnectivityProbeResult(
                    target.TargetId,
                    true,
                    [WinRmTransport.Https],
                    WinRmTransport.Https,
                    WinRmFailureCategory.None,
                    TimeSpan.Zero,
                    DateTimeOffset.MinValue,
                    new TestSession()));
    }

    private sealed class SuccessfulPersistence : IConnectivityResultPersistence
    {
        public Task<ConnectivityPersistenceResult> ApplyAsync(
            WindowsTarget target,
            WindowsConnectivityProbeResult probeResult,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new ConnectivityPersistenceResult(
                    target.TargetId,
                    ConnectivityPersistenceOutcome.AppliedSuccess,
                    ConnectivityState.Reachable,
                    0,
                    probeResult.CompletedAt.DateTime.AddMinutes(1)));
    }

    private sealed class OneFailurePersistence : IConnectivityResultPersistence
    {
        private int attempts;

        public int Attempts => Volatile.Read(ref attempts);

        public Task<ConnectivityPersistenceResult> ApplyAsync(
            WindowsTarget target,
            WindowsConnectivityProbeResult probeResult,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new InvalidOperationException("SENSITIVE-SENTINEL");
            }

            return Task.FromResult(
                new ConnectivityPersistenceResult(
                    target.TargetId,
                    ConnectivityPersistenceOutcome.AppliedSuccess,
                    ConnectivityState.Reachable,
                    0,
                    probeResult.CompletedAt.DateTime.AddMinutes(1)));
        }
    }

    private sealed class NoOpInventoryOrchestrator
        : IWindowsInventoryOrchestrator
    {
        public Task<WindowsInventoryOrchestrationResult> ExecuteAsync(
            WindowsTarget target,
            IWinRmCommandSession session,
            Guid correlationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new WindowsInventoryOrchestrationResult(target.TargetId, []));
    }

    private sealed class ScopeTrackingInventoryOrchestrator
        : IWindowsInventoryOrchestrator
    {
        public int Attempts { get; private set; }

        public Task<WindowsInventoryOrchestrationResult> ExecuteAsync(
            WindowsTarget target,
            IWinRmCommandSession session,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(
                new WindowsInventoryOrchestrationResult(target.TargetId, []));
        }
    }

    private sealed class FailingInventoryOrchestrator
        : IWindowsInventoryOrchestrator
    {
        public Task<WindowsInventoryOrchestrationResult> ExecuteAsync(
            WindowsTarget target,
            IWinRmCommandSession session,
            Guid correlationId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SENSITIVE-SENTINEL");
    }

    private sealed class CancellingInventoryOrchestrator(
        CancellationTokenSource cancellationSource)
        : IWindowsInventoryOrchestrator
    {
        public Task<WindowsInventoryOrchestrationResult> ExecuteAsync(
            WindowsTarget target,
            IWinRmCommandSession session,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            cancellationSource.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private sealed class TestSession : IWinRmCommandSession
    {
        public bool IsUsable => true;

        public Task OpenAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WinRmCommandRecord>>([]);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CountingSession(bool throwOnDispose = false)
        : IWinRmCommandSession
    {
        private int disposeCount;

        public int DisposeCount => Volatile.Read(ref disposeCount);

        public bool IsUsable => DisposeCount == 0;

        public Task OpenAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WinRmCommandRecord>>([]);

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref disposeCount);
            if (throwOnDispose)
            {
                throw new InvalidOperationException(
                    "Injected cleanup failure.");
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class SessionProbe(IWinRmCommandSession session)
        : IWindowsConnectivityProbe
    {
        public Task<WindowsConnectivityProbeResult> ProbeAsync(
            WindowsTarget target,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new WindowsConnectivityProbeResult(
                    target.TargetId,
                    true,
                    [WinRmTransport.Https],
                    WinRmTransport.Https,
                    WinRmFailureCategory.None,
                    TimeSpan.Zero,
                    DateTimeOffset.MinValue,
                    session));
    }

    private sealed class CapturingInventoryOrchestrator
        : IWindowsInventoryOrchestrator
    {
        public int Attempts { get; private set; }

        public IWinRmCommandSession? Session { get; private set; }

        public Task<WindowsInventoryOrchestrationResult> ExecuteAsync(
            WindowsTarget target,
            IWinRmCommandSession session,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            Attempts++;
            Session = session;
            return Task.FromResult(
                new WindowsInventoryOrchestrationResult(target.TargetId, []));
        }
    }

    private sealed class SkippedPersistence : IConnectivityResultPersistence
    {
        public Task<ConnectivityPersistenceResult> ApplyAsync(
            WindowsTarget target,
            WindowsConnectivityProbeResult probeResult,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new ConnectivityPersistenceResult(
                    target.TargetId,
                    ConnectivityPersistenceOutcome.SkippedStale));
    }

    private static WindowsConnectivityProbeResult Success(Guid targetId) =>
        new(
            targetId,
            true,
            [WinRmTransport.Https],
            WinRmTransport.Https,
            WinRmFailureCategory.None,
            TimeSpan.Zero,
            DateTimeOffset.MinValue);
}
