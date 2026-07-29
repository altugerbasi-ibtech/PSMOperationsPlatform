using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OperationsDbContext))]
[Migration("20260728093000_WP0071CoreInventoryReliability")]
public partial class WP0071CoreInventoryReliability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastInventoryAttemptAt", schema: "configuration",
            table: "ManagedServer", type: "datetime2(7)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "LastInventorySuccessAt", schema: "configuration",
            table: "ManagedServer", type: "datetime2(7)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "NextInventoryAttemptAt", schema: "configuration",
            table: "ManagedServer", type: "datetime2(7)", nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "ConsecutiveInventoryFailures", schema: "configuration",
            table: "ManagedServer", type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>(
            name: "LastInventoryFailureCategory", schema: "configuration",
            table: "ManagedServer", type: "nvarchar(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "InventoryVersion", schema: "configuration",
            table: "ManagedServer", type: "bigint", nullable: false, defaultValue: 0L);

        migrationBuilder.Sql(
            """
            EXEC(N'
                ALTER TABLE [configuration].[ManagedServer]
                ADD CONSTRAINT [CK_ManagedServer_InventoryFailures_NonNegative]
                CHECK ([ConsecutiveInventoryFailures] >= 0);
            ');
            """);
        migrationBuilder.Sql(
            """
            EXEC(N'
                ALTER TABLE [configuration].[ManagedServer]
                ADD CONSTRAINT [CK_ManagedServer_InventoryVersion_NonNegative]
                CHECK ([InventoryVersion] >= 0);
            ');
            """);
        migrationBuilder.Sql(
            """
            EXEC(N'
                ALTER TABLE [configuration].[ManagedServer]
                ADD CONSTRAINT [CK_ManagedServer_LastInventoryFailureCategory]
                CHECK ([LastInventoryFailureCategory] IS NULL OR [LastInventoryFailureCategory] IN
                    (''CollectionFailure'', ''ParsingFailure'', ''ValidationFailure'',
                     ''PersistenceFailure'', ''Timeout'', ''Unexpected''));
            ');
            """);
        migrationBuilder.Sql(
            """
            EXEC(N'
                CREATE INDEX [IX_ManagedServer_InventoryEligibility]
                ON [configuration].[ManagedServer] ([IsEnabled], [NextInventoryAttemptAt]);
            ');
            """);

        migrationBuilder.DropPrimaryKey(
            name: "PK_WindowsMemoryInventory",
            schema: "inventory", table: "WindowsMemoryInventory");
        migrationBuilder.DropCheckConstraint(
            name: "CK_WindowsMemoryInventory_TotalPhysicalMemoryBytes_NonNegative",
            schema: "inventory", table: "WindowsMemoryInventory");
        migrationBuilder.RenameColumn(
            name: "TotalPhysicalMemoryBytes", schema: "inventory",
            table: "WindowsMemoryInventory", newName: "CapacityBytes");
        migrationBuilder.AddColumn<Guid>(
            name: "Id", schema: "inventory", table: "WindowsMemoryInventory",
            type: "uniqueidentifier", nullable: false,
            defaultValue: Guid.Empty);
        migrationBuilder.AddColumn<string>(
            name: "ModuleKey", schema: "inventory", table: "WindowsMemoryInventory",
            type: "nvarchar(200)", maxLength: 200, nullable: false,
            defaultValue: "");
        migrationBuilder.AddColumn<string>(
            name: "DeviceLocator", schema: "inventory", table: "WindowsMemoryInventory",
            type: "nvarchar(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "BankLabel", schema: "inventory", table: "WindowsMemoryInventory",
            type: "nvarchar(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "SpeedMHz", schema: "inventory", table: "WindowsMemoryInventory",
            type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "ConfiguredClockSpeedMHz", schema: "inventory",
            table: "WindowsMemoryInventory", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "Manufacturer", schema: "inventory", table: "WindowsMemoryInventory",
            type: "nvarchar(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "PartNumber", schema: "inventory", table: "WindowsMemoryInventory",
            type: "nvarchar(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "SerialNumber", schema: "inventory", table: "WindowsMemoryInventory",
            type: "nvarchar(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "FormFactor", schema: "inventory", table: "WindowsMemoryInventory",
            type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "MemoryType", schema: "inventory", table: "WindowsMemoryInventory",
            type: "int", nullable: true);
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion", schema: "inventory", table: "WindowsMemoryInventory",
            type: "rowversion", rowVersion: true, nullable: false,
            defaultValue: Array.Empty<byte>());

        migrationBuilder.Sql(
            """
            EXEC(N'
                UPDATE [inventory].[WindowsMemoryInventory]
                SET [Id] = NEWID(),
                    [ModuleKey] = N''legacy-total'';
            ');
            """);
        migrationBuilder.Sql(
            """
            EXEC(N'
                ALTER TABLE [inventory].[WindowsMemoryInventory]
                ADD CONSTRAINT [PK_WindowsMemoryInventory]
                PRIMARY KEY ([Id]);
            ');
            """);
        migrationBuilder.Sql(
            """
            EXEC(N'
                CREATE UNIQUE INDEX [UX_WindowsMemoryInventory_ManagedServer_ModuleKey]
                ON [inventory].[WindowsMemoryInventory] ([ManagedServerId], [ModuleKey]);
            ');
            """);
        migrationBuilder.Sql(
            """
            EXEC(N'
                ALTER TABLE [inventory].[WindowsMemoryInventory]
                ADD CONSTRAINT [CK_WindowsMemoryInventory_CapacityBytes_Positive]
                CHECK ([CapacityBytes] > 0);
            ');
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId",
            schema: "inventory", table: "WindowsIpv4AddressInventory");
        migrationBuilder.AddUniqueConstraint(
            name: "AK_WindowsNetworkAdapterInventory_Id_ManagedServerId",
            schema: "inventory", table: "WindowsNetworkAdapterInventory",
            columns: new[] { "Id", "ManagedServerId" });
        migrationBuilder.CreateIndex(
            name: "IX_WindowsIpv4AddressInventory_NetworkAdapterInventoryId_ManagedServerId",
            schema: "inventory", table: "WindowsIpv4AddressInventory",
            columns: new[] { "NetworkAdapterInventoryId", "ManagedServerId" });
        migrationBuilder.Sql(
            """
            EXEC(N'
                ALTER TABLE [inventory].[WindowsIpv4AddressInventory]
                ADD CONSTRAINT [FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId_ManagedServerId]
                FOREIGN KEY ([NetworkAdapterInventoryId], [ManagedServerId])
                REFERENCES [inventory].[WindowsNetworkAdapterInventory] ([Id], [ManagedServerId])
                ON DELETE NO ACTION;
            ');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId_ManagedServerId",
            schema: "inventory", table: "WindowsIpv4AddressInventory");
        migrationBuilder.DropIndex(
            name: "IX_WindowsIpv4AddressInventory_NetworkAdapterInventoryId_ManagedServerId",
            schema: "inventory", table: "WindowsIpv4AddressInventory");
        migrationBuilder.DropUniqueConstraint(
            name: "AK_WindowsNetworkAdapterInventory_Id_ManagedServerId",
            schema: "inventory", table: "WindowsNetworkAdapterInventory");
        migrationBuilder.AddForeignKey(
            name: "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId",
            schema: "inventory", table: "WindowsIpv4AddressInventory",
            column: "NetworkAdapterInventoryId",
            principalSchema: "inventory", principalTable: "WindowsNetworkAdapterInventory",
            principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropPrimaryKey(
            name: "PK_WindowsMemoryInventory", schema: "inventory",
            table: "WindowsMemoryInventory");
        migrationBuilder.DropIndex(
            name: "UX_WindowsMemoryInventory_ManagedServer_ModuleKey",
            schema: "inventory", table: "WindowsMemoryInventory");
        migrationBuilder.DropCheckConstraint(
            name: "CK_WindowsMemoryInventory_CapacityBytes_Positive",
            schema: "inventory", table: "WindowsMemoryInventory");
        foreach (string column in new[]
        {
            "Id", "ModuleKey", "DeviceLocator", "BankLabel", "SpeedMHz",
            "ConfiguredClockSpeedMHz", "Manufacturer", "PartNumber",
            "SerialNumber", "FormFactor", "MemoryType", "RowVersion",
        })
        {
            migrationBuilder.DropColumn(
                name: column, schema: "inventory", table: "WindowsMemoryInventory");
        }
        migrationBuilder.RenameColumn(
            name: "CapacityBytes", schema: "inventory",
            table: "WindowsMemoryInventory", newName: "TotalPhysicalMemoryBytes");
        migrationBuilder.AddPrimaryKey(
            name: "PK_WindowsMemoryInventory", schema: "inventory",
            table: "WindowsMemoryInventory", column: "ManagedServerId");
        migrationBuilder.AddCheckConstraint(
            name: "CK_WindowsMemoryInventory_TotalPhysicalMemoryBytes_NonNegative",
            schema: "inventory", table: "WindowsMemoryInventory",
            sql: "[TotalPhysicalMemoryBytes] >= 0");

        migrationBuilder.DropIndex(
            name: "IX_ManagedServer_InventoryEligibility",
            schema: "configuration", table: "ManagedServer");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_InventoryFailures_NonNegative",
            schema: "configuration", table: "ManagedServer");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_InventoryVersion_NonNegative",
            schema: "configuration", table: "ManagedServer");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ManagedServer_LastInventoryFailureCategory",
            schema: "configuration", table: "ManagedServer");
        foreach (string column in new[]
        {
            "LastInventoryAttemptAt", "LastInventorySuccessAt",
            "NextInventoryAttemptAt", "ConsecutiveInventoryFailures",
            "LastInventoryFailureCategory", "InventoryVersion",
        })
        {
            migrationBuilder.DropColumn(
                name: column, schema: "configuration", table: "ManagedServer");
        }
    }
}
