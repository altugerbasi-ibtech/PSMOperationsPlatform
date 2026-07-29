using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal enum ConnectivityPersistenceOutcome
{
    AppliedSuccess,
    AppliedFailure,
    SkippedCancelled,
    SkippedDisabled,
    SkippedStale,
    TargetNotFound,
    ConcurrencyConflict,
    PersistenceFailed
}

internal sealed record ConnectivityPersistenceResult(
    Guid TargetId,
    ConnectivityPersistenceOutcome Outcome,
    ConnectivityState? State = null,
    int? FailureCount = null,
    DateTime? NextAttemptAt = null);

internal interface IConnectivityResultPersistence
{
    Task<ConnectivityPersistenceResult> ApplyAsync(
        WindowsTarget target,
        WindowsConnectivityProbeResult probeResult,
        CancellationToken cancellationToken);
}

internal interface IManagedServerConnectivityStore
{
    Task<ManagedServer?> FindAsync(
        Guid targetId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    void Clear();
}

internal sealed class ManagedServerConnectivityStore(
    OperationsDbContext context) : IManagedServerConnectivityStore
{
    public Task<ManagedServer?> FindAsync(
        Guid targetId,
        CancellationToken cancellationToken) =>
        context.ManagedServers.SingleOrDefaultAsync(
            target => target.Id == targetId,
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);
    }

    public void Clear() => context.ChangeTracker.Clear();
}

internal sealed class ConnectivityResultPersistence(
    IManagedServerConnectivityStore store,
    IOptions<WindowsCollectorOptions> options)
    : IConnectivityResultPersistence
{
    private const int MaximumSaveAttempts = 2;

    public async Task<ConnectivityPersistenceResult> ApplyAsync(
        WindowsTarget target,
        WindowsConnectivityProbeResult probeResult,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(probeResult);

        if (probeResult.FinalFailureCategory == WinRmFailureCategory.Cancelled)
        {
            return Result(ConnectivityPersistenceOutcome.SkippedCancelled);
        }

        for (int saveAttempt = 1;
             saveAttempt <= MaximumSaveAttempts;
             saveAttempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                ManagedServer? server = await store.FindAsync(
                    probeResult.TargetId,
                    cancellationToken);

                if (server is null)
                {
                    return Result(ConnectivityPersistenceOutcome.TargetNotFound);
                }

                if (!server.IsEnabled)
                {
                    return Result(ConnectivityPersistenceOutcome.SkippedDisabled);
                }

                DateTime completedAt = probeResult.CompletedAt.DateTime;
                if (server.LastConnectivityAttemptAt is DateTime lastAttempt
                    && completedAt <= lastAttempt)
                {
                    return Result(ConnectivityPersistenceOutcome.SkippedStale);
                }

                if (TargetPolicyChanged(server, target))
                {
                    return Result(ConnectivityPersistenceOutcome.SkippedStale);
                }

                ConnectivityPersistenceOutcome outcome;
                if (probeResult.IsReachable)
                {
                    ConnectivityTransport transport =
                        probeResult.SuccessfulTransport switch
                        {
                            WinRmTransport.Https => ConnectivityTransport.Https,
                            WinRmTransport.Http => ConnectivityTransport.Http,
                            _ => throw new InvalidOperationException(
                                "A successful probe must identify its transport.")
                        };
                    server.ApplyConnectivitySuccess(
                        completedAt,
                        transport,
                        completedAt + options.Value.PollingInterval);
                    outcome = ConnectivityPersistenceOutcome.AppliedSuccess;
                }
                else
                {
                    ConnectivityFailureCategory failureCategory =
                        MapFailure(probeResult.FinalFailureCategory);
                    int newFailureCount =
                        server.ConsecutiveConnectivityFailures == int.MaxValue
                            ? int.MaxValue
                            : server.ConsecutiveConnectivityFailures + 1;
                    TimeSpan delay = ConnectivityBackoff.Calculate(
                        newFailureCount,
                        options.Value.PollingInterval);
                    server.ApplyConnectivityFailure(
                        completedAt,
                        failureCategory,
                        completedAt + delay);
                    outcome = ConnectivityPersistenceOutcome.AppliedFailure;
                }

                await store.SaveChangesAsync(cancellationToken);
                return new ConnectivityPersistenceResult(
                    probeResult.TargetId,
                    outcome,
                    server.LastConnectivityState,
                    server.ConsecutiveConnectivityFailures,
                    server.NextConnectivityAttemptAt);
            }
            catch (PersistenceConcurrencyException)
                when (saveAttempt < MaximumSaveAttempts)
            {
                store.Clear();
            }
            catch (PersistenceConcurrencyException)
            {
                return Result(ConnectivityPersistenceOutcome.ConcurrencyConflict);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return Result(ConnectivityPersistenceOutcome.PersistenceFailed);
            }
        }

        return Result(ConnectivityPersistenceOutcome.ConcurrencyConflict);

        ConnectivityPersistenceResult Result(
            ConnectivityPersistenceOutcome outcome) =>
            new(probeResult.TargetId, outcome);
    }

    private static bool TargetPolicyChanged(
        ManagedServer server,
        WindowsTarget target) =>
        !string.Equals(
            server.Fqdn,
            target.HostName,
            StringComparison.OrdinalIgnoreCase)
        || server.WinRmTransportMode != target.TransportMode
        || server.WinRmHttpsPort != target.HttpsPort
        || server.WinRmHttpPort != target.HttpPort
        || server.WinRmProbeTimeoutSeconds !=
            checked((int)target.ProbeTimeout.TotalSeconds);

    private static ConnectivityFailureCategory MapFailure(
        WinRmFailureCategory failureCategory) =>
        failureCategory switch
        {
            WinRmFailureCategory.DnsFailure =>
                ConnectivityFailureCategory.DnsFailure,
            WinRmFailureCategory.ConnectionRefused =>
                ConnectivityFailureCategory.ConnectionRefused,
            WinRmFailureCategory.Timeout =>
                ConnectivityFailureCategory.Timeout,
            WinRmFailureCategory.TlsFailure =>
                ConnectivityFailureCategory.TlsFailure,
            WinRmFailureCategory.AuthenticationFailure =>
                ConnectivityFailureCategory.AuthenticationFailure,
            // Temporary until a separately approved migration extends the
            // persisted category CHECK constraint.
            WinRmFailureCategory.KerberosSpnMismatch =>
                ConnectivityFailureCategory.AuthenticationFailure,
            WinRmFailureCategory.AuthorizationFailure =>
                ConnectivityFailureCategory.AuthorizationFailure,
            WinRmFailureCategory.WinRmUnavailable =>
                ConnectivityFailureCategory.WinRmUnavailable,
            WinRmFailureCategory.ProtocolFailure =>
                ConnectivityFailureCategory.ProtocolFailure,
            WinRmFailureCategory.Unexpected =>
                ConnectivityFailureCategory.Unexpected,
            _ => throw new InvalidOperationException(
                "The probe result is not a completed target failure.")
        };
}
