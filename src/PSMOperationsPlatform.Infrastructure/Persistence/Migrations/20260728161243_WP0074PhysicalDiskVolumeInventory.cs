using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0074PhysicalDiskVolumeInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WindowsVolumeInventory_ManagedServer_StableSourceKey",
                schema: "inventory",
                table: "WindowsVolumeInventory");

            migrationBuilder.RenameColumn(
                name: "StableSourceKey",
                schema: "inventory",
                table: "WindowsVolumeInventory",
                newName: "VolumeKey");

            migrationBuilder.CreateIndex(
                schema: "inventory",
                table: "WindowsVolumeInventory",
                name: "UX_WindowsVolumeInventory_ManagedServer_VolumeKey",
                columns: new[] { "ManagedServerId", "VolumeKey" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "UX_WindowsDiskInventory_ManagedServer_StableSourceKey",
                schema: "inventory",
                table: "WindowsDiskInventory");

            migrationBuilder.RenameColumn(
                name: "StableSourceKey",
                schema: "inventory",
                table: "WindowsDiskInventory",
                newName: "DiskKey");

            migrationBuilder.CreateIndex(
                schema: "inventory",
                table: "WindowsDiskInventory",
                name: "UX_WindowsDiskInventory_ManagedServer_DiskKey",
                columns: new[] { "ManagedServerId", "DiskKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WindowsVolumeInventory_ManagedServer_VolumeKey",
                schema: "inventory",
                table: "WindowsVolumeInventory");

            migrationBuilder.RenameColumn(
                name: "VolumeKey",
                schema: "inventory",
                table: "WindowsVolumeInventory",
                newName: "StableSourceKey");

            migrationBuilder.CreateIndex(
                schema: "inventory",
                table: "WindowsVolumeInventory",
                name: "UX_WindowsVolumeInventory_ManagedServer_StableSourceKey",
                columns: new[] { "ManagedServerId", "StableSourceKey" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "UX_WindowsDiskInventory_ManagedServer_DiskKey",
                schema: "inventory",
                table: "WindowsDiskInventory");

            migrationBuilder.RenameColumn(
                name: "DiskKey",
                schema: "inventory",
                table: "WindowsDiskInventory",
                newName: "StableSourceKey");

            migrationBuilder.CreateIndex(
                schema: "inventory",
                table: "WindowsDiskInventory",
                name: "UX_WindowsDiskInventory_ManagedServer_StableSourceKey",
                columns: new[] { "ManagedServerId", "StableSourceKey" },
                unique: true);
        }
    }
}
