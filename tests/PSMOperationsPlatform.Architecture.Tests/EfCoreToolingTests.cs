using System.Xml.Linq;

namespace PSMOperationsPlatform.Architecture.Tests;

public sealed class EfCoreToolingTests
{
    [Fact]
    public void WindowsCollectorReferencesCentrallyVersionedPrivateEfDesignPackage()
    {
        string root = FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(
            root,
            "src",
            "PSMOperationsPlatform.WindowsCollector",
            "PSMOperationsPlatform.WindowsCollector.csproj"));

        XElement reference = Assert.Single(
            project.Descendants("PackageReference"),
            element =>
                (string?)element.Attribute("Include") ==
                "Microsoft.EntityFrameworkCore.Design");

        Assert.Null(reference.Attribute("Version"));
        Assert.Equal("all", (string?)reference.Element("PrivateAssets"));
        Assert.Equal(
            "runtime; build; native; contentfiles; analyzers; buildtransitive",
            (string?)reference.Element("IncludeAssets"));

        XDocument packages = XDocument.Load(Path.Combine(
            root,
            "Directory.Packages.props"));
        XElement version = Assert.Single(
            packages.Descendants("PackageVersion"),
            element =>
                (string?)element.Attribute("Include") ==
                "Microsoft.EntityFrameworkCore.Design");

        Assert.Equal("10.0.9", (string?)version.Attribute("Version"));
    }

    [Fact]
    public void DesignTimeFactoryHasNoEmbeddedConnectionOrMigrationExecution()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PSMOperationsPlatform.Infrastructure",
            "Persistence",
            "OperationsDbContextFactory.cs"));

        Assert.Contains("ConnectionStrings__OperationsDatabase", source);
        Assert.DoesNotContain("Server=", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database.Migrate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Database.Update", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PSMOperationsPlatform.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
