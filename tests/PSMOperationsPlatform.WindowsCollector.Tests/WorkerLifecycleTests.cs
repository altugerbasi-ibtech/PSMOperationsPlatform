using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class WorkerLifecycleTests
{
    [Fact]
    public async Task EachCycleUsesAndDisposesANewScopeWithoutOverlap()
    {
        var state = new CycleState(requiredRuns: 3);
        using IHost host = CreateHost(
            services => services.AddScoped<IWindowsCollectorCycle>(
                _ => new TrackingCycle(state)));

        await host.StartAsync();
        await state.RequiredRuns.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        Assert.Equal(3, state.Created);
        Assert.Equal(3, state.Disposed);
        Assert.Equal(1, state.MaximumActive);
    }

    [Fact]
    public async Task CycleFailureDoesNotStopLaterCyclesOrCreateATightLoop()
    {
        var state = new CycleState(requiredRuns: 2);
        var loggerProvider = new CaptureLoggerProvider();
        using IHost host = CreateHost(
            services => services.AddScoped<IWindowsCollectorCycle>(
                _ => new FailingFirstCycle(state)),
            loggerProvider);

        await host.StartAsync();
        await state.RequiredRuns.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        Assert.InRange(state.Created, 2, 20);
        Assert.Equal(1, state.MaximumActive);
        Assert.Contains(
            loggerProvider.Entries,
            entry => entry.EventId == WindowsCollectorLog.PollingCycleFailedId);
        Assert.All(
            loggerProvider.Entries.Where(
                entry =>
                    entry.EventId ==
                    WindowsCollectorLog.PollingCycleFailedId),
            entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.DoesNotContain(
            loggerProvider.Entries,
            entry =>
                entry.Message.Contains(
                    "SENSITIVE-SENTINEL",
                    StringComparison.Ordinal) ||
                entry.Exception is not null);
    }

    [Fact]
    public async Task TargetLoadFailureDoesNotStopLaterCyclesOrDuplicateFailureLog()
    {
        var state = new CycleState(requiredRuns: 2);
        var loggerProvider = new CaptureLoggerProvider();
        using IHost host = CreateHost(
            services => services.AddScoped<IWindowsCollectorCycle>(
                _ => new TargetLoadFailingFirstCycle(state)),
            loggerProvider);

        await host.StartAsync();
        await state.RequiredRuns.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        Assert.InRange(state.Created, 2, 20);
        Assert.DoesNotContain(
            loggerProvider.Entries,
            entry =>
                entry.EventId ==
                WindowsCollectorLog.PollingCycleFailedId);
    }

    [Fact]
    public async Task ShutdownCancellationIsNormalAndRetainsCorrelationScope()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loggerProvider = new CaptureLoggerProvider();
        using IHost host = CreateHost(
            services => services.AddScoped<IWindowsCollectorCycle>(
                _ => new BlockingCycle(entered)),
            loggerProvider);

        await host.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        Assert.DoesNotContain(
            loggerProvider.Entries,
            entry =>
                entry.EventId ==
                WindowsCollectorLog.PollingCycleFailedId);
        Assert.Contains(
            loggerProvider.Scopes,
            scope =>
                scope is IEnumerable<KeyValuePair<string, object>> values &&
                values.Any(value =>
                    value.Key == "PollingCycleId" &&
                    value.Value is Guid));
    }

    [Fact]
    public async Task DefaultDelayIsCancellableWithoutWaitingSixtySeconds()
    {
        var completed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loggerProvider = new CaptureLoggerProvider();
        using IHost host = CreateHost(
            services => services.AddScoped<IWindowsCollectorCycle>(
                _ => new SignalingCycle(completed)),
            loggerProvider,
            useDefaultInterval: true);

        await host.StartAsync();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task stopTask = host.StopAsync();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(
            loggerProvider.Entries,
            entry =>
                entry.EventId == WindowsCollectorLog.CollectorStartedId &&
                entry.Level == LogLevel.Information);
        Assert.Contains(
            loggerProvider.Entries,
            entry =>
                entry.EventId == WindowsCollectorLog.CollectorStoppingId &&
                entry.Level == LogLevel.Information);
    }

    private static IHost CreateHost(
        Action<IServiceCollection> configureCycle,
        ILoggerProvider? loggerProvider = null,
        TimeSpan? pollingInterval = null,
        bool useDefaultInterval = false)
    {
        HostApplicationBuilder builder =
            WindowsCollectorHost.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        if (loggerProvider is not null)
        {
            builder.Logging.AddProvider(loggerProvider);
        }

        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:OperationsDatabase"] =
                "Server=collector-test;Database=collector-test;" +
                "Integrated Security=True",
        };

        if (pollingInterval.HasValue)
        {
            values["WindowsCollector:PollingInterval"] =
                pollingInterval.Value.ToString("c");
        }
        else if (!useDefaultInterval)
        {
            values["WindowsCollector:PollingInterval"] =
                TimeSpan.FromMilliseconds(10).ToString("c");
        }

        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.RemoveAll<IWindowsCollectorCycle>();
        configureCycle(builder.Services);
        return builder.Build();
    }

    private sealed class CycleState(int requiredRuns)
    {
        private int active;
        private int created;
        private int disposed;
        private int maximumActive;

        public TaskCompletionSource RequiredRuns { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Created => Volatile.Read(ref created);

        public int Disposed => Volatile.Read(ref disposed);

        public int MaximumActive => Volatile.Read(ref maximumActive);

        public void Enter()
        {
            int currentCreated = Interlocked.Increment(ref created);
            int currentActive = Interlocked.Increment(ref active);
            InterlockedExtensions.Max(ref maximumActive, currentActive);

            if (currentCreated >= requiredRuns)
            {
                RequiredRuns.TrySetResult();
            }
        }

        public void Exit() => Interlocked.Decrement(ref active);

        public void Dispose() => Interlocked.Increment(ref disposed);
    }

    private sealed class TrackingCycle(CycleState state)
        : IWindowsCollectorCycle, IDisposable
    {
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            state.Enter();
            try
            {
                await Task.Yield();
            }
            finally
            {
                state.Exit();
            }
        }

        public void Dispose() => state.Dispose();
    }

    private sealed class FailingFirstCycle(CycleState state)
        : IWindowsCollectorCycle, IDisposable
    {
        public Task RunAsync(CancellationToken cancellationToken)
        {
            state.Enter();
            state.Exit();

            if (state.Created == 1)
            {
                throw new InvalidOperationException("SENSITIVE-SENTINEL");
            }

            return Task.CompletedTask;
        }

        public void Dispose() => state.Dispose();
    }

    private sealed class BlockingCycle(TaskCompletionSource entered)
        : IWindowsCollectorCycle
    {
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class TargetLoadFailingFirstCycle(CycleState state)
        : IWindowsCollectorCycle, IDisposable
    {
        public Task RunAsync(CancellationToken cancellationToken)
        {
            state.Enter();
            state.Exit();

            if (state.Created == 1)
            {
                throw new WindowsTargetLoadException();
            }

            return Task.CompletedTask;
        }

        public void Dispose() => state.Dispose();
    }

    private sealed class SignalingCycle(TaskCompletionSource completed)
        : IWindowsCollectorCycle
    {
        public Task RunAsync(CancellationToken cancellationToken)
        {
            completed.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public ConcurrentBag<LogEntry> Entries { get; } = [];

        public ConcurrentBag<object> Scopes { get; } = [];

        public ILogger CreateLogger(string categoryName) =>
            new CaptureLogger(this);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger(CaptureLoggerProvider provider)
            : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                provider.Scopes.Add(state);
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                provider.Entries.Add(
                    new LogEntry(
                        logLevel,
                        eventId.Id,
                        formatter(state, exception),
                        exception));
            }
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        int EventId,
        string Message,
        Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref location);
                if (current >= value)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(
                ref location,
                value,
                current) != current);
        }
    }
}
