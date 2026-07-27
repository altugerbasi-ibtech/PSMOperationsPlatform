using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class ProcessorInventoryCommand
{
    internal static readonly WinRmCommandDefinition Definition = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_Processor",
            ["Property"] = new[]
            {
                "DeviceID",
                "Name",
                "Manufacturer",
                "NumberOfCores",
                "NumberOfLogicalProcessors",
                "MaxClockSpeed",
            },
        },
        [
            "DeviceID",
            "Name",
            "Manufacturer",
            "NumberOfCores",
            "NumberOfLogicalProcessors",
            "MaxClockSpeed",
        ]);
}

internal sealed class ProcessorInventoryModule(IProcessorSnapshotStore store)
    : IWindowsInventoryModule
{
    public WindowsInventoryModuleKind Kind =>
        WindowsInventoryModuleKind.Processor;

    public async Task ExecuteAsync(WindowsInventoryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<WinRmCommandRecord> records =
            await context.Session.InvokeAsync(
                ProcessorInventoryCommand.Definition,
                context.CancellationToken);

        var stableSourceKeys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var items = new List<ProcessorInventoryItem>(records.Count);
        foreach (WinRmCommandRecord record in records)
        {
            string stableSourceKey =
                WindowsInventoryRecordNormalizer.RequiredString(
                    record,
                    "DeviceID",
                    200);
            if (!stableSourceKeys.Add(stableSourceKey))
            {
                throw new WindowsInventoryValidationException(
                    "Processor inventory contains a duplicate DeviceID.");
            }

            items.Add(
                new ProcessorInventoryItem(
                    stableSourceKey,
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "Name",
                        255),
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "Manufacturer",
                        255),
                    WindowsInventoryRecordNormalizer.OptionalPositiveInt32(
                        record,
                        "NumberOfCores"),
                    WindowsInventoryRecordNormalizer.OptionalPositiveInt32(
                        record,
                        "NumberOfLogicalProcessors"),
                    WindowsInventoryRecordNormalizer.OptionalPositiveInt32(
                        record,
                        "MaxClockSpeed")));
        }

        await store.ReplaceAsync(
            context.ManagedServer.TargetId,
            items,
            context.CancellationToken);
    }
}
