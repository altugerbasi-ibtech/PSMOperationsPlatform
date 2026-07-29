using System.Text.RegularExpressions;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class WindowsCollectorArchitectureTests
{
    [Fact]
    public void HostFoundationContainsNoDeferredBehaviorOrUnsafeTime()
    {
        string source = ReadProjectSource(
            "src",
            "PSMOperationsPlatform.WindowsCollector");

        Assert.DoesNotContain("DateTime.Now", source);
        Assert.DoesNotContain("DateTime.UtcNow", source);
        Assert.DoesNotContain("IOptionsMonitor", source);
        Assert.DoesNotContain("Database.Migrate", source);
        Assert.DoesNotContain("EnsureCreated", source);
        Assert.DoesNotContain("TrustedHosts", source);
        Assert.DoesNotContain("PSCredential", source);
        Assert.DoesNotContain("IQueryable<", source);
        Assert.DoesNotContain("Process.Start", source);
        Assert.DoesNotContain("SkipCACheck", source);
        Assert.DoesNotContain("SkipCNCheck", source);
        Assert.DoesNotContain("RemoteCertificateValidationCallback", source);
        Assert.DoesNotContain("GETDATE(", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SYSDATETIME", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UnitOfWork", source);
        Assert.DoesNotContain("MediatR", source);
        Assert.DoesNotContain("ConnectivityHistory", source);
        Assert.DoesNotContain("AlertService", source);
        Assert.DoesNotContain("SaveChangesAsync(CancellationToken.None", source);
    }

    [Fact]
    public void WorkerDoesNotCaptureOperationsDbContext()
    {
        string worker = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "PSMOperationsPlatform.WindowsCollector",
                "Worker.cs"));

        Assert.DoesNotContain("OperationsDbContext", worker);
        Assert.Contains("CreateAsyncScope", worker);
    }

    [Fact]
    public void WindowsCollectorReferencesNoOtherProductionHost()
    {
        string project = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "PSMOperationsPlatform.WindowsCollector",
                "PSMOperationsPlatform.WindowsCollector.csproj"));

        Assert.DoesNotContain("PSMOperationsPlatform.Web", project);
        Assert.DoesNotContain("PSMOperationsPlatform.SqlCollector", project);
        Assert.DoesNotContain("WindowsActionExecutor", project);
    }

    [Fact]
    public void CollectorEventIdsAreUniqueAndReserved()
    {
        string logSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "PSMOperationsPlatform.WindowsCollector",
                "WindowsCollectorLog.cs"));

        int[] eventIds = Regex
            .Matches(logSource, @"const int \w+Id = (?<id>\d+);")
            .Select(match => int.Parse(match.Groups["id"].Value))
            .ToArray();

        Assert.NotEmpty(eventIds);
        Assert.Equal(eventIds.Length, eventIds.Distinct().Count());
        Assert.All(eventIds, eventId => Assert.InRange(eventId, 2300, 2399));
        Assert.Contains(
            "EventName = \"PollingCycleCompleted\",\r\n" +
            "        Level = LogLevel.Debug",
            logSource);
    }

    [Fact]
    public void HostRegistersOfficialWindowsServiceSupport()
    {
        string hostSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "PSMOperationsPlatform.WindowsCollector",
                "WindowsCollectorHost.cs"));
        string packages = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Directory.Packages.props"));

        Assert.Contains("AddWindowsService", hostSource);
        Assert.Contains(
            "Microsoft.Extensions.Hosting.WindowsServices",
            packages);
    }

    [Fact]
    public void PowerShellDependencyIsConfinedToWindowsCollector()
    {
        string packages = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Directory.Packages.props"));
        string collectorProject = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "PSMOperationsPlatform.WindowsCollector",
                "PSMOperationsPlatform.WindowsCollector.csproj"));

        Assert.Contains(
            "Microsoft.PowerShell.SDK\" Version=\"7.6.4\"",
            packages);
        Assert.Contains("Microsoft.PowerShell.SDK", collectorProject);

        foreach (string projectName in new[]
        {
            "PSMOperationsPlatform.Domain",
            "PSMOperationsPlatform.Application",
            "PSMOperationsPlatform.Contracts"
        })
        {
            string project = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    "src",
                    projectName,
                    $"{projectName}.csproj"));
            Assert.DoesNotContain("Microsoft.PowerShell.SDK", project);
        }
    }

    [Fact]
    public void WinRmSessionsPermitOnlyPortQualifiedKerberosProcessIdentity()
    {
        string source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "PSMOperationsPlatform.WindowsCollector",
                "PowerShellWinRmSession.cs"));

        Assert.Contains(
            "AuthenticationMechanism = AuthenticationMechanism.Kerberos",
            source);
        Assert.Contains("IncludePortInSPN = true", source);
        Assert.DoesNotContain("AuthenticationMechanism.Negotiate", source);
        Assert.DoesNotContain("AuthenticationMechanism.Basic", source);
        Assert.DoesNotContain("AuthenticationMechanism.Digest", source);
        Assert.DoesNotContain("AuthenticationMechanism.Credssp", source);
        Assert.DoesNotContain("AuthenticationMechanism.Ntlm", source);
        Assert.DoesNotContain("TrustedHosts", source);
        Assert.DoesNotContain("PSCredential", source);
    }

    [Fact]
    public void InventoryFoundationHasNoPersistenceOrDynamicDiscovery()
    {
        string source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "PSMOperationsPlatform.WindowsCollector",
                "WindowsInventoryOrchestration.cs"));

        Assert.DoesNotContain("OperationsDbContext", source);
        Assert.DoesNotContain("IServiceProvider", source);
        Assert.DoesNotContain("IWinRmSessionFactory", source);
        Assert.DoesNotContain("Assembly", source);
        Assert.DoesNotContain("Reflection", source);
        Assert.DoesNotContain("Activator", source);
        Assert.DoesNotContain("SaveChanges", source);
    }

    private static string ReadProjectSource(params string[] parts)
    {
        string path = Path.Combine([FindRepositoryRoot(), .. parts]);
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(
                Path.Combine(directory.FullName, "PSMOperationsPlatform.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root.");
    }
}
