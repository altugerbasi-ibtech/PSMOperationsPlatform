using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class WindowsInventoryOrchestratorTests
{
    [Fact]
    public async Task EmptyPipelineCompletesSuccessfully()
    {
        var orchestrator = CreateOrchestrator([]);

        WindowsInventoryOrchestrationResult result =
            await orchestrator.ExecuteAsync(
                Target(),
                new TestSession(),
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.ModuleResults);
    }

    [Fact]
    public async Task ModulesExecuteInStableKindOrderWithTheSameContext()
    {
        var calls = new List<WindowsInventoryModuleKind>();
        var session = new TestSession();
        var timeProvider = new FixedTimeProvider();
        WindowsTarget target = Target();
        Guid correlationId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var modules = new IWindowsInventoryModule[]
        {
            new RecordingModule(
                WindowsInventoryModuleKind.Disk,
                calls),
            new RecordingModule(
                WindowsInventoryModuleKind.Computer,
                calls),
            new RecordingModule(
                WindowsInventoryModuleKind.Processor,
                calls),
        };
        var orchestrator = new WindowsInventoryOrchestrator(
            modules,
            timeProvider,
            NullLogger<WindowsInventoryOrchestrator>.Instance);

        WindowsInventoryOrchestrationResult result =
            await orchestrator.ExecuteAsync(
                target,
                session,
                correlationId,
                cancellation.Token);

        Assert.Equal(
            [
                WindowsInventoryModuleKind.Computer,
                WindowsInventoryModuleKind.Processor,
                WindowsInventoryModuleKind.Disk,
            ],
            calls);
        Assert.All(
            modules.Cast<RecordingModule>(),
            module =>
            {
                Assert.Same(target, module.Context!.ManagedServer);
                Assert.Same(session, module.Context.Session);
                Assert.Same(timeProvider, module.Context.TimeProvider);
                Assert.Equal(
                    target.ProbeTimeout,
                    module.Context.ManagedServer.ProbeTimeout);
                Assert.Equal(
                    cancellation.Token,
                    module.Context.CancellationToken);
                Assert.Same(
                    NullLogger<WindowsInventoryOrchestrator>.Instance,
                    module.Context.Logger);
                Assert.Equal(correlationId, module.Context.CorrelationId);
            });
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task ModuleFailureIsClassifiedAndDoesNotStopNextModule()
    {
        var calls = new List<WindowsInventoryModuleKind>();
        var orchestrator = CreateOrchestrator(
            [
                new RecordingModule(
                    WindowsInventoryModuleKind.Computer,
                    calls,
                    new InvalidOperationException("SENSITIVE-SENTINEL")),
                new RecordingModule(
                    WindowsInventoryModuleKind.OperatingSystem,
                    calls),
            ]);

        WindowsInventoryOrchestrationResult result =
            await orchestrator.ExecuteAsync(
                Target(),
                new TestSession(),
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Equal(2, calls.Count);
        Assert.False(result.IsSuccessful);
        Assert.Equal(
            "InvalidOperationException",
            result.ModuleResults[0].FailureCategory);
        Assert.Equal(
            WindowsInventoryModuleOutcome.Succeeded,
            result.ModuleResults[1].Outcome);
        Assert.DoesNotContain(
            "SENSITIVE-SENTINEL",
            result.ModuleResults[0].FailureCategory);
    }

    [Fact]
    public async Task UnusableSessionStopsRemainingModules()
    {
        var calls = new List<WindowsInventoryModuleKind>();
        var session = new TestSession();
        var orchestrator = CreateOrchestrator(
            [
                new BreakingModule(session, calls),
                new RecordingModule(
                    WindowsInventoryModuleKind.OperatingSystem,
                    calls),
            ]);

        WindowsInventoryOrchestrationResult result =
            await orchestrator.ExecuteAsync(
                Target(),
                session,
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Equal([WindowsInventoryModuleKind.Computer], calls);
        Assert.Single(result.ModuleResults);
        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CancellationPropagatesAndStopsLaterModules()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = new List<WindowsInventoryModuleKind>();
        var orchestrator = CreateOrchestrator(
            [
                new CancellingModule(cancellation, calls),
                new RecordingModule(
                    WindowsInventoryModuleKind.OperatingSystem,
                    calls),
            ]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => orchestrator.ExecuteAsync(
                Target(),
                new TestSession(),
                Guid.NewGuid(),
                cancellation.Token));

        Assert.Equal([WindowsInventoryModuleKind.Computer], calls);
    }

    [Fact]
    public void DuplicateModuleKindsFailFast()
    {
        var calls = new List<WindowsInventoryModuleKind>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateOrchestrator(
                [
                    new RecordingModule(
                        WindowsInventoryModuleKind.Computer,
                        calls),
                    new RecordingModule(
                        WindowsInventoryModuleKind.Computer,
                        calls),
                ]));

        Assert.Contains("Computer", exception.Message);
    }

    [Fact]
    public async Task PreCancelledExecutionDoesNotStartAModule()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var calls = new List<WindowsInventoryModuleKind>();
        var orchestrator = CreateOrchestrator(
            [
                new RecordingModule(
                    WindowsInventoryModuleKind.Computer,
                    calls),
            ]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => orchestrator.ExecuteAsync(
                Target(),
                new TestSession(),
                Guid.NewGuid(),
                cancellation.Token));

        Assert.Empty(calls);
    }

    private static WindowsInventoryOrchestrator CreateOrchestrator(
        IEnumerable<IWindowsInventoryModule> modules) =>
        new(
            modules,
            new FixedTimeProvider(),
            NullLogger<WindowsInventoryOrchestrator>.Instance);

    private static WindowsTarget Target() =>
        new(
            Guid.NewGuid(),
            "inventory-target.example.local",
            WinRmTransportMode.Auto,
            5986,
            5985,
            TimeSpan.FromSeconds(10));

    private sealed class RecordingModule(
        WindowsInventoryModuleKind kind,
        List<WindowsInventoryModuleKind> calls,
        Exception? exception = null) : IWindowsInventoryModule
    {
        public WindowsInventoryModuleKind Kind => kind;

        public WindowsInventoryExecutionContext? Context { get; private set; }

        public Task ExecuteAsync(WindowsInventoryExecutionContext context)
        {
            Context = context;
            calls.Add(Kind);
            if (exception is not null)
            {
                throw exception;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CancellingModule(
        CancellationTokenSource cancellation,
        List<WindowsInventoryModuleKind> calls) : IWindowsInventoryModule
    {
        public WindowsInventoryModuleKind Kind =>
            WindowsInventoryModuleKind.Computer;

        public Task ExecuteAsync(WindowsInventoryExecutionContext context)
        {
            calls.Add(Kind);
            cancellation.Cancel();
            context.CancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class BreakingModule(
        TestSession session,
        List<WindowsInventoryModuleKind> calls) : IWindowsInventoryModule
    {
        public WindowsInventoryModuleKind Kind =>
            WindowsInventoryModuleKind.Computer;

        public Task ExecuteAsync(WindowsInventoryExecutionContext context)
        {
            calls.Add(Kind);
            session.IsUsable = false;
            throw new InvalidOperationException("SENSITIVE-SENTINEL");
        }
    }

    private sealed class TestSession : IWinRmCommandSession
    {
        public bool IsUsable { get; set; } = true;

        public Task OpenAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WinRmCommandRecord>>([]);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() =>
            Interlocked.Increment(ref timestamp);
    }
}
