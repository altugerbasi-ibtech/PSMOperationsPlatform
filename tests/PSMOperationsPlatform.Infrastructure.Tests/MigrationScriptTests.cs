using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class MigrationScriptTests
{
    [Fact]
    public void InitialCreateGeneratesApprovedSqlServerObjects()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ScriptOnly;Integrated Security=true")
            .Options;
        using var context = new OperationsDbContext(options);

        string script = context.GetService<IMigrator>().GenerateScript();

        foreach (string schema in new[]
                 {
                     "configuration",
                     "collection",
                     "monitoring",
                     "inventory",
                     "operations",
                     "audit"
                 })
        {
            Assert.Contains($"CREATE SCHEMA [{schema}]", script, StringComparison.Ordinal);
        }

        foreach (string table in new[]
                 {
                     "[configuration].[ManagedServer]",
                     "[collection].[CollectorNode]",
                     "[monitoring].[CollectorHeartbeat]",
                     "[collection].[CollectorRun]",
                     "[inventory].[InventorySnapshot]",
                     "[operations].[CommandQueueItem]",
                     "[audit].[AuditLog]"
                 })
        {
            Assert.Equal(1, CountOccurrences(script, $"CREATE TABLE {table}"));
        }
        Assert.Contains("ISJSON([PayloadJson]) = 1", script, StringComparison.Ordinal);
        Assert.Contains("[RowVersion] rowversion NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains(
            "ADD [NextConnectivityAttemptAt] datetime2(3) NULL",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX [IX_ManagedServer_Eligibility]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "SET [WinRmTransportMode] = N'Auto'",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[WinRmHttpsPort] = 5986",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[WinRmHttpPort] = 5985",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[WinRmProbeTimeoutSeconds] = 10",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_ManagedServer_WinRmTransportMode",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "SET [LastConnectivityState] = N'Unknown'",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ConsecutiveConnectivityFailures] = 0",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "ADD [RowVersion] rowversion NOT NULL",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_ManagedServer_ConnectivityFailures_NonNegative",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_ManagedServer_LastConnectivityFailureCategory",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_ManagedServer_LastSuccessfulTransport",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ConnectivityHistory",
            script,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "RawException",
            script,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE LOGIN", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [configuration]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [collection]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [monitoring]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [inventory]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [operations]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [audit]", script, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string value, string fragment)
    {
        int count = 0;
        int position = 0;
        while ((position = value.IndexOf(fragment, position, StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += fragment.Length;
        }

        return count;
    }
}
