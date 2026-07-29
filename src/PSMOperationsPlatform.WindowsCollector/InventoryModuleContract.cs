namespace PSMOperationsPlatform.WindowsCollector;

internal sealed record InventoryModuleContext(
    Guid ManagedServerId,
    string TargetFqdn,
    Guid InventoryRunId,
    IWinRmCommandSession Session,
    TimeProvider TimeProvider,
    ILogger Logger);

internal sealed record InventoryModuleResult<T>(
    bool IsSuccessful,
    T? NormalizedResult,
    bool IsValid,
    string? FailureCategory,
    bool IsValidEmpty,
    int RawResultCount,
    int NormalizedResultCount,
    double DurationMilliseconds,
    IReadOnlyList<string> Warnings)
{
    internal static InventoryModuleResult<T> Success(
        T result,
        bool isValidEmpty,
        int rawResultCount,
        int normalizedResultCount,
        double durationMilliseconds,
        IReadOnlyList<string>? warnings = null) =>
        new(
            true, result, true, null, isValidEmpty, rawResultCount,
            normalizedResultCount, durationMilliseconds, warnings ?? []);

    internal static InventoryModuleResult<T> Failure(
        string category,
        int rawResultCount,
        double durationMilliseconds,
        IReadOnlyList<string>? warnings = null) =>
        new(
            false, default, false, category, false, rawResultCount, 0,
            durationMilliseconds, warnings ?? []);
}

internal interface IInventoryModule<TNormalizedResult>
{
    WindowsInventoryModuleKind Kind { get; }

    Task<InventoryModuleResult<TNormalizedResult>> CollectAsync(
        InventoryModuleContext context,
        CancellationToken cancellationToken);
}

internal static class InventoryModuleFailure
{
    internal static string Category(Exception exception) => exception switch
    {
        TimeoutException => "Timeout",
        WinRmCommandExecutionException => "CollectionFailure",
        WindowsInventoryValidationException => "ValidationFailure",
        FormatException or OverflowException => "ParsingFailure",
        _ => "Unexpected",
    };
}
