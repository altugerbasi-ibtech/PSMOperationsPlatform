using Microsoft.Extensions.Logging;
using PSMOperationsPlatform.Application.Runtime;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

/// <summary>Safe lifecycle logging subscriber; execution state remains authoritative.</summary>
public sealed class LoggingExecutionEventSubscriber(
    ILogger<LoggingExecutionEventSubscriber> logger) : IExecutionEventSubscriber
{
    public Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Execution event {EventType} sequence {Sequence} for plan {ExecutionPlanId}, run {ExecutionRunId}, strategy {StrategyCode}, plugin {PluginId}, status {Status}, reason {ReasonCode}",
            executionEvent.EventType, executionEvent.Sequence, executionEvent.ExecutionPlanId,
            executionEvent.ExecutionRunId, executionEvent.StrategyCode, executionEvent.PluginId,
            executionEvent.Status, executionEvent.ReasonCode);
        return Task.CompletedTask;
    }
}
