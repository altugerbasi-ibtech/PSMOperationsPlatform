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
            migrationBuilder.RenameColumn(
                name: "StableSourceKey",
                schema: "inventory",
                table: "WindowsVolumeInventory",
                newName: "VolumeKey");

            migrationBuilder.RenameIndex(
                name: "UX_WindowsVolumeInventory_ManagedServer_StableSourceKey",
                schema: "inventory",
                table: "WindowsVolumeInventory",
                newName: "UX_WindowsVolumeInventory_ManagedServer_VolumeKey");

            migrationBuilder.RenameColumn(
                name: "StableSourceKey",
                schema: "inventory",
                table: "WindowsDiskInventory",
                newName: "DiskKey");

            migrationBuilder.RenameIndex(
                name: "UX_WindowsDiskInventory_ManagedServer_StableSourceKey",
                schema: "inventory",
                table: "WindowsDiskInventory",
                newName: "UX_WindowsDiskInventory_ManagedServer_DiskKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VolumeKey",
                schema: "inventory",
                table: "WindowsVolumeInventory",
                newName: "StableSourceKey");

            migrationBuilder.RenameIndex(
                name: "UX_WindowsVolumeInventory_ManagedServer_VolumeKey",
                schema: "inventory",
                table: "WindowsVolumeInventory",
                newName: "UX_WindowsVolumeInventory_ManagedServer_StableSourceKey");

            migrationBuilder.RenameColumn(
                name: "DiskKey",
                schema: "inventory",
                table: "WindowsDiskInventory",
                newName: "StableSourceKey");

            migrationBuilder.RenameIndex(
                name: "UX_WindowsDiskInventory_ManagedServer_DiskKey",
                schema: "inventory",
                table: "WindowsDiskInventory",
                newName: "UX_WindowsDiskInventory_ManagedServer_StableSourceKey");
        }
    }
}
