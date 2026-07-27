using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSMOperationsPlatform.WindowsCollector;

internal interface IWinRmSessionFactory
{
    IWinRmSession Create(
        WindowsTarget target,
        WinRmTransport transport,
        TimeSpan timeout);
}

internal interface IWinRmSession : IAsyncDisposable
{
    Task OpenAsync(CancellationToken cancellationToken);
}

internal sealed class PowerShellWinRmSessionFactory : IWinRmSessionFactory
{
    public IWinRmSession Create(
        WindowsTarget target,
        WinRmTransport transport,
        TimeSpan timeout)
    {
        WSManConnectionInfo connection =
            CreateConnectionInfo(target, transport, timeout);
        Runspace runspace = RunspaceFactory.CreateRunspace(connection);
        return new PowerShellWinRmSession(runspace);
    }

    internal static WSManConnectionInfo CreateConnectionInfo(
        WindowsTarget target,
        WinRmTransport transport,
        TimeSpan timeout)
    {
        string scheme =
            transport == WinRmTransport.Https ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        int port =
            transport == WinRmTransport.Https ? target.HttpsPort : target.HttpPort;
        var endpoint = new UriBuilder(scheme, target.HostName, port, "wsman").Uri;
        int timeoutMilliseconds = checked((int)timeout.TotalMilliseconds);
        return new WSManConnectionInfo(endpoint)
        {
            AuthenticationMechanism = AuthenticationMechanism.Negotiate,
            OpenTimeout = timeoutMilliseconds,
            OperationTimeout = timeoutMilliseconds,
            MaximumConnectionRedirectionCount = 0
        };
    }
}

internal sealed class PowerShellWinRmSession(Runspace runspace) : IWinRmSession
{
    private bool disposed;

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void StateChanged(object? sender, RunspaceStateEventArgs args)
        {
            if (args.RunspaceStateInfo.State == RunspaceState.Opened)
            {
                completion.TrySetResult();
            }
            else if (args.RunspaceStateInfo.State == RunspaceState.Broken)
            {
                completion.TrySetException(
                    args.RunspaceStateInfo.Reason
                    ?? new PSInvalidOperationException(
                        "The WinRM session could not be opened."));
            }
            else if (args.RunspaceStateInfo.State == RunspaceState.Closed)
            {
                completion.TrySetException(
                    new PSInvalidOperationException(
                        "The WinRM session closed before opening."));
            }
        }

        runspace.StateChanged += StateChanged;
        try
        {
            runspace.OpenAsync();
            await completion.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            runspace.StateChanged -= StateChanged;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            runspace.Dispose();
            disposed = true;
        }

        return ValueTask.CompletedTask;
    }
}
