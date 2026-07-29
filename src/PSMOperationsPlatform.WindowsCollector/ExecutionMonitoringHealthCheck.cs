using Microsoft.Extensions.Diagnostics.HealthChecks;
using PSMOperationsPlatform.Application.Runtime;

namespace PSMOperationsPlatform.WindowsCollector;

/// <summary>Current in-process monitoring health; performs no target access.</summary>
internal sealed class ExecutionMonitoringHealthCheck(IExecutionMonitoring monitoring)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HealthCheckResult result = monitoring.Snapshot.Status switch
        {
            ExecutionMonitoringStatus.Healthy =>
                HealthCheckResult.Healthy("Execution monitoring is healthy."),
            ExecutionMonitoringStatus.Degraded =>
                HealthCheckResult.Degraded("Execution monitoring has recent adverse signals."),
            ExecutionMonitoringStatus.Unhealthy =>
                HealthCheckResult.Unhealthy("Execution monitoring instrumentation is unhealthy."),
            _ => HealthCheckResult.Degraded("Execution monitoring has not observed an event.")
        };
        return Task.FromResult(result);
    }
}
