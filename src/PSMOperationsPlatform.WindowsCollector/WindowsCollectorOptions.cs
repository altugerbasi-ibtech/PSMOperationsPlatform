namespace PSMOperationsPlatform.WindowsCollector;

internal sealed class WindowsCollectorOptions
{
    internal const string SectionName = "WindowsCollector";

    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(60);
}
