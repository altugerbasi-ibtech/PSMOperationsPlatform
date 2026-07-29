using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0071ADurableInventoryRunCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            string[] tables =
            [
                "WindowsComputerInventory",
                "WindowsOperatingSystemInventory",
                "WindowsProcessorInventory",
                "WindowsMemoryInventory",
                "WindowsDiskInventory",
                "WindowsVolumeInventory",
                "WindowsNetworkAdapterInventory",
                "WindowsIpv4AddressInventory",
            ];
            foreach (string table in tables)
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "InventoryRunId",
                    schema: "inventory",
                    table: table,
                    type: "uniqueidentifier",
                    nullable: true);
            }

            migrationBuilder.Sql(
                """
                EXEC(N'
                    CREATE TABLE #LegacyInventoryRun
                    (
                        ManagedServerId uniqueidentifier NOT NULL PRIMARY KEY,
                        LegacyInventoryRunId uniqueidentifier NOT NULL
                    );

                    INSERT INTO #LegacyInventoryRun (ManagedServerId, LegacyInventoryRunId)
                    SELECT ManagedServerId, NEWID()
                    FROM
                    (
                        SELECT ManagedServerId FROM [inventory].[WindowsComputerInventory]
                        UNION SELECT ManagedServerId FROM [inventory].[WindowsOperatingSystemInventory]
                        UNION SELECT ManagedServerId FROM [inventory].[WindowsProcessorInventory]
                        UNION SELECT ManagedServerId FROM [inventory].[WindowsMemoryInventory]
                        UNION SELECT ManagedServerId FROM [inventory].[WindowsDiskInventory]
                        UNION SELECT ManagedServerId FROM [inventory].[WindowsVolumeInventory]
                        UNION SELECT ManagedServerId FROM [inventory].[WindowsNetworkAdapterInventory]
                        UNION SELECT ManagedServerId FROM [inventory].[WindowsIpv4AddressInventory]
                    ) AS ExistingServers;

                    UPDATE target SET InventoryRunId = mapping.LegacyInventoryRunId
                    FROM [inventory].[WindowsComputerInventory] AS target
                    INNER JOIN #LegacyInventoryRun AS mapping ON mapping.ManagedServerId = target.ManagedServerId;
                    UPDATE target SET InventoryRunId = mapping.LegacyInventoryRunId
                    FROM [inventory].[WindowsOperatingSystemInventory] AS target
                    INNER JOIN #LegacyInventoryRun AS mapping ON mapping.ManagedServerId = target.ManagedServerId;
                    UPDATE target SET InventoryRunId = mapping.LegacyInventoryRunId
                    FROM [inventory].[WindowsProcessorInventory] AS target
                    INNER JOIN #LegacyInventoryRun AS mapping ON mapping.ManagedServerId = target.ManagedServerId;
                    UPDATE target SET InventoryRunId = mapping.LegacyInventoryRunId
                    FROM [inventory].[WindowsMemoryInventory] AS target
                    INNER JOIN #LegacyInventoryRun AS mapping ON mapping.ManagedServerId = target.ManagedServerId;
                    UPDATE target SET InventoryRunId = mapping.LegacyInventoryRunId
                    FROM [inventory].[WindowsDiskInventory] AS target
                    INNER JOIN #LegacyInventoryRun AS mapping ON mapping.ManagedServerId = target.ManagedServerId;
                    UPDATE target SET InventoryRunId = mapping.LegacyInventoryRunId
                    FROM [inventory].[WindowsVolumeInventory] AS target
                    INNER JOIN #LegacyInventoryRun AS mapping ON mapping.ManagedServerId = target.ManagedServerId;
                    UPDATE target SET InventoryRunId = mapping.LegacyInventoryRunId
                    FROM [inventory].[WindowsNetworkAdapterInventory] AS target
                    INNER JOIN #LegacyInventoryRun AS mapping ON mapping.ManagedServerId = target.ManagedServerId;
                    UPDATE target SET InventoryRunId = mapping.LegacyInventoryRunId
                    FROM [inventory].[WindowsIpv4AddressInventory] AS target
                    INNER JOIN #LegacyInventoryRun AS mapping ON mapping.ManagedServerId = target.ManagedServerId;

                    IF EXISTS
                    (
                        SELECT InventoryRunId FROM [inventory].[WindowsComputerInventory] WHERE InventoryRunId IS NULL
                        UNION ALL SELECT InventoryRunId FROM [inventory].[WindowsOperatingSystemInventory] WHERE InventoryRunId IS NULL
                        UNION ALL SELECT InventoryRunId FROM [inventory].[WindowsProcessorInventory] WHERE InventoryRunId IS NULL
                        UNION ALL SELECT InventoryRunId FROM [inventory].[WindowsMemoryInventory] WHERE InventoryRunId IS NULL
                        UNION ALL SELECT InventoryRunId FROM [inventory].[WindowsDiskInventory] WHERE InventoryRunId IS NULL
                        UNION ALL SELECT InventoryRunId FROM [inventory].[WindowsVolumeInventory] WHERE InventoryRunId IS NULL
                        UNION ALL SELECT InventoryRunId FROM [inventory].[WindowsNetworkAdapterInventory] WHERE InventoryRunId IS NULL
                        UNION ALL SELECT InventoryRunId FROM [inventory].[WindowsIpv4AddressInventory] WHERE InventoryRunId IS NULL
                    )
                        THROW 51000, ''WP-007.1A InventoryRunId backfill left NULL rows.'', 1;

                    ALTER TABLE [inventory].[WindowsComputerInventory] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL;
                    ALTER TABLE [inventory].[WindowsOperatingSystemInventory] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL;
                    ALTER TABLE [inventory].[WindowsProcessorInventory] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL;
                    ALTER TABLE [inventory].[WindowsMemoryInventory] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL;
                    ALTER TABLE [inventory].[WindowsDiskInventory] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL;
                    ALTER TABLE [inventory].[WindowsVolumeInventory] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL;
                    ALTER TABLE [inventory].[WindowsNetworkAdapterInventory] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL;
                    ALTER TABLE [inventory].[WindowsIpv4AddressInventory] ALTER COLUMN [InventoryRunId] uniqueidentifier NOT NULL;

                    CREATE INDEX [IX_WindowsComputerInventory_ManagedServer_InventoryRun] ON [inventory].[WindowsComputerInventory] ([ManagedServerId], [InventoryRunId]);
                    CREATE INDEX [IX_WindowsOperatingSystemInventory_ManagedServer_InventoryRun] ON [inventory].[WindowsOperatingSystemInventory] ([ManagedServerId], [InventoryRunId]);
                    CREATE INDEX [IX_WindowsProcessorInventory_ManagedServer_InventoryRun] ON [inventory].[WindowsProcessorInventory] ([ManagedServerId], [InventoryRunId]);
                    CREATE INDEX [IX_WindowsMemoryInventory_ManagedServer_InventoryRun] ON [inventory].[WindowsMemoryInventory] ([ManagedServerId], [InventoryRunId]);
                    CREATE INDEX [IX_WindowsDiskInventory_ManagedServer_InventoryRun] ON [inventory].[WindowsDiskInventory] ([ManagedServerId], [InventoryRunId]);
                    CREATE INDEX [IX_WindowsVolumeInventory_ManagedServer_InventoryRun] ON [inventory].[WindowsVolumeInventory] ([ManagedServerId], [InventoryRunId]);
                    CREATE INDEX [IX_WindowsNetworkAdapterInventory_ManagedServer_InventoryRun] ON [inventory].[WindowsNetworkAdapterInventory] ([ManagedServerId], [InventoryRunId]);
                    CREATE INDEX [IX_WindowsIpv4AddressInventory_ManagedServer_InventoryRun] ON [inventory].[WindowsIpv4AddressInventory] ([ManagedServerId], [InventoryRunId]);

                    DROP TABLE #LegacyInventoryRun;
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WindowsVolumeInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsVolumeInventory");

            migrationBuilder.DropIndex(
                name: "IX_WindowsProcessorInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropIndex(
                name: "IX_WindowsOperatingSystemInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory");

            migrationBuilder.DropIndex(
                name: "IX_WindowsNetworkAdapterInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropIndex(
                name: "IX_WindowsMemoryInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsMemoryInventory");

            migrationBuilder.DropIndex(
                name: "IX_WindowsIpv4AddressInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory");

            migrationBuilder.DropIndex(
                name: "IX_WindowsDiskInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsDiskInventory");

            migrationBuilder.DropIndex(
                name: "IX_WindowsComputerInventory_ManagedServer_InventoryRun",
                schema: "inventory",
                table: "WindowsComputerInventory");

            migrationBuilder.DropColumn(
                name: "InventoryRunId",
                schema: "inventory",
                table: "WindowsVolumeInventory");

            migrationBuilder.DropColumn(
                name: "InventoryRunId",
                schema: "inventory",
                table: "WindowsProcessorInventory");

            migrationBuilder.DropColumn(
                name: "InventoryRunId",
                schema: "inventory",
                table: "WindowsOperatingSystemInventory");

            migrationBuilder.DropColumn(
                name: "InventoryRunId",
                schema: "inventory",
                table: "WindowsNetworkAdapterInventory");

            migrationBuilder.DropColumn(
                name: "InventoryRunId",
                schema: "inventory",
                table: "WindowsMemoryInventory");

            migrationBuilder.DropColumn(
                name: "InventoryRunId",
                schema: "inventory",
                table: "WindowsIpv4AddressInventory");

            migrationBuilder.DropColumn(
                name: "InventoryRunId",
                schema: "inventory",
                table: "WindowsDiskInventory");

            migrationBuilder.DropColumn(
                name: "InventoryRunId",
                schema: "inventory",
                table: "WindowsComputerInventory");
        }
    }
}
