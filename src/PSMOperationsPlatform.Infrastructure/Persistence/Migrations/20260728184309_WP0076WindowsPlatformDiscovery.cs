using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0076WindowsPlatformDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WindowsDotNetPlatformInventory",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DotNetKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Release = table.Column<int>(type: "int", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsDotNetPlatformInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowsDotNetPlatformInventory_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WindowsFeatureInventory",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Parent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RestartRequired = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FeatureType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsFeatureInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowsFeatureInventory_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WindowsIisPlatformInventory",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IisKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Installed = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsIisPlatformInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowsIisPlatformInventory_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WindowsPowerShellPlatformInventory",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PowerShellKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Edition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsPowerShellPlatformInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowsPowerShellPlatformInventory_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WindowsRoleInventory",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Parent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FeatureType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsRoleInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowsRoleInventory_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WindowsDotNetPlatformInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsDotNetPlatformInventory",
                columns: new[] { "ManagedServerId", "InventoryRunId" });

            migrationBuilder.CreateIndex(
                name: "UX_WindowsDotNetPlatformInventory_ManagedServer_DotNetKey",
                schema: "inventory",
                table: "WindowsDotNetPlatformInventory",
                columns: new[] { "ManagedServerId", "DotNetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WindowsFeatureInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsFeatureInventory",
                columns: new[] { "ManagedServerId", "InventoryRunId" });

            migrationBuilder.CreateIndex(
                name: "UX_WindowsFeatureInventory_ManagedServer_FeatureKey",
                schema: "inventory",
                table: "WindowsFeatureInventory",
                columns: new[] { "ManagedServerId", "FeatureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WindowsIisPlatformInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsIisPlatformInventory",
                columns: new[] { "ManagedServerId", "InventoryRunId" });

            migrationBuilder.CreateIndex(
                name: "UX_WindowsIisPlatformInventory_ManagedServer_IisKey",
                schema: "inventory",
                table: "WindowsIisPlatformInventory",
                columns: new[] { "ManagedServerId", "IisKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WindowsPowerShellPlatformInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsPowerShellPlatformInventory",
                columns: new[] { "ManagedServerId", "InventoryRunId" });

            migrationBuilder.CreateIndex(
                name: "UX_WindowsPowerShellPlatformInventory_ManagedServer_PowerShellKey",
                schema: "inventory",
                table: "WindowsPowerShellPlatformInventory",
                columns: new[] { "ManagedServerId", "PowerShellKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WindowsRoleInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsRoleInventory",
                columns: new[] { "ManagedServerId", "InventoryRunId" });

            migrationBuilder.CreateIndex(
                name: "UX_WindowsRoleInventory_ManagedServer_RoleKey",
                schema: "inventory",
                table: "WindowsRoleInventory",
                columns: new[] { "ManagedServerId", "RoleKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WindowsDotNetPlatformInventory",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "WindowsFeatureInventory",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "WindowsIisPlatformInventory",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "WindowsPowerShellPlatformInventory",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "WindowsRoleInventory",
                schema: "inventory");
        }
    }
}
