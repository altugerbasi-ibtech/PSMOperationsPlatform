namespace PSMOperationsPlatform.SqlCollector;

internal sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SQL Collector started.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
