using Microsoft.EntityFrameworkCore;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal interface IWindowsTargetProvider
{
    Task<IReadOnlyList<WindowsTarget>> LoadEligibleAsync(
        DateTime currentTime,
        CancellationToken cancellationToken);
}

internal sealed class WindowsTargetProvider(OperationsDbContext context)
    : IWindowsTargetProvider
{
    public async Task<IReadOnlyList<WindowsTarget>> LoadEligibleAsync(
        DateTime currentTime,
        CancellationToken cancellationToken)
    {
        return await context.ManagedServers
            .AsNoTracking()
            .Where(target =>
                target.IsEnabled
                && (target.NextConnectivityAttemptAt == null
                    || target.NextConnectivityAttemptAt <= currentTime
                    || target.NextInventoryAttemptAt == null
                    || target.NextInventoryAttemptAt <= currentTime))
            .Select(target => new WindowsTarget(
                target.Id,
                target.Fqdn,
                target.WinRmTransportMode,
                target.WinRmHttpsPort,
                target.WinRmHttpPort,
                TimeSpan.FromSeconds(target.WinRmProbeTimeoutSeconds),
                target.RowVersion,
                target.NextInventoryAttemptAt == null
                    || target.NextInventoryAttemptAt <= currentTime))
            .ToListAsync(cancellationToken);
    }
}
