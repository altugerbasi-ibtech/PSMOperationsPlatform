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
            1,
            false,
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
            1,
            false,
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
            1,
            false,
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
            1,
            false,
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
            1,
            false,
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

    [Fact]
    public void ClassifierUsesStructuredKerberosSpnErrorCodeWithoutMessageDependency()
    {
        var exception = new System.Management.Automation.Remoting
            .PSRemotingTransportException("localized diagnostic")
        {
            ErrorCode = WinRmFailureClassifier.KerberosSpnMismatchErrorCode
        };

        Assert.Equal(
            WinRmFailureCategory.KerberosSpnMismatch,
            WinRmFailureClassifier.Classify(exception));
        Assert.Equal(
            WinRmFailureCategory.WinRmUnavailable,
            WinRmFailureClassifier.Classify(
                new System.Management.Automation.Remoting
                    .PSRemotingTransportException("0x80090322 in text only")));
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
            System.Management.Automation.Runspaces.AuthenticationMechanism.Kerberos,
            connection.AuthenticationMechanism);
        Assert.NotEqual(
            System.Management.Automation.Runspaces.AuthenticationMechanism.Negotiate,
            connection.AuthenticationMechanism);
        Assert.True(connection.IncludePortInSPN);
        Assert.Null(connection.Credential);
        Assert.Equal(7000, connection.OpenTimeout);
        Assert.Equal(7000, connection.OperationTimeout);
    }

    [Fact]
    public void RepeatedSessionConfigurationPreservesKerberosAndPortQualifiedSpn()
    {
        foreach (WinRmTransport transport in new[]
                 {
                     WinRmTransport.Https,
                     WinRmTransport.Http,
                     WinRmTransport.Https
                 })
        {
            System.Management.Automation.Runspaces.WSManConnectionInfo connection =
                PowerShellWinRmSessionFactory.CreateConnectionInfo(
                    Target(),
                    transport,
                    TimeSpan.FromSeconds(7));

            Assert.Equal(
                System.Management.Automation.Runspaces.AuthenticationMechanism.Kerberos,
                connection.AuthenticationMechanism);
            Assert.True(connection.IncludePortInSPN);
            Assert.Null(connection.Credential);
        }
    }

    [Fact]
    public async Task AttemptLogsSafeStructuredKerberosContext()
    {
        var logger = new RecordingLogger<WinRmTransportClient>();
        var client = new WinRmTransportClient(
            new TestSessionFactory(new TestSession()),
            TimeProvider.System,
            logger);

        await client.AttemptAsync(
            Target(),
            WinRmTransport.Http,
            TimeSpan.FromSeconds(7),
            2,
            true,
            CancellationToken.None);

        LogEntry started = Assert.Single(
            logger.Entries,
            entry =>
                entry.EventId ==
                WindowsCollectorLog.WinRmConnectionAttemptStartedId);
        Assert.Equal("target.example.local", started.Values["TargetFqdn"]);
        Assert.Equal("Http", started.Values["Transport"]);
        Assert.Equal(15985, started.Values["Port"]);
        Assert.Equal("Kerberos", started.Values["Authentication"]);
        Assert.Equal(true, started.Values["IncludePortInSpn"]);
        Assert.Equal(7d, started.Values["ProbeTimeoutSeconds"]);
        Assert.Equal(2, started.Values["AttemptNumber"]);
        Assert.Equal(true, started.Values["IsFallbackAttempt"]);
        Assert.DoesNotContain(
            started.Values.Keys,
            key => key.Contains("Credential", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task KerberosSpnMismatchIsExposedInCompletedAttemptDiagnostics()
    {
        var logger = new RecordingLogger<WinRmTransportClient>();
        var exception = new System.Management.Automation.Remoting
            .PSRemotingTransportException("localized diagnostic")
        {
            ErrorCode = WinRmFailureClassifier.KerberosSpnMismatchErrorCode
        };
        var client = new WinRmTransportClient(
            new TestSessionFactory(new TestSession(exception)),
            TimeProvider.System,
            logger);

        WinRmAttemptResult result = await client.AttemptAsync(
            Target(),
            WinRmTransport.Https,
            TimeSpan.FromSeconds(7),
            1,
            false,
            CancellationToken.None);

        Assert.Equal(
            WinRmFailureCategory.KerberosSpnMismatch,
            result.FailureCategory);
        LogEntry completed = Assert.Single(
            logger.Entries,
            entry =>
                entry.EventId ==
                WindowsCollectorLog.WinRmConnectionAttemptCompletedId);
        Assert.Equal(
            "KerberosSpnMismatch",
            completed.Values["FailureCategory"]);
        Assert.DoesNotContain(
            completed.Values.Values,
            value => string.Equals(
                value?.ToString(),
                exception.Message,
                StringComparison.Ordinal));
    }

    private static WinRmTransportClient CreateClient(TestSession session) =>
        new(
            new TestSessionFactory(session),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<WinRmTransportClient>.Instance);

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

    private sealed record LogEntry(
        int EventId,
        IReadOnlyDictionary<string, object?> Values);

    private sealed class RecordingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) =>
            true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = ((IEnumerable<KeyValuePair<string, object?>>)state!)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Entries.Add(new LogEntry(eventId.Id, values));
        }
    }
}
