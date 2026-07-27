using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OperationsDbContext))]
[Migration("20260727160000_AddManagedServerWinRmConfiguration")]
public partial class AddManagedServerWinRmConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WinRmHttpPort",
            schema: "configuration",
            table: "ManagedServer",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "WinRmHttpsPort",
            schema: "configuration",
            table: "ManagedServer",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "WinRmProbeTimeoutSeconds",
            schema: "configuration",
            table: "ManagedServer",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WinRmTransportMode",
            schema: "configuration",
            table: "ManagedServer",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE [configuration].[ManagedServer]
            SET [WinRmTransportMode] = N'Auto',
                [WinRmHttpsPort] = 5986,
                [WinRmHttpPort] = 5985,
                [WinRmProbeTimeoutSeconds] = 10
            WHERE [WinRmTransportMode] IS NULL
               OR [WinRmHttpsPort] IS NULL
               OR [WinRmHttpPort] IS NULL
               OR [WinRmProbeTimeoutSeconds] IS NULL;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "WinRmHttpPort",
            schema: "configuration",
            table: "ManagedServer",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "WinRmHttpsPort",
            schema: "configuration",
            table: "ManagedServer",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "WinRmProbeTimeoutSeconds",
            schema: "configuration",
            table: "ManagedServer",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "WinRmTransportMode",
            schema: "configuration",
            table: "ManagedServer",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(20)",
            oldMaxLength: 20,
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ManagedServer_WinRmHttpPort_Range",
            schema: "configuration",
            table: "ManagedServer",
            sql: "[WinRmHttpPort] >= 1 AND [WinRmHttpPort] <= 65535");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ManagedServer_WinRmHttpsPort_Range",
            schema: "configuration",
            table: "ManagedServer",
            sql: "[WinRmHttpsPort] >= 1 AND [WinRmHttpsPort] <= 65535");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ManagedServer_WinRmProbeTimeout_Positive",
            schema: "configuration",
            table: "ManagedServer",
            sql: "[WinRmProbeTimeoutSeconds] > 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ManagedServer_WinRmTransportMode",
            schema: "configuration",
            table: "ManagedServer",
            sql: "[WinRmTransportMode] IN ('Auto', 'HttpsOnly', 'HttpOnly')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_WinRmHttpPort_Range",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_WinRmHttpsPort_Range",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_WinRmProbeTimeout_Positive",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_WinRmTransportMode",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "WinRmHttpPort",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "WinRmHttpsPort",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "WinRmProbeTimeoutSeconds",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "WinRmTransportMode",
            schema: "configuration",
            table: "ManagedServer");
    }
}
