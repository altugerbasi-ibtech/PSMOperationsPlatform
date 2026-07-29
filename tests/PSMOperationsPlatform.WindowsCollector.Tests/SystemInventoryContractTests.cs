namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class SystemInventoryContractTests
{
    [Theory]
    [InlineData("To Be Filled By O.E.M.")]
    [InlineData("Default string")]
    [InlineData("Not Specified")]
    [InlineData("Unknown")]
    public void General_placeholders_are_explicitly_unavailable(string value) =>
        Assert.Null(InventoryPlaceholderNormalizer.General(value));

    [Theory]
    [InlineData("System Serial Number")]
    [InlineData("00000000")]
    public void Serial_placeholders_are_explicitly_unavailable(string value) =>
        Assert.Null(InventoryPlaceholderNormalizer.Serial(value));

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    public void Placeholder_uuids_are_unavailable(string value) =>
        Assert.Null(InventoryPlaceholderNormalizer.Uuid(value));

    [Fact]
    public void Valid_uuid_is_preserved()
    {
        Guid expected = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        Assert.Equal(expected, InventoryPlaceholderNormalizer.Uuid(expected.ToString()));
    }

    [Fact]
    public void Structurally_invalid_uuid_is_rejected() =>
        Assert.Throws<WindowsInventoryValidationException>(
            () => InventoryPlaceholderNormalizer.Uuid("not-a-uuid"));

    [Theory]
    [InlineData("Microsoft Corporation", "Virtual Machine")]
    [InlineData("VMware, Inc.", "VMware Virtual Platform")]
    public void Known_virtual_platform_is_classified(
        string manufacturer,
        string model) =>
        Assert.True(InventoryPlaceholderNormalizer.VirtualMachine(manufacturer, model));

    [Fact]
    public void Physical_platform_is_classified_false() =>
        Assert.False(InventoryPlaceholderNormalizer.VirtualMachine("Dell Inc.", "PowerEdge R750"));

    [Fact]
    public void Missing_platform_identity_has_unknown_classification() =>
        Assert.Null(InventoryPlaceholderNormalizer.VirtualMachine(null, null));

    [Fact]
    public void System_strings_are_trimmed_and_internal_whitespace_is_collapsed()
    {
        var record = Record(("Model", "  PowerEdge\t R750  "));
        Assert.Equal(
            "PowerEdge R750",
            WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "Model", 255));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Single_row_modules_reject_non_single_results(int count)
    {
        IReadOnlyList<WinRmCommandRecord> records =
            Enumerable.Range(0, count).Select(_ => Record(("Name", "value"))).ToArray();
        Assert.Throws<WindowsInventoryValidationException>(
            () => WindowsInventoryRecordNormalizer.Single(records, "System"));
    }

    [Fact]
    public void Bios_projection_is_read_only_narrow_and_contains_no_sensitive_firmware_data()
    {
        WinRmCommandDefinition command = CoreWindowsInventoryCommands.Bios;
        Assert.Equal("Get-CimInstance", command.CommandName);
        Assert.Equal("Win32_BIOS", command.Parameters["ClassName"]);
        Assert.Contains("ReleaseDate", command.PropertyNames);
        Assert.Contains("SMBIOSMajorVersion", command.PropertyNames);
        Assert.DoesNotContain(
            command.PropertyNames,
            property => property.Contains("Key", StringComparison.OrdinalIgnoreCase)
                || property.Contains("SecureBoot", StringComparison.OrdinalIgnoreCase));
    }

    private static WinRmCommandRecord Record(
        params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value));
}
