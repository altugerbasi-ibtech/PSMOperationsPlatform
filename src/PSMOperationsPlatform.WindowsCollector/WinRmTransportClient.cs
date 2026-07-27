namespace PSMOperationsPlatform.WindowsCollector;

internal interface IWinRmTransportClient
{
    Task<WinRmAttemptResult> AttemptAsync(
        WindowsTarget target,
        WinRmTransport transport,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal sealed class WinRmTransportClient(
    IWinRmSessionFactory sessionFactory,
    TimeProvider timeProvider) : IWinRmTransportClient
{
    public async Task<WinRmAttemptResult> AttemptAsync(
        WindowsTarget target,
        WinRmTransport transport,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
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
            IWinRmCommandSession? session = null) =>
            new(
                transport,
                isSuccessful,
                failureCategory,
                timeProvider.GetElapsedTime(startedAt),
                session);
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
