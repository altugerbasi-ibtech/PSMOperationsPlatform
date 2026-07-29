using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Collections.ObjectModel;

namespace PSMOperationsPlatform.WindowsCollector;

internal interface IWinRmSessionFactory
{
    IWinRmCommandSession Create(
        WindowsTarget target,
        WinRmTransport transport,
        TimeSpan timeout);
}

internal interface IWinRmCommandSession : IAsyncDisposable
{
    bool IsUsable { get; }

    Task OpenAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
        WinRmCommandDefinition command,
        CancellationToken cancellationToken);
}

internal sealed class WinRmCommandDefinition
{
    public WinRmCommandDefinition(
        string commandName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyList<string>? propertyNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        CommandName = commandName.Trim();
        var parameterCopy = new Dictionary<string, object?>(
            StringComparer.Ordinal);
        if (parameters is not null)
        {
            foreach ((string name, object? value) in parameters)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(name);
                parameterCopy.Add(name.Trim(), value);
            }
        }

        Parameters =
            new ReadOnlyDictionary<string, object?>(parameterCopy);

        var propertyNameCopy = new List<string>();
        if (propertyNames is not null)
        {
            foreach (string name in propertyNames)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(name);
                propertyNameCopy.Add(name.Trim());
            }
        }

        PropertyNames = propertyNameCopy.AsReadOnly();
    }

    public string CommandName { get; }

    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public IReadOnlyList<string> PropertyNames { get; }
}

internal sealed record WinRmCommandRecord(
    IReadOnlyDictionary<string, object?> Properties);

internal sealed class WinRmCommandExecutionException : Exception;

internal sealed class PowerShellWinRmSessionFactory : IWinRmSessionFactory
{
    public IWinRmCommandSession Create(
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
            AuthenticationMechanism = AuthenticationMechanism.Kerberos,
            IncludePortInSPN = true,
            OpenTimeout = timeoutMilliseconds,
            OperationTimeout = timeoutMilliseconds,
            MaximumConnectionRedirectionCount = 0
        };
    }
}

internal sealed class PowerShellWinRmSession(Runspace runspace)
    : IWinRmCommandSession
{
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private bool opened;
    private bool disposed;

    public bool IsUsable =>
        !disposed
        && opened
        && runspace.RunspaceStateInfo.State == RunspaceState.Opened;

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
            opened = true;
        }
        finally
        {
            runspace.StateChanged -= StateChanged;
        }
    }

    public async Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
        WinRmCommandDefinition command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!opened)
        {
            throw new InvalidOperationException(
                "The WinRM command session must be open before invocation.");
        }

        await commandGate.WaitAsync(cancellationToken);
        try
        {
            using PowerShell pipeline = PowerShell.Create();
            pipeline.Runspace = runspace;
            pipeline.AddCommand(command.CommandName);
            foreach ((string name, object? value) in command.Parameters)
            {
                pipeline.AddParameter(name, value);
            }

            PSDataCollection<PSObject> output;
            try
            {
                output = await pipeline.InvokeAsync()
                    .WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                pipeline.Stop();
                throw;
            }

            if (pipeline.HadErrors)
            {
                throw new WinRmCommandExecutionException();
            }

            return output
                .Select(item => MapRecord(item, command.PropertyNames))
                .ToArray();
        }
        finally
        {
            commandGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            try
            {
                runspace.Dispose();
            }
            finally
            {
                commandGate.Dispose();
                disposed = true;
            }
        }

        return ValueTask.CompletedTask;
    }

    private static WinRmCommandRecord MapRecord(
        PSObject item,
        IReadOnlyList<string> propertyNames)
    {
        var properties = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string propertyName in propertyNames)
        {
            PSPropertyInfo? property = item.Properties[propertyName];
            properties[propertyName] = property?.Value;
        }

        return new WinRmCommandRecord(
            new ReadOnlyDictionary<string, object?>(properties));
    }
}
