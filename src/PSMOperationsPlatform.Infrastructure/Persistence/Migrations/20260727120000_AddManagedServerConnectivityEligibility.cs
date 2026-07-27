using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OperationsDbContext))]
[Migration("20260727120000_AddManagedServerConnectivityEligibility")]
public partial class AddManagedServerConnectivityEligibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "NextConnectivityAttemptAt",
            schema: "configuration",
            table: "ManagedServer",
            type: "datetime2(3)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ManagedServer_Eligibility",
            schema: "configuration",
            table: "ManagedServer",
            columns: new[] { "IsEnabled", "NextConnectivityAttemptAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ManagedServer_Eligibility",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "NextConnectivityAttemptAt",
            schema: "configuration",
            table: "ManagedServer");
    }
}
