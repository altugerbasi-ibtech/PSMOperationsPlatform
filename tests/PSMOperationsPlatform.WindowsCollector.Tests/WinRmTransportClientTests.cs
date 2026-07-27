namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class WinRmTransportClientTests
{
    [Fact]
    public async Task SuccessTransfersSessionOwnershipWithoutDisposal()
    {
        var session = new TestSession();
        var client = CreateClient(session);

        WinRmAttemptResult result = await client.AttemptAsync(
            Target(),
            WinRmTransport.Https,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Same(session, result.Session);
        Assert.False(session.Disposed);

        await result.Session!.DisposeAsync();
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task FailureIsClassifiedAndDisposesSession()
    {
        var session = new TestSession(
            new System.Net.Sockets.SocketException(
                (int)System.Net.Sockets.SocketError.ConnectionRefused));
        var client = CreateClient(session);

        WinRmAttemptResult result = await client.AttemptAsync(
            Target(),
            WinRmTransport.Https,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal(
            WinRmFailureCategory.ConnectionRefused,
            result.FailureCategory);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task TimeoutIsDistinctAndDisposesSession()
    {
        var session = new TestSession(blockUntilCancellation: true);
        var client = CreateClient(session);

        WinRmAttemptResult result = await client.AttemptAsync(
            Target(),
            WinRmTransport.Https,
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None);

        Assert.Equal(WinRmFailureCategory.Timeout, result.FailureCategory);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task ExternalCancellationIsDistinctAndDisposesSession()
    {
        var session = new TestSession(blockUntilCancellation: true);
        var client = CreateClient(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        WinRmAttemptResult result = await client.AttemptAsync(
            Target(),
            WinRmTransport.Https,
            TimeSpan.FromSeconds(10),
            cancellation.Token);

        Assert.Equal(WinRmFailureCategory.Cancelled, result.FailureCategory);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task DisposalFailureDoesNotReplaceClassifiedOpenFailure()
    {
        var session = new TestSession(
            new System.Net.Sockets.SocketException(
                (int)System.Net.Sockets.SocketError.ConnectionRefused),
            throwOnDispose: true);
        var client = CreateClient(session);

        WinRmAttemptResult result = await client.AttemptAsync(
            Target(),
            WinRmTransport.Https,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal(
            WinRmFailureCategory.ConnectionRefused,
            result.FailureCategory);
        Assert.Equal(1, session.DisposeAttempts);
    }

    [Theory]
    [InlineData(
        System.Net.Sockets.SocketError.HostNotFound,
        (int)WinRmFailureCategory.DnsFailure)]
    [InlineData(
        System.Net.Sockets.SocketError.ConnectionRefused,
        (int)WinRmFailureCategory.ConnectionRefused)]
    [InlineData(
        System.Net.Sockets.SocketError.TimedOut,
        (int)WinRmFailureCategory.Timeout)]
    public void ClassifierUsesStableSocketCodes(
        System.Net.Sockets.SocketError socketError,
        int expectedValue)
    {
        var expected = (WinRmFailureCategory)expectedValue;
        var exception = new System.Net.Sockets.SocketException((int)socketError);

        Assert.Equal(expected, WinRmFailureClassifier.Classify(exception));
    }

    [Fact]
    public void ClassifierMapsTlsAuthorizationProtocolAndUnexpected()
    {
        Assert.Equal(
            WinRmFailureCategory.TlsFailure,
            WinRmFailureClassifier.Classify(
                new System.Security.Authentication.AuthenticationException()));
        Assert.Equal(
            WinRmFailureCategory.AuthorizationFailure,
            WinRmFailureClassifier.Classify(new UnauthorizedAccessException()));
        Assert.Equal(
            WinRmFailureCategory.ProtocolFailure,
            WinRmFailureClassifier.Classify(
                new System.Management.Automation.Remoting
                    .PSRemotingDataStructureException()));
        Assert.Equal(
            WinRmFailureCategory.Unexpected,
            WinRmFailureClassifier.Classify(new InvalidOperationException()));
    }

    [Theory]
    [InlineData((int)WinRmTransport.Https, "https", 15986)]
    [InlineData((int)WinRmTransport.Http, "http", 15985)]
    public void ConnectionInfoUsesTargetEndpointAndProcessIdentity(
        int transportValue,
        string expectedScheme,
        int expectedPort)
    {
        var transport = (WinRmTransport)transportValue;
        System.Management.Automation.Runspaces.WSManConnectionInfo connection =
            PowerShellWinRmSessionFactory.CreateConnectionInfo(
                Target(),
                transport,
                TimeSpan.FromSeconds(7));

        Assert.Equal(expectedScheme, connection.ConnectionUri.Scheme);
        Assert.Equal(expectedPort, connection.ConnectionUri.Port);
        Assert.Equal(
            System.Management.Automation.Runspaces.AuthenticationMechanism.Negotiate,
            connection.AuthenticationMechanism);
        Assert.Null(connection.Credential);
        Assert.Equal(7000, connection.OpenTimeout);
        Assert.Equal(7000, connection.OperationTimeout);
    }

    private static WinRmTransportClient CreateClient(TestSession session) =>
        new(new TestSessionFactory(session), TimeProvider.System);

    private static WindowsTarget Target() =>
        new(
            Guid.NewGuid(),
            "target.example.local",
            PSMOperationsPlatform.Domain.Enums.WinRmTransportMode.Auto,
            15986,
            15985,
            TimeSpan.FromSeconds(10));

    private sealed class TestSessionFactory(TestSession session)
        : IWinRmSessionFactory
    {
        public IWinRmCommandSession Create(
            WindowsTarget target,
            WinRmTransport transport,
            TimeSpan timeout) => session;
    }

    private sealed class TestSession(
        Exception? exception = null,
        bool blockUntilCancellation = false,
        bool throwOnDispose = false) : IWinRmCommandSession
    {
        public bool IsUsable => !Disposed;

        public bool Disposed { get; private set; }

        public int DisposeAttempts { get; private set; }

        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            if (blockUntilCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (exception is not null)
            {
                throw exception;
            }
        }

        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WinRmCommandRecord>>([]);

        public ValueTask DisposeAsync()
        {
            DisposeAttempts++;
            if (throwOnDispose)
            {
                throw new InvalidOperationException("Injected cleanup failure.");
            }

            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
