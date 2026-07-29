using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0073ProcessorInventoryContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StableSourceKey",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                newName: "ProcessorKey");

            migrationBuilder.RenameIndex(
                name: "UX_WindowsProcessorInventory_ManagedServer_StableSourceKey",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                newName: "UX_WindowsProcessorInventory_ManagedServer_ProcessorKey");

            migrationBuilder.AddColumn<int>(
                name: "AddressWidth",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Architecture",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentClockSpeedMhz",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DataWidth",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessorId",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<bool>(
                name: "SecondLevelAddressTranslationExtensions",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocketDesignation",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VirtualizationFirmwareEnabled",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VmMonitorModeExtensions",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressWidth",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "Architecture",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "CurrentClockSpeedMhz",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "DataWidth",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "ProcessorId",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "SecondLevelAddressTranslationExtensions",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "SocketDesignation",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "VirtualizationFirmwareEnabled",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "VmMonitorModeExtensions",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.RenameColumn(
                name: "ProcessorKey",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                newName: "StableSourceKey");

            migrationBuilder.RenameIndex(
                name: "UX_WindowsProcessorInventory_ManagedServer_ProcessorKey",
                schema: "inventory",
                table: "WindowsProcessorInventory",
                newName: "UX_WindowsProcessorInventory_ManagedServer_StableSourceKey");
        }
    }
}
