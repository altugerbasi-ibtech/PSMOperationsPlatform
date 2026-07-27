using Microsoft.Extensions.Options;

namespace PSMOperationsPlatform.WindowsCollector;

internal sealed class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<WindowsCollectorOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WindowsCollectorLog.CollectorStarted(logger);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Guid pollingCycleId = Guid.NewGuid();

                using (logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["PollingCycleId"] = pollingCycleId,
                    }))
                {
                    WindowsCollectorLog.PollingCycleStarted(
                        logger,
                        timeProvider.GetLocalNow());

                    try
                    {
                        await using AsyncServiceScope scope =
                            scopeFactory.CreateAsyncScope();
                        IWindowsCollectorCycle cycle = scope.ServiceProvider
                            .GetRequiredService<IWindowsCollectorCycle>();
                        await cycle.RunAsync(stoppingToken);
                        WindowsCollectorLog.PollingCycleCompleted(logger);
                    }
                    catch (OperationCanceledException)
                        when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (WindowsTargetLoadException)
                    {
                        // The scoped cycle already emitted the safe target-load event.
                    }
                    catch (Exception exception)
                    {
                        WindowsCollectorLog.PollingCycleFailed(
                            logger,
                            exception.GetType().Name);
                    }
                }

                try
                {
                    await Task.Delay(
                        options.Value.PollingInterval,
                        timeProvider,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            WindowsCollectorLog.CollectorStopping(logger);
        }
    }
}
