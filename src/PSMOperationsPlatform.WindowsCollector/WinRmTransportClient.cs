namespace PSMOperationsPlatform.WindowsCollector;

internal interface IWinRmTransportClient
{
    Task<WinRmAttemptResult> AttemptAsync(
        WindowsTarget target,
        WinRmTransport transport,
        TimeSpan timeout,
        int attemptNumber,
        bool isFallbackAttempt,
        CancellationToken cancellationToken);
}

internal sealed class WinRmTransportClient(
    IWinRmSessionFactory sessionFactory,
    TimeProvider timeProvider,
    ILogger<WinRmTransportClient> logger) : IWinRmTransportClient
{
    public async Task<WinRmAttemptResult> AttemptAsync(
        WindowsTarget target,
        WinRmTransport transport,
        TimeSpan timeout,
        int attemptNumber,
        bool isFallbackAttempt,
        CancellationToken cancellationToken)
    {
        int port = transport == WinRmTransport.Https
            ? target.HttpsPort
            : target.HttpPort;
        WindowsCollectorLog.WinRmConnectionAttemptStarted(
            logger,
            target.HostName,
            transport.ToString(),
            port,
            "Kerberos",
            true,
            timeout.TotalSeconds,
            attemptNumber,
            isFallbackAttempt);
        long startedAt = timeProvider.GetTimestamp();
        using var timeoutSource =
            new CancellationTokenSource(timeout, timeProvider);
        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);
        IWinRmCommandSession? session = null;

        try
        {
            session = sessionFactory.Create(target, transport, timeout);
            await session.OpenAsync(linkedSource.Token);
            return Result(true, WinRmFailureCategory.None, session);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await DisposeFailedSessionAsync(session);
            return Result(false, WinRmFailureCategory.Cancelled);
        }
        catch (OperationCanceledException)
            when (timeoutSource.IsCancellationRequested)
        {
            await DisposeFailedSessionAsync(session);
            return Result(false, WinRmFailureCategory.Timeout);
        }
        catch (Exception exception)
        {
            await DisposeFailedSessionAsync(session);
            return Result(
                false,
                WinRmFailureClassifier.Classify(exception));
        }

        WinRmAttemptResult Result(
            bool isSuccessful,
            WinRmFailureCategory failureCategory,
            IWinRmCommandSession? session = null)
        {
            TimeSpan duration = timeProvider.GetElapsedTime(startedAt);
            WindowsCollectorLog.WinRmConnectionAttemptCompleted(
                logger,
                target.HostName,
                transport.ToString(),
                port,
                "Kerberos",
                true,
                attemptNumber,
                isFallbackAttempt,
                isSuccessful,
                failureCategory.ToString(),
                duration.TotalMilliseconds);
            return new(
                transport,
                isSuccessful,
                failureCategory,
                duration,
                session);
        }
    }

    private static async ValueTask DisposeFailedSessionAsync(
        IWinRmCommandSession? session)
    {
        if (session is not null)
        {
            try
            {
                await session.DisposeAsync();
            }
            catch
            {
                // Cleanup failure must not replace the classified open result.
            }
        }
    }
}
