using Microsoft.EntityFrameworkCore;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

[Collection(nameof(DesignTimeEnvironmentCollection))]
public sealed class OperationsDbContextFactoryTests
{
    private const string VariableName = "ConnectionStrings__OperationsDatabase";

    [Fact]
    public void CreateDbContextRequiresEnvironmentConfiguration()
    {
        WithEnvironmentValue(null, () =>
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => new OperationsDbContextFactory().CreateDbContext([]));

            Assert.Contains(VariableName, exception.Message);
            Assert.DoesNotContain("Server=", exception.Message);
        });
    }

    [Fact]
    public void CreateDbContextRejectsSqlAuthenticationWithoutExposingIt()
    {
        const string secret = "do-not-emit";
        WithEnvironmentValue(
            $"Server=sql.invalid;Database=Operations;User ID=tooling;Password={secret}",
            () =>
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => new OperationsDbContextFactory().CreateDbContext([]));

                Assert.Contains("Windows Integrated Authentication", exception.Message);
                Assert.DoesNotContain(secret, exception.Message);
            });
    }

    [Fact]
    public void CreateDbContextSanitizesMalformedConfiguration()
    {
        const string secret = "do-not-emit";
        WithEnvironmentValue(
            $"Unsupported Key={secret}",
            () =>
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => new OperationsDbContextFactory().CreateDbContext([]));

                Assert.Contains("configuration is invalid", exception.Message);
                Assert.DoesNotContain(secret, exception.Message);
            });
    }

    [Fact]
    public void CreateDbContextUsesIntegratedAuthenticationWithoutConnecting()
    {
        WithEnvironmentValue(
            "Server=sql.invalid;Database=Operations;Integrated Security=true",
            () =>
            {
                using OperationsDbContext context =
                    new OperationsDbContextFactory().CreateDbContext([]);

                Assert.True(context.Database.IsSqlServer());
                Assert.Contains(
                    "Integrated Security=True",
                    context.Database.GetConnectionString());
            });
    }

    private static void WithEnvironmentValue(
        string? value,
        Action assertion)
    {
        string? original = Environment.GetEnvironmentVariable(VariableName);

        try
        {
            Environment.SetEnvironmentVariable(VariableName, value);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(VariableName, original);
        }
    }
}

[CollectionDefinition(nameof(DesignTimeEnvironmentCollection), DisableParallelization = true)]
public sealed class DesignTimeEnvironmentCollection;
