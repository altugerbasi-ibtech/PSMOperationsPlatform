using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0075NetworkAdapterIpv4Inventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WindowsNetworkAdapterInventory_ManagedServer_StableSourceKey",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.RenameColumn(
                name: "StableSourceKey",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                newName: "AdapterKey");

            migrationBuilder.CreateIndex(
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                name: "UX_WindowsNetworkAdapterInventory_ManagedServer_AdapterKey",
                columns: new[] { "ManagedServerId", "AdapterKey" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "UX_WindowsIpv4AddressInventory_ManagedServer_StableSourceKey",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory");

            migrationBuilder.RenameColumn(
                name: "StableSourceKey",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory",
                newName: "Ipv4Key");

            migrationBuilder.CreateIndex(
                schema: "inventory",
                table: "WindowsIpv4AddressInventory",
                name: "UX_WindowsIpv4AddressInventory_ManagedServer_Ipv4Key",
                columns: new[] { "ManagedServerId", "Ipv4Key" },
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "FriendlyName",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterfaceGuid",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InterfaceIndex",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PhysicalAdapter",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PnpDeviceId",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "AdapterKey",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                EXEC(N'
                    UPDATE [ip]
                    SET [AdapterKey] = [adapter].[AdapterKey]
                    FROM [inventory].[WindowsIpv4AddressInventory] AS [ip]
                    INNER JOIN [inventory].[WindowsNetworkAdapterInventory] AS [adapter]
                        ON [adapter].[Id] = [ip].[NetworkAdapterInventoryId]
                       AND [adapter].[ManagedServerId] = [ip].[ManagedServerId];

                    IF EXISTS (
                        SELECT 1
                        FROM [inventory].[WindowsIpv4AddressInventory]
                        WHERE [AdapterKey] IS NULL
                    )
                        THROW 51000, N''WP-007.5 AdapterKey backfill failed.'', 1;
                ');
                """);

            migrationBuilder.Sql(
                """
                EXEC(N'
                    ALTER TABLE [inventory].[WindowsIpv4AddressInventory]
                    ALTER COLUMN [AdapterKey] nvarchar(200) NOT NULL;
                ');
                """);

            migrationBuilder.AddColumn<string>(
                name: "DefaultGateway",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DnsRegistrationEnabled",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FriendlyName",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropColumn(
                name: "InterfaceGuid",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropColumn(
                name: "InterfaceIndex",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropColumn(
                name: "PhysicalAdapter",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropColumn(
                name: "PnpDeviceId",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropColumn(
                name: "AdapterKey",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory");

            migrationBuilder.DropColumn(
                name: "DefaultGateway",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory");

            migrationBuilder.DropColumn(
                name: "DnsRegistrationEnabled",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory");

            migrationBuilder.DropIndex(
                name: "UX_WindowsNetworkAdapterInventory_ManagedServer_AdapterKey",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.RenameColumn(
                name: "AdapterKey",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                newName: "StableSourceKey");

            migrationBuilder.CreateIndex(
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory",
                name: "UX_WindowsNetworkAdapterInventory_ManagedServer_StableSourceKey",
                columns: new[] { "ManagedServerId", "StableSourceKey" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "UX_WindowsIpv4AddressInventory_ManagedServer_Ipv4Key",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory");

            migrationBuilder.RenameColumn(
                name: "Ipv4Key",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory",
                newName: "StableSourceKey");

            migrationBuilder.CreateIndex(
                schema: "inventory",
                table: "WindowsIpv4AddressInventory",
                name: "UX_WindowsIpv4AddressInventory_ManagedServer_StableSourceKey",
                columns: new[] { "ManagedServerId", "StableSourceKey" },
                unique: true);
        }
    }
}
