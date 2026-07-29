using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0082CollectorDecisionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "RuleVersion",
                schema: "inventory",
                table: "WindowsCapabilityEntry",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "inventory",
                table: "WindowsCapabilityEntry",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(
                """
                EXEC(N'UPDATE [inventory].[WindowsCapabilityEntry]
                SET [Category] = CASE
                    WHEN [CapabilityCode] LIKE N''CanCollect%'' OR [CapabilityCode] = N''CanRunWindowsPowerShell51Collection'' THEN N''Collection''
                    WHEN [CapabilityCode] IN (N''SupportsPowerShell7'', N''CanRunPowerShell7CollectorTooling'') THEN N''Diagnostics''
                    ELSE N''Platform''
                END
                WHERE [Category] IS NULL;');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                schema: "inventory",
                table: "WindowsCapabilityEntry",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WindowsCapabilityEntry_RuleVersion",
                schema: "inventory",
                table: "WindowsCapabilityEntry",
                sql: "[RuleVersion] > 0");

            migrationBuilder.CreateTable(
                name: "CollectorDecisionPlan",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilitySnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryVersion = table.Column<long>(type: "bigint", nullable: false),
                    CapabilitySchemaVersion = table.Column<int>(type: "int", nullable: false),
                    DecisionSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    OverallStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StrategyCount = table.Column<int>(type: "int", nullable: false),
                    EligibleCount = table.Column<int>(type: "int", nullable: false),
                    BlockedCount = table.Column<int>(type: "int", nullable: false),
                    IndeterminateCount = table.Column<int>(type: "int", nullable: false),
                    NotApplicableCount = table.Column<int>(type: "int", nullable: false),
                    DisabledCount = table.Column<int>(type: "int", nullable: false),
                    InvalidCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectorDecisionPlan", x => x.Id);
                    table.CheckConstraint("CK_CollectorDecisionPlan_SourceInventoryVersion", "[SourceInventoryVersion] > 0");
                    table.CheckConstraint("CK_CollectorDecisionPlan_Versions", "[CapabilitySchemaVersion] > 0 AND [DecisionSchemaVersion] > 0");
                    table.ForeignKey(
                        name: "FK_CollectorDecisionPlan_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WindowsCapabilityProvenance",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FactCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FactKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsCapabilityProvenance", x => x.Id);
                    table.CheckConstraint("CK_WindowsCapabilityProvenance_InventoryVersion", "[InventoryVersion] > 0");
                    table.ForeignKey(
                        name: "FK_WindowsCapabilityProvenance_WindowsCapabilityEntry_CapabilityEntryId",
                        column: x => x.CapabilityEntryId,
                        principalSchema: "inventory",
                        principalTable: "WindowsCapabilityEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectorStrategyDecision",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrategyVersion = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EligibilityStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExecutionReadinessStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DecisionStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "int", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    RequiresManualApproval = table.Column<bool>(type: "bit", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectorStrategyDecision", x => x.Id);
                    table.CheckConstraint("CK_CollectorStrategyDecision_PriorityOrder", "[Priority] > 0 AND [ExecutionOrder] > 0");
                    table.CheckConstraint("CK_CollectorStrategyDecision_StrategyVersion", "[StrategyVersion] > 0");
                    table.ForeignKey(
                        name: "FK_CollectorStrategyDecision_CollectorDecisionPlan_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "inventory",
                        principalTable: "CollectorDecisionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectorDecisionCapabilityReference",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CapabilityCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrerequisiteStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CapabilityRuleVersion = table.Column<int>(type: "int", nullable: false),
                    SupportStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReadinessStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EvaluationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CapabilitySnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectorDecisionCapabilityReference", x => x.Id);
                    table.CheckConstraint("CK_CollectorDecisionCapabilityReference_InventoryVersion", "[SourceInventoryVersion] > 0");
                    table.CheckConstraint("CK_CollectorDecisionCapabilityReference_RuleVersion", "[CapabilityRuleVersion] > 0");
                    table.ForeignKey(
                        name: "FK_CollectorDecisionCapabilityReference_CollectorStrategyDecision_StrategyDecisionId",
                        column: x => x.StrategyDecisionId,
                        principalSchema: "inventory",
                        principalTable: "CollectorStrategyDecision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectorDecisionCapabilityReference_StrategyDecisionId_CapabilityCode",
                schema: "inventory",
                table: "CollectorDecisionCapabilityReference",
                columns: new[] { "StrategyDecisionId", "CapabilityCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectorDecisionPlan_CapabilitySnapshotId",
                schema: "inventory",
                table: "CollectorDecisionPlan",
                column: "CapabilitySnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectorDecisionPlan_SourceInventoryRunId",
                schema: "inventory",
                table: "CollectorDecisionPlan",
                columns: new[] { "ManagedServerId", "SourceInventoryRunId" });

            migrationBuilder.CreateIndex(
                name: "UX_CollectorDecisionPlan_ManagedServer",
                schema: "inventory",
                table: "CollectorDecisionPlan",
                column: "ManagedServerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectorStrategyDecision_PlanId_StrategyCode",
                schema: "inventory",
                table: "CollectorStrategyDecision",
                columns: new[] { "PlanId", "StrategyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WindowsCapabilityProvenance_CapabilityEntryId_FactCategory_FactKey",
                schema: "inventory",
                table: "WindowsCapabilityProvenance",
                columns: new[] { "CapabilityEntryId", "FactCategory", "FactKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectorDecisionCapabilityReference",
                schema: "inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WindowsCapabilityEntry_RuleVersion",
                schema: "inventory",
                table: "WindowsCapabilityEntry");

            migrationBuilder.DropTable(
                name: "WindowsCapabilityProvenance",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "CollectorStrategyDecision",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "CollectorDecisionPlan",
                schema: "inventory");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "inventory",
                table: "WindowsCapabilityEntry");

            migrationBuilder.AlterColumn<string>(
                name: "RuleVersion",
                schema: "inventory",
                table: "WindowsCapabilityEntry",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
