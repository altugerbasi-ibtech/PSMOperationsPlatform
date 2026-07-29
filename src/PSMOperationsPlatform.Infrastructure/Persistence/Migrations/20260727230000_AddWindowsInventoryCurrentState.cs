using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OperationsDbContext))]
[Migration("20260727230000_AddWindowsInventoryCurrentState")]
public partial class AddWindowsInventoryCurrentState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WindowsComputerInventory",
            schema: "inventory",
            columns: table => new
            {
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ComputerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                Fqdn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                DomainName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                Manufacturer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                Model = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                SerialNumber = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsComputerInventory", x => x.ManagedServerId);
                table.ForeignKey(
                    name: "FK_WindowsComputerInventory_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId,
                    principalSchema: "configuration",
                    principalTable: "ManagedServer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WindowsOperatingSystemInventory",
            schema: "inventory",
            columns: table => new
            {
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Caption = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BuildNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Edition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Architecture = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                InstallDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                LastBootTime = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsOperatingSystemInventory", x => x.ManagedServerId);
                table.ForeignKey(
                    name: "FK_WindowsOperatingSystemInventory_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId,
                    principalSchema: "configuration",
                    principalTable: "ManagedServer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WindowsMemoryInventory",
            schema: "inventory",
            columns: table => new
            {
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TotalPhysicalMemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsMemoryInventory", x => x.ManagedServerId);
                table.CheckConstraint(
                    "CK_WindowsMemoryInventory_TotalPhysicalMemoryBytes_NonNegative",
                    "[TotalPhysicalMemoryBytes] >= 0");
                table.ForeignKey(
                    name: "FK_WindowsMemoryInventory_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId,
                    principalSchema: "configuration",
                    principalTable: "ManagedServer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        CreateCollectionTables(migrationBuilder);
        CreateIndexes(migrationBuilder);
    }

    private static void CreateCollectionTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WindowsProcessorInventory",
            schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StableSourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                Manufacturer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                CoreCount = table.Column<int>(type: "int", nullable: true),
                LogicalProcessorCount = table.Column<int>(type: "int", nullable: true),
                MaxClockSpeedMhz = table.Column<int>(type: "int", nullable: true),
                CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsProcessorInventory", x => x.Id);
                table.ForeignKey(
                    name: "FK_WindowsProcessorInventory_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId,
                    principalSchema: "configuration",
                    principalTable: "ManagedServer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WindowsDiskInventory",
            schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StableSourceKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                DiskNumber = table.Column<int>(type: "int", nullable: true),
                FriendlyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                SerialNumber = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                BusType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                PartitionStyle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsDiskInventory", x => x.Id);
                table.ForeignKey(
                    name: "FK_WindowsDiskInventory_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId,
                    principalSchema: "configuration",
                    principalTable: "ManagedServer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WindowsVolumeInventory",
            schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StableSourceKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                DriveLetter = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                Label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                FileSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                FreeSpaceBytes = table.Column<long>(type: "bigint", nullable: true),
                CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsVolumeInventory", x => x.Id);
                table.ForeignKey(
                    name: "FK_WindowsVolumeInventory_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId,
                    principalSchema: "configuration",
                    principalTable: "ManagedServer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WindowsNetworkAdapterInventory",
            schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StableSourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                InterfaceDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                MacAddress = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                OperationalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                LinkSpeedBitsPerSecond = table.Column<long>(type: "bigint", nullable: true),
                CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsNetworkAdapterInventory", x => x.Id);
                table.ForeignKey(
                    name: "FK_WindowsNetworkAdapterInventory_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId,
                    principalSchema: "configuration",
                    principalTable: "ManagedServer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WindowsIpv4AddressInventory",
            schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NetworkAdapterInventoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StableSourceKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Address = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                PrefixLength = table.Column<int>(type: "int", nullable: false),
                IsDhcp = table.Column<bool>(type: "bit", nullable: true),
                CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsIpv4AddressInventory", x => x.Id);
                table.CheckConstraint("CK_WindowsIpv4AddressInventory_PrefixLength_Range", "[PrefixLength] >= 0 AND [PrefixLength] <= 32");
                table.ForeignKey(
                    name: "FK_WindowsIpv4AddressInventory_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId,
                    principalSchema: "configuration",
                    principalTable: "ManagedServer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId",
                    column: x => x.NetworkAdapterInventoryId,
                    principalSchema: "inventory",
                    principalTable: "WindowsNetworkAdapterInventory",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
    }

    private static void CreateIndexes(MigrationBuilder migrationBuilder)
    {
        foreach ((string table, string index) in new[]
        {
            ("WindowsProcessorInventory", "UX_WindowsProcessorInventory_ManagedServer_StableSourceKey"),
            ("WindowsDiskInventory", "UX_WindowsDiskInventory_ManagedServer_StableSourceKey"),
            ("WindowsVolumeInventory", "UX_WindowsVolumeInventory_ManagedServer_StableSourceKey"),
            ("WindowsNetworkAdapterInventory", "UX_WindowsNetworkAdapterInventory_ManagedServer_StableSourceKey"),
            ("WindowsIpv4AddressInventory", "UX_WindowsIpv4AddressInventory_ManagedServer_StableSourceKey")
        })
        {
            migrationBuilder.CreateIndex(
                name: index,
                schema: "inventory",
                table: table,
                columns: new[] { "ManagedServerId", "StableSourceKey" },
                unique: true);
        }

        migrationBuilder.CreateIndex(
            name: "IX_WindowsIpv4AddressInventory_NetworkAdapterInventoryId",
            schema: "inventory",
            table: "WindowsIpv4AddressInventory",
            column: "NetworkAdapterInventoryId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WindowsIpv4AddressInventory", schema: "inventory");
        migrationBuilder.DropTable(name: "WindowsComputerInventory", schema: "inventory");
        migrationBuilder.DropTable(name: "WindowsDiskInventory", schema: "inventory");
        migrationBuilder.DropTable(name: "WindowsMemoryInventory", schema: "inventory");
        migrationBuilder.DropTable(name: "WindowsOperatingSystemInventory", schema: "inventory");
        migrationBuilder.DropTable(name: "WindowsProcessorInventory", schema: "inventory");
        migrationBuilder.DropTable(name: "WindowsVolumeInventory", schema: "inventory");
        migrationBuilder.DropTable(name: "WindowsNetworkAdapterInventory", schema: "inventory");
    }
}
