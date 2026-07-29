using System.Collections.Immutable;

namespace PSMOperationsPlatform.WindowsCollector;

internal enum WinRmTransport
{
    Https,
    Http
}

internal enum WinRmFailureCategory
{
    None,
    DnsFailure,
    ConnectionRefused,
    Timeout,
    TlsFailure,
    AuthenticationFailure,
    KerberosSpnMismatch,
    AuthorizationFailure,
    WinRmUnavailable,
    ProtocolFailure,
    Cancelled,
    Unexpected
}

internal sealed record WinRmAttemptResult(
    WinRmTransport Transport,
    bool IsSuccessful,
    WinRmFailureCategory FailureCategory,
    TimeSpan Duration,
    IWinRmCommandSession? Session = null);

internal sealed record WindowsConnectivityProbeResult(
    Guid TargetId,
    bool IsReachable,
    ImmutableArray<WinRmTransport> AttemptedTransports,
    WinRmTransport? SuccessfulTransport,
    WinRmFailureCategory FinalFailureCategory,
    TimeSpan Duration,
    DateTimeOffset CompletedAt,
    IWinRmCommandSession? Session = null);
