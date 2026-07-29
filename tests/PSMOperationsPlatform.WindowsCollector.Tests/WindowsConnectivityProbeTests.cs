using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class WindowsConnectivityProbeTests
{
    [Fact]
    public async Task AutoStartsWithHttpsAndStopsAfterSuccess()
    {
        var client = new RecordingTransportClient(
            Success(WinRmTransport.Https));
        var probe = new WindowsConnectivityProbe(client, TimeProvider.System);

        WindowsConnectivityProbeResult result =
            await probe.ProbeAsync(Target(), CancellationToken.None);

        Assert.True(result.IsReachable);
        Assert.Equal([WinRmTransport.Https], client.Transports);
        Assert.Equal(WinRmTransport.Https, result.SuccessfulTransport);
        Assert.Equal(WinRmFailureCategory.None, result.FinalFailureCategory);
    }

    [Theory]
    [InlineData((int)WinRmFailureCategory.TlsFailure)]
    [InlineData((int)WinRmFailureCategory.ConnectionRefused)]
    [InlineData((int)WinRmFailureCategory.Timeout)]
    [InlineData((int)WinRmFailureCategory.WinRmUnavailable)]
    [InlineData((int)WinRmFailureCategory.ProtocolFailure)]
    public async Task AutoFallsBackOnlyForApprovedCategories(
        int categoryValue)
    {
        var category = (WinRmFailureCategory)categoryValue;
        var client = new RecordingTransportClient(
            Failure(WinRmTransport.Https, category),
            Success(WinRmTransport.Http));
        var probe = new WindowsConnectivityProbe(client, TimeProvider.System);

        WindowsConnectivityProbeResult result =
            await probe.ProbeAsync(Target(), CancellationToken.None);

        Assert.Equal(
            [WinRmTransport.Https, WinRmTransport.Http],
            client.Transports);
        Assert.True(result.IsReachable);
        Assert.Equal(WinRmTransport.Http, result.SuccessfulTransport);
        Assert.Equal(WinRmFailureCategory.None, result.FinalFailureCategory);
        Assert.Equal(1, client.Attempts[0].AttemptNumber);
        Assert.False(client.Attempts[0].IsFallbackAttempt);
        Assert.Equal(2, client.Attempts[1].AttemptNumber);
        Assert.True(client.Attempts[1].IsFallbackAttempt);
    }

    [Theory]
    [InlineData((int)WinRmFailureCategory.DnsFailure)]
    [InlineData((int)WinRmFailureCategory.AuthenticationFailure)]
    [InlineData((int)WinRmFailureCategory.KerberosSpnMismatch)]
    [InlineData((int)WinRmFailureCategory.AuthorizationFailure)]
    [InlineData((int)WinRmFailureCategory.Cancelled)]
    [InlineData((int)WinRmFailureCategory.Unexpected)]
    public async Task AutoDoesNotFallbackForClosedCategories(
        int categoryValue)
    {
        var category = (WinRmFailureCategory)categoryValue;
        var client = new RecordingTransportClient(
            Failure(WinRmTransport.Https, category));
        var probe = new WindowsConnectivityProbe(client, TimeProvider.System);

        WindowsConnectivityProbeResult result =
            await probe.ProbeAsync(Target(), CancellationToken.None);

        Assert.Equal([WinRmTransport.Https], client.Transports);
        Assert.False(result.IsReachable);
        Assert.Equal(category, result.FinalFailureCategory);
    }

    [Fact]
    public async Task HttpsOnlyUsesTargetHttpsConfiguration()
    {
        WindowsTarget target = Target(
            WinRmTransportMode.HttpsOnly,
            httpsPort: 15986,
            timeout: TimeSpan.FromSeconds(7));
        var client = new RecordingTransportClient(
            Success(WinRmTransport.Https));
        var probe = new WindowsConnectivityProbe(client, TimeProvider.System);

        await probe.ProbeAsync(target, CancellationToken.None);

        RecordedAttempt attempt = Assert.Single(client.Attempts);
        Assert.Equal(WinRmTransport.Https, attempt.Transport);
        Assert.Equal(15986, attempt.Target.HttpsPort);
        Assert.Equal(TimeSpan.FromSeconds(7), attempt.Timeout);
    }

    [Fact]
    public async Task HttpOnlyUsesTargetHttpConfiguration()
    {
        WindowsTarget target = Target(
            WinRmTransportMode.HttpOnly,
            httpPort: 15985,
            timeout: TimeSpan.FromSeconds(6));
        var client = new RecordingTransportClient(
            Success(WinRmTransport.Http));
        var probe = new WindowsConnectivityProbe(client, TimeProvider.System);

        await probe.ProbeAsync(target, CancellationToken.None);

        RecordedAttempt attempt = Assert.Single(client.Attempts);
        Assert.Equal(WinRmTransport.Http, attempt.Transport);
        Assert.Equal(15985, attempt.Target.HttpPort);
        Assert.Equal(TimeSpan.FromSeconds(6), attempt.Timeout);
    }

    [Fact]
    public async Task EveryAutoInvocationStartsWithHttps()
    {
        var client = new RecordingTransportClient(
            Failure(WinRmTransport.Https, WinRmFailureCategory.TlsFailure),
            Success(WinRmTransport.Http),
            Success(WinRmTransport.Https));
        var probe = new WindowsConnectivityProbe(client, TimeProvider.System);

        await probe.ProbeAsync(Target(), CancellationToken.None);
        await probe.ProbeAsync(Target(), CancellationToken.None);

        Assert.Equal(
            [
                WinRmTransport.Https,
                WinRmTransport.Http,
                WinRmTransport.Https
            ],
            client.Transports);
    }

    [Fact]
    public async Task AutoFallbackReceivesOnlyRemainingCombinedBudget()
    {
        var timeProvider = new AdvancingTimeProvider();
        var client = new AdvancingTransportClient(timeProvider);
        var probe = new WindowsConnectivityProbe(client, timeProvider);

        await probe.ProbeAsync(
            Target(timeout: TimeSpan.FromSeconds(15)),
            CancellationToken.None);

        Assert.Equal(2, client.Timeouts.Count);
        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeouts[0]);
        Assert.Equal(TimeSpan.FromSeconds(8), client.Timeouts[1]);
        Assert.Equal(
            TimeSpan.FromSeconds(20),
            WindowsConnectivityProbe.AutoBudget);
    }

    [Fact]
    public async Task ExternalCancellationDoesNotWaitForAutoBudget()
    {
        var client = new CancellingTransportClient();
        var probe = new WindowsConnectivityProbe(client, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();

        Task<WindowsConnectivityProbeResult> operation =
            probe.ProbeAsync(Target(), cancellation.Token);
        cancellation.Cancel();
        WindowsConnectivityProbeResult result =
            await operation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(WinRmFailureCategory.Cancelled, result.FinalFailureCategory);
        Assert.Equal([WinRmTransport.Https], client.Transports);
    }

    [Fact]
    public void ResultContainsNoExceptionCredentialOrMutableMetadata()
    {
        string[] names = typeof(WindowsConnectivityProbeResult)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(
            names,
            name =>
                name.Contains("Exception", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Credential", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Metadata", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            typeof(WindowsConnectivityProbeResult).GetProperties(),
            property => Assert.Contains(
                typeof(System.Runtime.CompilerServices.IsExternalInit),
                property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()));
    }

    private static WindowsTarget Target(
        WinRmTransportMode mode = WinRmTransportMode.Auto,
        int httpsPort = 5986,
        int httpPort = 5985,
        TimeSpan? timeout = null) =>
        new(
            Guid.NewGuid(),
            "target.example.local",
            mode,
            httpsPort,
            httpPort,
            timeout ?? TimeSpan.FromSeconds(10));

    private static WinRmAttemptResult Success(WinRmTransport transport) =>
        new(transport, true, WinRmFailureCategory.None, TimeSpan.Zero);

    private static WinRmAttemptResult Failure(
        WinRmTransport transport,
        WinRmFailureCategory category) =>
        new(transport, false, category, TimeSpan.Zero);

    private sealed class RecordingTransportClient(
        params WinRmAttemptResult[] results) : IWinRmTransportClient
    {
        private readonly Queue<WinRmAttemptResult> results = new(results);

        public List<RecordedAttempt> Attempts { get; } = [];

        public WinRmTransport[] Transports =>
            Attempts.Select(attempt => attempt.Transport).ToArray();

        public Task<WinRmAttemptResult> AttemptAsync(
            WindowsTarget target,
            WinRmTransport transport,
            TimeSpan timeout,
            int attemptNumber,
            bool isFallbackAttempt,
            CancellationToken cancellationToken)
        {
            Attempts.Add(new RecordedAttempt(
                target,
                transport,
                timeout,
                attemptNumber,
                isFallbackAttempt));
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed class CancellingTransportClient : IWinRmTransportClient
    {
        public List<WinRmTransport> Transports { get; } = [];

        public async Task<WinRmAttemptResult> AttemptAsync(
            WindowsTarget target,
            WinRmTransport transport,
            TimeSpan timeout,
            int attemptNumber,
            bool isFallbackAttempt,
            CancellationToken cancellationToken)
        {
            Transports.Add(transport);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Failure(transport, WinRmFailureCategory.Cancelled);
            }

            return Success(transport);
        }
    }

    private sealed class AdvancingTransportClient(
        AdvancingTimeProvider timeProvider) : IWinRmTransportClient
    {
        public List<TimeSpan> Timeouts { get; } = [];

        public Task<WinRmAttemptResult> AttemptAsync(
            WindowsTarget target,
            WinRmTransport transport,
            TimeSpan timeout,
            int attemptNumber,
            bool isFallbackAttempt,
            CancellationToken cancellationToken)
        {
            Timeouts.Add(timeout);
            if (transport == WinRmTransport.Https)
            {
                timeProvider.Advance(TimeSpan.FromSeconds(12));
                return Task.FromResult(
                    Failure(transport, WinRmFailureCategory.Timeout));
            }

            timeProvider.Advance(TimeSpan.FromSeconds(8));
            return Task.FromResult(Success(transport));
        }
    }

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.UnixEpoch.AddTicks(timestamp);

        public void Advance(TimeSpan value) => timestamp += value.Ticks;
    }

    private sealed record RecordedAttempt(
        WindowsTarget Target,
        WinRmTransport Transport,
        TimeSpan Timeout,
        int AttemptNumber,
        bool IsFallbackAttempt);
}
