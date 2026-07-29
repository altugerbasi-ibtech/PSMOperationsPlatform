using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OperationsDbContext))]
[Migration("20260727190000_AddManagedServerConnectivityState")]
public partial class AddManagedServerConnectivityState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ConsecutiveConnectivityFailures",
            schema: "configuration",
            table: "ManagedServer",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastConnectivityAttemptAt",
            schema: "configuration",
            table: "ManagedServer",
            type: "datetime2(3)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastConnectivityFailureCategory",
            schema: "configuration",
            table: "ManagedServer",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastConnectivityState",
            schema: "configuration",
            table: "ManagedServer",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastConnectivitySuccessAt",
            schema: "configuration",
            table: "ManagedServer",
            type: "datetime2(3)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastSuccessfulTransport",
            schema: "configuration",
            table: "ManagedServer",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            schema: "configuration",
            table: "ManagedServer",
            type: "rowversion",
            rowVersion: true,
            nullable: false);

        migrationBuilder.Sql(
            """
            EXEC(N'
            UPDATE [configuration].[ManagedServer]
            SET [LastConnectivityState] = N''Unknown'',
                [ConsecutiveConnectivityFailures] = 0
            WHERE [LastConnectivityState] IS NULL
               OR [ConsecutiveConnectivityFailures] IS NULL;
            ');
            """);

        migrationBuilder.AlterColumn<int>(
            name: "ConsecutiveConnectivityFailures",
            schema: "configuration",
            table: "ManagedServer",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LastConnectivityState",
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
            name: "CK_ManagedServer_ConnectivityFailures_NonNegative",
            schema: "configuration",
            table: "ManagedServer",
            sql: "[ConsecutiveConnectivityFailures] >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ManagedServer_LastConnectivityState",
            schema: "configuration",
            table: "ManagedServer",
            sql: "[LastConnectivityState] IN ('Unknown', 'Reachable', 'Unreachable')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ManagedServer_LastConnectivityFailureCategory",
            schema: "configuration",
            table: "ManagedServer",
            sql: "[LastConnectivityFailureCategory] IS NULL OR [LastConnectivityFailureCategory] IN ('DnsFailure', 'ConnectionRefused', 'Timeout', 'TlsFailure', 'AuthenticationFailure', 'AuthorizationFailure', 'WinRmUnavailable', 'ProtocolFailure', 'Unexpected')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ManagedServer_LastSuccessfulTransport",
            schema: "configuration",
            table: "ManagedServer",
            sql: "[LastSuccessfulTransport] IS NULL OR [LastSuccessfulTransport] IN ('Https', 'Http')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_ConnectivityFailures_NonNegative",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_LastConnectivityState",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_LastConnectivityFailureCategory",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_LastSuccessfulTransport",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "ConsecutiveConnectivityFailures",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "LastConnectivityAttemptAt",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "LastConnectivityFailureCategory",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "LastConnectivityState",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "LastConnectivitySuccessAt",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "LastSuccessfulTransport",
            schema: "configuration",
            table: "ManagedServer");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            schema: "configuration",
            table: "ManagedServer");
    }
}
