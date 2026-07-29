using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Net.Sockets;
using System.Security.Authentication;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class WinRmFailureClassifier
{
    internal const int KerberosSpnMismatchErrorCode =
        unchecked((int)0x80090322);

    public static WinRmFailureCategory Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        foreach (Exception candidate in Enumerate(exception))
        {
            if (candidate.HResult == KerberosSpnMismatchErrorCode
                || candidate is PSRemotingTransportException
                {
                    ErrorCode: KerberosSpnMismatchErrorCode
                })
            {
                return WinRmFailureCategory.KerberosSpnMismatch;
            }

            if (candidate is SocketException socketException)
            {
                return socketException.SocketErrorCode switch
                {
                    SocketError.HostNotFound
                        or SocketError.NoData
                        or SocketError.TryAgain => WinRmFailureCategory.DnsFailure,
                    SocketError.ConnectionRefused =>
                        WinRmFailureCategory.ConnectionRefused,
                    SocketError.TimedOut => WinRmFailureCategory.Timeout,
                    _ => WinRmFailureCategory.WinRmUnavailable
                };
            }

            if (candidate is TimeoutException)
            {
                return WinRmFailureCategory.Timeout;
            }

            if (candidate is AuthenticationException)
            {
                return WinRmFailureCategory.TlsFailure;
            }

            if (candidate is UnauthorizedAccessException)
            {
                return WinRmFailureCategory.AuthorizationFailure;
            }

            if (candidate is RuntimeException runtimeException)
            {
                ErrorCategory category =
                    runtimeException.ErrorRecord.CategoryInfo.Category;
                if (category == ErrorCategory.PermissionDenied)
                {
                    return WinRmFailureCategory.AuthorizationFailure;
                }

                if (category == ErrorCategory.SecurityError
                    || category == ErrorCategory.AuthenticationError)
                {
                    return WinRmFailureCategory.AuthenticationFailure;
                }
            }

            if (candidate is PSRemotingDataStructureException)
            {
                return WinRmFailureCategory.ProtocolFailure;
            }

            if (candidate is PSRemotingTransportException)
            {
                return WinRmFailureCategory.WinRmUnavailable;
            }
        }

        return WinRmFailureCategory.Unexpected;
    }

    private static IEnumerable<Exception> Enumerate(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            yield return current;
        }
    }
}
