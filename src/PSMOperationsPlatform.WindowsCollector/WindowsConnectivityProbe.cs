using System.Collections.Immutable;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.WindowsCollector;

internal interface IWindowsConnectivityProbe
{
    Task<WindowsConnectivityProbeResult> ProbeAsync(
        WindowsTarget target,
        CancellationToken cancellationToken);
}

internal sealed class WindowsConnectivityProbe(
    IWinRmTransportClient transportClient,
    TimeProvider timeProvider) : IWindowsConnectivityProbe
{
    internal static readonly TimeSpan AutoBudget = TimeSpan.FromSeconds(20);

    public async Task<WindowsConnectivityProbeResult> ProbeAsync(
        WindowsTarget target,
        CancellationToken cancellationToken)
    {
        long startedAt = timeProvider.GetTimestamp();

        return target.TransportMode switch
        {
            WinRmTransportMode.HttpsOnly =>
                Final(
                    target.TargetId,
                    await transportClient.AttemptAsync(
                        target,
                        WinRmTransport.Https,
                        target.ProbeTimeout,
                        1,
                        false,
                        cancellationToken),
                    startedAt),
            WinRmTransportMode.HttpOnly =>
                Final(
                    target.TargetId,
                    await transportClient.AttemptAsync(
                        target,
                        WinRmTransport.Http,
                        target.ProbeTimeout,
                        1,
                        false,
                        cancellationToken),
                    startedAt),
            WinRmTransportMode.Auto =>
                await ProbeAutoAsync(
                    target,
                    startedAt,
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(target),
                target.TransportMode,
                "Target transport mode is not supported.")
        };
    }

    private async Task<WindowsConnectivityProbeResult> ProbeAutoAsync(
        WindowsTarget target,
        long startedAt,
        CancellationToken cancellationToken)
    {
        using var budgetSource =
            new CancellationTokenSource(AutoBudget, timeProvider);
        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                budgetSource.Token);

        WinRmAttemptResult https = await AttemptAsync(
            WinRmTransport.Https,
            1,
            false);

        if (https.IsSuccessful || !ShouldFallback(https.FailureCategory))
        {
            return Final(target.TargetId, https, startedAt);
        }

        WinRmAttemptResult http = await AttemptAsync(
            WinRmTransport.Http,
            2,
            true);

        return Final(
            target.TargetId,
            http,
            startedAt,
            WinRmTransport.Https);

        async Task<WinRmAttemptResult> AttemptAsync(
            WinRmTransport transport,
            int attemptNumber,
            bool isFallbackAttempt)
        {
            WinRmAttemptResult result = await transportClient.AttemptAsync(
                target,
                transport,
                RemainingAttemptTimeout(target.ProbeTimeout, startedAt),
                attemptNumber,
                isFallbackAttempt,
                linkedSource.Token);

            if (result.FailureCategory == WinRmFailureCategory.Cancelled
                && !cancellationToken.IsCancellationRequested
                && budgetSource.IsCancellationRequested)
            {
                return result with
                {
                    FailureCategory = WinRmFailureCategory.Timeout
                };
            }

            return result;
        }
    }

    internal static bool ShouldFallback(WinRmFailureCategory category) =>
        category is
            WinRmFailureCategory.TlsFailure
            or WinRmFailureCategory.ConnectionRefused
            or WinRmFailureCategory.Timeout
            or WinRmFailureCategory.WinRmUnavailable
            or WinRmFailureCategory.ProtocolFailure;

    private TimeSpan RemainingAttemptTimeout(
        TimeSpan configuredTimeout,
        long startedAt)
    {
        TimeSpan remaining = AutoBudget - timeProvider.GetElapsedTime(startedAt);
        return remaining <= TimeSpan.Zero
            ? TimeSpan.FromTicks(1)
            : TimeSpan.Compare(configuredTimeout, remaining) <= 0
                ? configuredTimeout
                : remaining;
    }

    private WindowsConnectivityProbeResult Final(
        Guid targetId,
        WinRmAttemptResult finalAttempt,
        long startedAt,
        WinRmTransport? previousTransport = null)
    {
        ImmutableArray<WinRmTransport> attempted = previousTransport.HasValue
            ? [previousTransport.Value, finalAttempt.Transport]
            : [finalAttempt.Transport];

        return new WindowsConnectivityProbeResult(
            targetId,
            finalAttempt.IsSuccessful,
            attempted,
            finalAttempt.IsSuccessful ? finalAttempt.Transport : null,
            finalAttempt.IsSuccessful
                ? WinRmFailureCategory.None
                : finalAttempt.FailureCategory,
            timeProvider.GetElapsedTime(startedAt),
            timeProvider.GetLocalNow(),
            finalAttempt.Session);
    }
}
