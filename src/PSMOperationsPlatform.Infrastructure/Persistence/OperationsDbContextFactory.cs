using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Data.SqlClient;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__OperationsDatabase";

    public OperationsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"{ConnectionStringEnvironmentVariable} is required for EF Core design-time tooling.");

        SqlConnectionStringBuilder connection;
        try
        {
            connection = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                "EF Core design-time database configuration is invalid.");
        }

        if (!connection.IntegratedSecurity ||
            !string.IsNullOrWhiteSpace(connection.UserID) ||
            !string.IsNullOrWhiteSpace(connection.Password))
        {
            throw new InvalidOperationException(
                "EF Core design-time tooling requires Windows Integrated Authentication.");
        }

        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;

        return new OperationsDbContext(options);
    }
}
