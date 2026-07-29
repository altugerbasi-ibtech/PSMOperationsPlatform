using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0084CollectorRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "runtime");

            migrationBuilder.CreateTable(
                name: "ExecutionRunState",
                schema: "runtime",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionPlanSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ExecutionStateSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    SourceDecisionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCapabilitySnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryVersion = table.Column<long>(type: "bigint", nullable: false),
                    RuntimeVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    TotalDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    StepCount = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    BytesCollected = table.Column<long>(type: "bigint", nullable: false),
                    ObjectsCollected = table.Column<long>(type: "bigint", nullable: false),
                    FailureCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionRunState", x => x.Id);
                    table.CheckConstraint("CK_ExecutionRunState_Metrics", "[TotalDurationTicks] >= 0 AND [StepCount] >= 0 AND [AttemptCount] >= 0 AND [RetryCount] >= 0 AND [BytesCollected] >= 0 AND [ObjectsCollected] >= 0");
                    table.CheckConstraint("CK_ExecutionRunState_Versions", "[ExecutionPlanSchemaVersion] > 0 AND [ExecutionStateSchemaVersion] > 0 AND [SourceInventoryVersion] > 0");
                    table.ForeignKey(
                        name: "FK_ExecutionRunState_ExecutionPlan_ExecutionPlanId",
                        column: x => x.ExecutionPlanId,
                        principalSchema: "inventory",
                        principalTable: "ExecutionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionStepState",
                schema: "runtime",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionPlanStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrategyVersion = table.Column<int>(type: "int", nullable: false),
                    PluginVersion = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    QueueSequence = table.Column<int>(type: "int", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EligibleAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    TimedOutAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    QueueDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    WaitDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    TotalDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    BytesCollected = table.Column<long>(type: "bigint", nullable: false),
                    ObjectsCollected = table.Column<long>(type: "bigint", nullable: false),
                    FailureCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionStepState", x => x.Id);
                    table.CheckConstraint("CK_ExecutionStepState_Metrics", "[QueueDurationTicks] >= 0 AND [WaitDurationTicks] >= 0 AND [ExecutionDurationTicks] >= 0 AND [TotalDurationTicks] >= 0 AND [AttemptCount] >= 0 AND [RetryCount] >= 0 AND [BytesCollected] >= 0 AND [ObjectsCollected] >= 0");
                    table.CheckConstraint("CK_ExecutionStepState_Versions", "[StrategyVersion] > 0 AND [QueueSequence] > 0 AND ([PluginVersion] IS NULL OR [PluginVersion] > 0)");
                    table.ForeignKey(
                        name: "FK_ExecutionStepState_ExecutionRunState_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalSchema: "runtime",
                        principalTable: "ExecutionRunState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionAttemptState",
                schema: "runtime",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionStepStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    IsRetry = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    BytesCollected = table.Column<long>(type: "bigint", nullable: true),
                    ObjectsCollected = table.Column<long>(type: "bigint", nullable: true),
                    FailureCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionAttemptState", x => x.Id);
                    table.CheckConstraint("CK_ExecutionAttemptState_Metrics", "[DurationTicks] >= 0 AND ([BytesCollected] IS NULL OR [BytesCollected] >= 0) AND ([ObjectsCollected] IS NULL OR [ObjectsCollected] >= 0)");
                    table.CheckConstraint("CK_ExecutionAttemptState_Number", "[AttemptNumber] > 0");
                    table.ForeignKey(
                        name: "FK_ExecutionAttemptState_ExecutionStepState_ExecutionStepStateId",
                        column: x => x.ExecutionStepStateId,
                        principalSchema: "runtime",
                        principalTable: "ExecutionStepState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionAttemptState_ExecutionStepStateId_AttemptNumber",
                schema: "runtime",
                table: "ExecutionAttemptState",
                columns: new[] { "ExecutionStepStateId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunState_ExecutionPlanId",
                schema: "runtime",
                table: "ExecutionRunState",
                column: "ExecutionPlanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunState_ManagedServerId_SourceInventoryRunId",
                schema: "runtime",
                table: "ExecutionRunState",
                columns: new[] { "ManagedServerId", "SourceInventoryRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunState_ManagedServerId_Status",
                schema: "runtime",
                table: "ExecutionRunState",
                columns: new[] { "ManagedServerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStepState_ExecutionRunId_ExecutionPlanStepId",
                schema: "runtime",
                table: "ExecutionStepState",
                columns: new[] { "ExecutionRunId", "ExecutionPlanStepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStepState_ExecutionRunId_StrategyCode",
                schema: "runtime",
                table: "ExecutionStepState",
                columns: new[] { "ExecutionRunId", "StrategyCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionAttemptState",
                schema: "runtime");

            migrationBuilder.DropTable(
                name: "ExecutionStepState",
                schema: "runtime");

            migrationBuilder.DropTable(
                name: "ExecutionRunState",
                schema: "runtime");
        }
    }
}
