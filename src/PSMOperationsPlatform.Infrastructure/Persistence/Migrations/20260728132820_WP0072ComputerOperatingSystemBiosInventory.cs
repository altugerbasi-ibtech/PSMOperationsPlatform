using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0072ComputerOperatingSystemBiosInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentTimeZoneMinutes",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstallationType",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Locale",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductType",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemDrive",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowsDirectory",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DomainRole",
                schema: "inventory",
                table: "WindowsComputerInventory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVirtualMachine",
                schema: "inventory",
                table: "WindowsComputerInventory",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemType",
                schema: "inventory",
                table: "WindowsComputerInventory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Uuid",
                schema: "inventory",
                table: "WindowsComputerInventory",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WindowsBiosInventory",
                schema: "inventory",
                columns: table => new
                {
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmbiosBiosVersion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmbiosMajorVersion = table.Column<int>(type: "int", nullable: true),
                    SmbiosMinorVersion = table.Column<int>(type: "int", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsBiosInventory", x => x.ManagedServerId);
                    table.ForeignKey(
                        name: "FK_WindowsBiosInventory_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                EXEC(N'
                    CREATE INDEX [IX_WindowsBiosInventory_ManagedServer_InventoryRun]
                    ON [inventory].[WindowsBiosInventory] ([ManagedServerId], [InventoryRunId]);
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WindowsBiosInventory",
                schema: "inventory");

            migrationBuilder.DropColumn(
                name: "CurrentTimeZoneMinutes",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory");

            migrationBuilder.DropColumn(
                name: "InstallationType",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory");

            migrationBuilder.DropColumn(
                name: "Locale",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory");

            migrationBuilder.DropColumn(
                name: "ProductType",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory");

            migrationBuilder.DropColumn(
                name: "SystemDrive",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory");

            migrationBuilder.DropColumn(
                name: "WindowsDirectory",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory");

            migrationBuilder.DropColumn(
                name: "DomainRole",
                schema: "inventory",
                table: "WindowsComputerInventory");

            migrationBuilder.DropColumn(
                name: "IsVirtualMachine",
                schema: "inventory",
                table: "WindowsComputerInventory");

            migrationBuilder.DropColumn(
                name: "SystemType",
                schema: "inventory",
                table: "WindowsComputerInventory");

            migrationBuilder.DropColumn(
                name: "Uuid",
                schema: "inventory",
                table: "WindowsComputerInventory");
        }
    }
}
