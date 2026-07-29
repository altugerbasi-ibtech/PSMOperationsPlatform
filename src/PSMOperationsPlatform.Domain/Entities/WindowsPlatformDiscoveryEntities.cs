using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class WindowsRoleInventory : Entity
{
    public WindowsRoleInventory(
        Guid id, Guid managedServerId, string roleKey, string name,
        DateTime capturedAt, string? displayName = null, string? parent = null,
        string? featureType = null) : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        RoleKey = InventoryEntityGuard.Required(roleKey, 260, nameof(roleKey));
        Name = InventoryEntityGuard.Required(name, 200, nameof(name));
        DisplayName = InventoryEntityGuard.Optional(displayName, 255, nameof(displayName));
        Parent = InventoryEntityGuard.Optional(parent, 200, nameof(parent));
        FeatureType = InventoryEntityGuard.Optional(featureType, 50, nameof(featureType));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsRoleInventory() { RoleKey = Name = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string RoleKey { get; private set; }
    public string Name { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Parent { get; private set; }
    public string? FeatureType { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
}

public sealed class WindowsFeatureInventory : Entity
{
    public WindowsFeatureInventory(
        Guid id, Guid managedServerId, string featureKey, string name,
        DateTime capturedAt, string? displayName = null, string? parent = null,
        string? restartRequired = null, string? featureType = null) : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        FeatureKey = InventoryEntityGuard.Required(featureKey, 260, nameof(featureKey));
        Name = InventoryEntityGuard.Required(name, 200, nameof(name));
        DisplayName = InventoryEntityGuard.Optional(displayName, 255, nameof(displayName));
        Parent = InventoryEntityGuard.Optional(parent, 200, nameof(parent));
        RestartRequired = InventoryEntityGuard.Optional(
            restartRequired, 50, nameof(restartRequired));
        FeatureType = InventoryEntityGuard.Optional(featureType, 50, nameof(featureType));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsFeatureInventory() { FeatureKey = Name = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string FeatureKey { get; private set; }
    public string Name { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Parent { get; private set; }
    public string? RestartRequired { get; private set; }
    public string? FeatureType { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
}

public sealed class WindowsIisPlatformInventory : Entity
{
    public WindowsIisPlatformInventory(
        Guid id, Guid managedServerId, string iisKey, bool installed,
        DateTime capturedAt, string? version = null) : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        IisKey = InventoryEntityGuard.Required(iisKey, 100, nameof(iisKey));
        Installed = installed;
        Version = InventoryEntityGuard.Optional(version, 100, nameof(version));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsIisPlatformInventory() { IisKey = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string IisKey { get; private set; }
    public bool Installed { get; private set; }
    public string? Version { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
}

public sealed class WindowsDotNetPlatformInventory : Entity
{
    public WindowsDotNetPlatformInventory(
        Guid id, Guid managedServerId, string dotNetKey, string category,
        string name, DateTime capturedAt, string? version = null,
        int? release = null) : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        DotNetKey = InventoryEntityGuard.Required(dotNetKey, 500, nameof(dotNetKey));
        Category = InventoryEntityGuard.Required(category, 50, nameof(category));
        Name = InventoryEntityGuard.Required(name, 255, nameof(name));
        Version = InventoryEntityGuard.Optional(version, 100, nameof(version));
        Release = release < 0 ? throw new ArgumentOutOfRangeException(nameof(release)) : release;
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsDotNetPlatformInventory()
    {
        DotNetKey = Category = Name = null!;
    }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string DotNetKey { get; private set; }
    public string Category { get; private set; }
    public string Name { get; private set; }
    public string? Version { get; private set; }
    public int? Release { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
}

public sealed class WindowsPowerShellPlatformInventory : Entity
{
    public WindowsPowerShellPlatformInventory(
        Guid id, Guid managedServerId, string powerShellKey, string edition,
        string path, DateTime capturedAt, string? version = null) : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        PowerShellKey = InventoryEntityGuard.Required(
            powerShellKey, 200, nameof(powerShellKey));
        Edition = InventoryEntityGuard.Required(edition, 50, nameof(edition));
        Path = InventoryEntityGuard.Required(path, 500, nameof(path));
        Version = InventoryEntityGuard.Optional(version, 100, nameof(version));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsPowerShellPlatformInventory()
    {
        PowerShellKey = Edition = Path = null!;
    }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string PowerShellKey { get; private set; }
    public string Edition { get; private set; }
    public string? Version { get; private set; }
    public string Path { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
}
