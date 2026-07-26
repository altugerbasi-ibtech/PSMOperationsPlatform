using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__OperationsDatabase";

    public OperationsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=PSMOperationsPlatform;Integrated Security=true;TrustServerCertificate=true";

        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new OperationsDbContext(options);
    }
}
