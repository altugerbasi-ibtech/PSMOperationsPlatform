namespace PSMOperationsPlatform.Architecture.Tests;

public sealed class ConfigurationValidationScopeTests
{
    [Fact]
    public void ConfigurationValidation_DoesNotCreatePersistenceOptions()
    {
        string configurationPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PSMOperationsPlatform.Infrastructure",
            "Configuration");

        string source = ReadCSharpFiles(configurationPath);

        Assert.DoesNotContain("class PersistenceOptions", source);
        Assert.DoesNotContain("record PersistenceOptions", source);
    }

    [Fact]
    public void ConfigurationDiagnostics_DoesNotIntroduceRuntimeMonitoring()
    {
        string configurationPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PSMOperationsPlatform.Infrastructure",
            "Configuration");

        string source = ReadCSharpFiles(configurationPath);

        Assert.DoesNotContain("IOptionsMonitor", source);
        Assert.DoesNotContain("PeriodicTimer", source);
        Assert.DoesNotContain("System.Threading.Timer", source);
        Assert.DoesNotContain("while (", source);
        Assert.DoesNotContain("Database.Migrate", source);
    }

    [Fact]
    public void ConfigurationDiagnostics_DoesNotReadOrStoreConfiguration()
    {
        string diagnosticsPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PSMOperationsPlatform.Infrastructure",
            "Configuration",
            "OperationsDatabaseStartupDiagnostics.cs");

        string source = File.ReadAllText(diagnosticsPath);

        Assert.DoesNotContain("IConfiguration", source);
        Assert.DoesNotContain("IOperationsDatabaseConfiguration", source);
        Assert.DoesNotContain("SqlConnectionStringBuilder", source);
        Assert.DoesNotContain("connectionString", source);
    }

    [Theory]
    [InlineData("PSMOperationsPlatform.Web")]
    [InlineData("PSMOperationsPlatform.SqlCollector")]
    public void ConfigurationValidation_IsNotRegisteredByProductionHosts(
        string hostProject)
    {
        string programPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            hostProject,
            "Program.cs");

        string source = File.ReadAllText(programPath);

        Assert.DoesNotContain(
            "AddOperationsDatabaseConfiguration",
            source);
    }

    [Fact]
    public void WindowsCollector_SelectsOperationsDatabaseCapability()
    {
        string hostPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PSMOperationsPlatform.WindowsCollector");

        string source = ReadCSharpFiles(hostPath);

        Assert.Contains("AddOperationsDatabasePersistence", source);
    }

    private static string ReadCSharpFiles(string path) =>
        string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

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
