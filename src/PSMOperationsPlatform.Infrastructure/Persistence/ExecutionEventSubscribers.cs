using PSMOperationsPlatform.Application.Runtime;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

/// <summary>
/// In-process fan-out with per-subscriber isolation. Delivery is best effort,
/// non-durable and not exactly once.
/// </summary>
public sealed class CompositeExecutionEventSink(
    IEnumerable<IExecutionEventSubscriber> subscribers) : IExecutionEventSink
{
    private readonly IReadOnlyList<IExecutionEventSubscriber> subscribers =
        subscribers.ToArray();

    public async Task PublishAsync(
        ExecutionEvent executionEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (IExecutionEventSubscriber subscriber in subscribers)
        {
            try { await subscriber.PublishAsync(executionEvent, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Each subscriber is observational and isolated. Execution State
                // remains authoritative.
            }
        }
    }
}

public interface IExecutionEventSubscriber : IExecutionEventSink;

public sealed class ExecutionMonitoringEventSubscriber(
    ExecutionMonitoringSubscriber monitoring) : IExecutionEventSubscriber
{
    public Task PublishAsync(
        ExecutionEvent executionEvent, CancellationToken cancellationToken) =>
        monitoring.PublishAsync(executionEvent, cancellationToken);
}
