using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0083ExecutionPlanEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecutionPlan",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilitySnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryVersion = table.Column<long>(type: "bigint", nullable: false),
                    CapabilitySchemaVersion = table.Column<int>(type: "int", nullable: false),
                    DecisionSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ExecutionPlanSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PlanStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StepCount = table.Column<int>(type: "int", nullable: false),
                    ExclusionCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPlan", x => x.Id);
                    table.CheckConstraint("CK_ExecutionPlan_Counts", "[StepCount] >= 0 AND [ExclusionCount] >= 0");
                    table.CheckConstraint("CK_ExecutionPlan_SchemaVersions", "[CapabilitySchemaVersion] > 0 AND [DecisionSchemaVersion] > 0 AND [ExecutionPlanSchemaVersion] > 0");
                    table.CheckConstraint("CK_ExecutionPlan_SourceInventoryVersion", "[SourceInventoryVersion] > 0");
                    table.ForeignKey(
                        name: "FK_ExecutionPlan_ManagedServer_ManagedServerId",
                        column: x => x.ManagedServerId,
                        principalSchema: "configuration",
                        principalTable: "ManagedServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionPlanExclusion",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrategyVersion = table.Column<int>(type: "int", nullable: false),
                    SourceDecisionStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlanningDisposition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPlanExclusion", x => x.Id);
                    table.CheckConstraint("CK_ExecutionPlanExclusion_StrategyVersion", "[StrategyVersion] > 0");
                    table.ForeignKey(
                        name: "FK_ExecutionPlanExclusion_ExecutionPlan_ExecutionPlanId",
                        column: x => x.ExecutionPlanId,
                        principalSchema: "inventory",
                        principalTable: "ExecutionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionPlanStep",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LogicalStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrategyVersion = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StepSequence = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "int", nullable: false),
                    ParallelGroupCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TimeoutPolicyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TimeoutPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    RetryPolicyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RetryPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ThrottlingClass = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BatchGroupCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    RequiresManualApproval = table.Column<bool>(type: "bit", nullable: false),
                    SourceDecisionStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceDecisionReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InclusionReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPlanStep", x => x.Id);
                    table.CheckConstraint("CK_ExecutionPlanStep_PolicyVersions", "[TimeoutPolicyVersion] > 0 AND [RetryPolicyVersion] > 0");
                    table.CheckConstraint("CK_ExecutionPlanStep_PositiveValues", "[StrategyVersion] > 0 AND [StepSequence] > 0 AND [Priority] > 0 AND [ExecutionOrder] > 0 AND [TimeoutSeconds] > 0");
                    table.CheckConstraint("CK_ExecutionPlanStep_ReadOnly", "[IsReadOnly] = 1");
                    table.CheckConstraint("CK_ExecutionPlanStep_TimeoutBound", "[TimeoutSeconds] <= 3600");
                    table.ForeignKey(
                        name: "FK_ExecutionPlanStep_ExecutionPlan_ExecutionPlanId",
                        column: x => x.ExecutionPlanId,
                        principalSchema: "inventory",
                        principalTable: "ExecutionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionPlanExclusionCapability",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExclusionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CapabilityRuleVersion = table.Column<int>(type: "int", nullable: false),
                    CapabilitySnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPlanExclusionCapability", x => x.Id);
                    table.CheckConstraint("CK_ExecutionPlanExclusionCapability_InventoryVersion", "[SourceInventoryVersion] > 0");
                    table.CheckConstraint("CK_ExecutionPlanExclusionCapability_RuleVersion", "[CapabilityRuleVersion] > 0");
                    table.ForeignKey(
                        name: "FK_ExecutionPlanExclusionCapability_ExecutionPlanExclusion_ExclusionId",
                        column: x => x.ExclusionId,
                        principalSchema: "inventory",
                        principalTable: "ExecutionPlanExclusion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPlan_CapabilitySnapshotId",
                schema: "inventory",
                table: "ExecutionPlan",
                column: "CapabilitySnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPlan_DecisionPlanId",
                schema: "inventory",
                table: "ExecutionPlan",
                columns: new[] { "ManagedServerId", "DecisionPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPlan_SourceInventoryRunId",
                schema: "inventory",
                table: "ExecutionPlan",
                columns: new[] { "ManagedServerId", "SourceInventoryRunId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExecutionPlan_ManagedServer",
                schema: "inventory",
                table: "ExecutionPlan",
                column: "ManagedServerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPlanExclusion_ExecutionPlanId_StrategyCode",
                schema: "inventory",
                table: "ExecutionPlanExclusion",
                columns: new[] { "ExecutionPlanId", "StrategyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPlanExclusionCapability_ExclusionId_CapabilityCode",
                schema: "inventory",
                table: "ExecutionPlanExclusionCapability",
                columns: new[] { "ExclusionId", "CapabilityCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPlanStep_ExecutionPlanId_LogicalStepId",
                schema: "inventory",
                table: "ExecutionPlanStep",
                columns: new[] { "ExecutionPlanId", "LogicalStepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPlanStep_ExecutionPlanId_StepSequence",
                schema: "inventory",
                table: "ExecutionPlanStep",
                columns: new[] { "ExecutionPlanId", "StepSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPlanStep_ExecutionPlanId_StrategyCode",
                schema: "inventory",
                table: "ExecutionPlanStep",
                columns: new[] { "ExecutionPlanId", "StrategyCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionPlanExclusionCapability",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ExecutionPlanStep",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ExecutionPlanExclusion",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ExecutionPlan",
                schema: "inventory");
        }
    }
}
