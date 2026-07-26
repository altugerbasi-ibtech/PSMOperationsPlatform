namespace PSMOperationsPlatform.WindowsCollector;

internal sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Windows Collector started.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
