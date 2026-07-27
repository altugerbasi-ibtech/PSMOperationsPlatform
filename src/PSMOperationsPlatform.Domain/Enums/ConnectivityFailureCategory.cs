namespace PSMOperationsPlatform.Domain.Enums;

public enum ConnectivityFailureCategory
{
    DnsFailure,
    ConnectionRefused,
    Timeout,
    TlsFailure,
    AuthenticationFailure,
    AuthorizationFailure,
    WinRmUnavailable,
    ProtocolFailure,
    Unexpected
}
