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

        try
        {
            await using IWinRmSession session =
                sessionFactory.Create(target, transport, timeout);
            await session.OpenAsync(linkedSource.Token);
            return Result(true, WinRmFailureCategory.None);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return Result(false, WinRmFailureCategory.Cancelled);
        }
        catch (OperationCanceledException)
            when (timeoutSource.IsCancellationRequested)
        {
            return Result(false, WinRmFailureCategory.Timeout);
        }
        catch (Exception exception)
        {
            return Result(
                false,
                WinRmFailureClassifier.Classify(exception));
        }

        WinRmAttemptResult Result(
            bool isSuccessful,
            WinRmFailureCategory failureCategory) =>
            new(
                transport,
                isSuccessful,
                failureCategory,
                timeProvider.GetElapsedTime(startedAt));
    }
}
