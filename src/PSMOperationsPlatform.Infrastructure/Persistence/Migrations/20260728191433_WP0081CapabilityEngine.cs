using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations;

public partial class WP0081CapabilityEngine : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WindowsCapabilitySnapshot", schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceInventoryVersion = table.Column<long>(type: "bigint", nullable: false),
                CapabilitySchemaVersion = table.Column<int>(type: "int", nullable: false),
                EvaluatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                EvaluationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsCapabilitySnapshot", x => x.Id);
                table.CheckConstraint("CK_WindowsCapabilitySnapshot_SchemaVersion", "[CapabilitySchemaVersion] > 0");
                table.CheckConstraint("CK_WindowsCapabilitySnapshot_SourceInventoryVersion", "[SourceInventoryVersion] > 0");
                table.ForeignKey(name: "FK_WindowsCapabilitySnapshot_ManagedServer_ManagedServerId",
                    column: x => x.ManagedServerId, principalSchema: "configuration",
                    principalTable: "ManagedServer", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WindowsCapabilityEntry", schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CapabilityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SupportStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ReadinessStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                RuleVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WindowsCapabilityEntry", x => x.Id);
                table.ForeignKey(name: "FK_WindowsCapabilityEntry_WindowsCapabilitySnapshot_SnapshotId",
                    column: x => x.SnapshotId, principalSchema: "inventory",
                    principalTable: "WindowsCapabilitySnapshot", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("UX_WindowsCapabilityEntry_Snapshot_CapabilityCode",
            schema: "inventory", table: "WindowsCapabilityEntry",
            columns: new[] { "SnapshotId", "CapabilityCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_WindowsCapabilitySnapshot_SourceInventory",
            schema: "inventory", table: "WindowsCapabilitySnapshot",
            columns: new[] { "ManagedServerId", "InventoryRunId", "SourceInventoryVersion" });
        migrationBuilder.CreateIndex("UX_WindowsCapabilitySnapshot_ManagedServer",
            schema: "inventory", table: "WindowsCapabilitySnapshot",
            column: "ManagedServerId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("WindowsCapabilityEntry", "inventory");
        migrationBuilder.DropTable("WindowsCapabilitySnapshot", "inventory");
    }
}
