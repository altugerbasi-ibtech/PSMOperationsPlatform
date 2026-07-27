namespace PSMOperationsPlatform.WindowsCollector;

internal static class ConnectivityBackoff
{
    internal static TimeSpan Calculate(
        int consecutiveFailureCount,
        TimeSpan pollingInterval)
    {
        if (consecutiveFailureCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(consecutiveFailureCount),
                "Failure count must be positive.");
        }

        return consecutiveFailureCount switch
        {
            1 => pollingInterval,
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            4 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromMinutes(60)
        };
    }
}
