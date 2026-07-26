using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "monitoring");

            migrationBuilder.EnsureSchema(
                name: "collection");

            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.EnsureSchema(
                name: "configuration");

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.CheckConstraint("CK_AuditLog_DetailJson_IsJson", "[DetailJson] IS NULL OR ISJSON([DetailJson]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "CollectorNode",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CollectorType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HostFqdn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    InstanceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectorNode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagedServer",
                schema: "configuration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fqdn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedServer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectorHeartbeat",
                schema: "monitoring",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectorNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessId = table.Column<int>(type: "int", nullable: true),
                    WorkingSetBytes = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectorHeartbeat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectorHeartbeat_CollectorNode_CollectorNodeId",
                        column: x => x.CollectorNodeId,
                        principalSchema: "collection",
                        principalTable: "CollectorNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectorRun",
                schema: "collection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectorNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectorRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectorRun_CollectorNode_CollectorNodeId",
                        column: x => x.CollectorNodeId,
                        principalSchema: "collection",
                        principalTable: "CollectorNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectorRun_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommandQueueItem",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetCollectorType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    NotBefore = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandQueueItem", x => x.Id);
                    table.CheckConstraint("CK_CommandQueueItem_PayloadJson_IsJson", "ISJSON([PayloadJson]) = 1");
                    table.CheckConstraint("CK_CommandQueueItem_Priority_NonNegative", "[Priority] >= 0");
                    table.ForeignKey(
                        name: "FK_CommandQueueItem_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventorySnapshot",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectorRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySnapshot", x => x.Id);
                    table.CheckConstraint("CK_InventorySnapshot_PayloadJson_IsJson", "ISJSON([PayloadJson]) = 1");
                    table.CheckConstraint("CK_InventorySnapshot_SchemaVersion_Positive", "[SchemaVersion] > 0");
                    table.ForeignKey(
                        name: "FK_InventorySnapshot_CollectorRun_CollectorRunId",
                        column: x => x.CollectorRunId,
                        principalSchema: "collection",
                        principalTable: "CollectorRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySnapshot_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CorrelationId",
                schema: "audit",
                table: "AuditLog",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Entity",
                schema: "audit",
                table: "AuditLog",
                columns: new[] { "EntityType", "EntityId", "OccurredAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_OccurredAt",
                schema: "audit",
                table: "AuditLog",
                column: "OccurredAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CollectorHeartbeat_Collector_ObservedAt",
                schema: "monitoring",
                table: "CollectorHeartbeat",
                columns: new[] { "CollectorNodeId", "ObservedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UX_CollectorNode_Registration",
                schema: "collection",
                table: "CollectorNode",
                columns: new[] { "HostFqdn", "CollectorType", "InstanceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectorRun_Collector_Status_CreatedAt",
                schema: "collection",
                table: "CollectorRun",
                columns: new[] { "CollectorNodeId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectorRun_Server_CreatedAt",
                schema: "collection",
                table: "CollectorRun",
                columns: new[] { "ManagedServerId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueue_Status_Target_Priority",
                schema: "operations",
                table: "CommandQueueItem",
                columns: new[] { "Status", "TargetCollectorType", "Priority", "NotBefore", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueueItem_ManagedServerId",
                schema: "operations",
                table: "CommandQueueItem",
                column: "ManagedServerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySnapshot_Server_Type_CapturedAt",
                schema: "inventory",
                table: "InventorySnapshot",
                columns: new[] { "ManagedServerId", "SnapshotType", "CapturedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "UX_InventorySnapshot_RunContract",
                schema: "inventory",
                table: "InventorySnapshot",
                columns: new[] { "CollectorRunId", "SnapshotType", "SchemaVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ManagedServer_Fqdn",
                schema: "configuration",
                table: "ManagedServer",
                column: "Fqdn",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "CollectorHeartbeat",
                schema: "monitoring");

            migrationBuilder.DropTable(
                name: "CommandQueueItem",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "InventorySnapshot",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "CollectorRun",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "CollectorNode",
                schema: "collection");

            migrationBuilder.DropTable(
                name: "ManagedServer",
                schema: "configuration");
        }
    }
}
