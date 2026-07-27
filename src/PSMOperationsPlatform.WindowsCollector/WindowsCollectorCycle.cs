using System.Collections.Concurrent;

namespace PSMOperationsPlatform.WindowsCollector;

internal interface IWindowsCollectorCycle
{
    Task RunAsync(CancellationToken cancellationToken);
}

internal sealed class WindowsCollectorCycle(
    IWindowsTargetProvider targetProvider,
    IWindowsConnectivityProbe connectivityProbe,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<WindowsCollectorCycle> logger) : IWindowsCollectorCycle
{
    internal const int MaximumParallelProbes = 20;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset evaluationTime = timeProvider.GetLocalNow();
        long startedAt = timeProvider.GetTimestamp();
        IReadOnlyList<WindowsTarget> targets;

        try
        {
            targets = await targetProvider.LoadEligibleAsync(
                evaluationTime.DateTime,
                cancellationToken);

            WindowsCollectorLog.EligibleTargetsLoaded(
                logger,
                targets.Count,
                evaluationTime,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WindowsCollectorLog.TargetLoadFailed(
                logger,
                exception.GetType().Name);
            throw new WindowsTargetLoadException();
        }

        var results = new ConcurrentBag<WindowsConnectivityProbeResult>();
        var persistenceResults =
            new ConcurrentBag<ConnectivityPersistenceResult>();
        await Parallel.ForEachAsync(
            targets,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = MaximumParallelProbes
            },
            async (target, targetCancellationToken) =>
            {
                IWinRmCommandSession? session = null;
                try
                {
                    Guid inventoryCorrelationId = Guid.NewGuid();
                    WindowsConnectivityProbeResult result =
                        await connectivityProbe.ProbeAsync(
                            target,
                            targetCancellationToken);
                    session = result.Session;

                    if (result.FinalFailureCategory ==
                        WinRmFailureCategory.Cancelled)
                    {
                        targetCancellationToken.ThrowIfCancellationRequested();
                        return;
                    }

                    results.Add(result with { Session = null });
                    if (result.IsReachable)
                    {
                        WindowsCollectorLog.TargetProbeSucceeded(
                            logger,
                            result.TargetId,
                            target.TransportMode.ToString(),
                            result.SuccessfulTransport!.Value.ToString(),
                            result.Duration.TotalMilliseconds,
                            result.AttemptedTransports.Length);
                    }
                    else
                    {
                        WindowsCollectorLog.TargetProbeFailed(
                            logger,
                            result.TargetId,
                            target.TransportMode.ToString(),
                            result.FinalFailureCategory.ToString(),
                            result.Duration.TotalMilliseconds,
                            result.AttemptedTransports.Length);
                    }

                    await using AsyncServiceScope persistenceScope =
                        scopeFactory.CreateAsyncScope();
                    IConnectivityResultPersistence persistence =
                        persistenceScope.ServiceProvider
                            .GetRequiredService<IConnectivityResultPersistence>();
                    IWindowsInventoryOrchestrator inventoryOrchestrator =
                        persistenceScope.ServiceProvider
                            .GetRequiredService<IWindowsInventoryOrchestrator>();
                    ConnectivityPersistenceResult persistenceResult =
                        await persistence.ApplyAsync(
                            target,
                            result,
                            targetCancellationToken);
                    persistenceResults.Add(persistenceResult);
                    LogPersistenceOutcome(logger, persistenceResult);

                    if (persistenceResult.Outcome ==
                        ConnectivityPersistenceOutcome.AppliedSuccess)
                    {
                        if (session is null)
                        {
                            throw new InvalidOperationException(
                                "A successful WinRM probe did not return its session.");
                        }

                        await inventoryOrchestrator.ExecuteAsync(
                            target,
                            session,
                            inventoryCorrelationId,
                            targetCancellationToken);
                    }
                }
                catch (OperationCanceledException)
                    when (targetCancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    WindowsCollectorLog.TargetProbeUnexpected(
                        logger,
                        target.TargetId,
                        target.TransportMode.ToString(),
                        exception.GetType().Name);
                }
                finally
                {
                    if (session is not null)
                    {
                        try
                        {
                            await session.DisposeAsync();
                        }
                        catch (Exception exception)
                        {
                            WindowsCollectorLog.TargetProbeUnexpected(
                                logger,
                                target.TargetId,
                                target.TransportMode.ToString(),
                                exception.GetType().Name);
                        }
                    }
                }
            });

        WindowsCollectorLog.TargetProbeCycleSummary(
            logger,
            results.Count,
            results.Count(result => result.IsReachable),
            results.Count(result => !result.IsReachable));
        int persistenceFailureCount = persistenceResults.Count(result =>
            result.Outcome is
                ConnectivityPersistenceOutcome.ConcurrencyConflict
                or ConnectivityPersistenceOutcome.PersistenceFailed);
        WindowsCollectorLog.ConnectivityPersistenceSummary(
            logger,
            persistenceResults.Count(result =>
                result.Outcome is
                    ConnectivityPersistenceOutcome.AppliedSuccess
                    or ConnectivityPersistenceOutcome.AppliedFailure),
            persistenceResults.Count(result =>
                result.Outcome is
                    ConnectivityPersistenceOutcome.SkippedCancelled
                    or ConnectivityPersistenceOutcome.SkippedDisabled
                    or ConnectivityPersistenceOutcome.SkippedStale
                    or ConnectivityPersistenceOutcome.TargetNotFound),
            persistenceFailureCount);
        if (persistenceFailureCount > 0)
        {
            WindowsCollectorLog.ConnectivityPersistenceFailures(
                logger,
                persistenceFailureCount);
        }
    }

    private static void LogPersistenceOutcome(
        ILogger logger,
        ConnectivityPersistenceResult result)
    {
        switch (result.Outcome)
        {
            case ConnectivityPersistenceOutcome.AppliedSuccess:
            case ConnectivityPersistenceOutcome.AppliedFailure:
                WindowsCollectorLog.ConnectivityResultApplied(
                    logger,
                    result.TargetId,
                    result.Outcome.ToString(),
                    result.State!.Value.ToString(),
                    result.FailureCount!.Value,
                    result.NextAttemptAt!.Value);
                break;
            case ConnectivityPersistenceOutcome.ConcurrencyConflict:
                WindowsCollectorLog.ConnectivityConcurrencyConflict(
                    logger,
                    result.TargetId);
                break;
            case ConnectivityPersistenceOutcome.PersistenceFailed:
                WindowsCollectorLog.ConnectivityResultPersistFailed(
                    logger,
                    result.TargetId);
                break;
            default:
                WindowsCollectorLog.ConnectivityResultSkipped(
                    logger,
                    result.TargetId,
                    result.Outcome.ToString());
                break;
        }
    }
}

internal sealed class WindowsTargetLoadException : Exception;
