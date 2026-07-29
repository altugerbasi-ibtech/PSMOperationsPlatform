using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSMOperationsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WP0088ExecutionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "history");

            migrationBuilder.CreateTable(
                name: "ExecutionRunHistory",
                schema: "history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDecisionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCapabilitySnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryVersion = table.Column<long>(type: "bigint", nullable: false),
                    HistorySchemaVersion = table.Column<int>(type: "int", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExecutionOutcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TerminalState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FailureCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    StepCount = table.Column<int>(type: "int", nullable: false),
                    CompletedStepCount = table.Column<int>(type: "int", nullable: false),
                    FailedStepCount = table.Column<int>(type: "int", nullable: false),
                    TimedOutStepCount = table.Column<int>(type: "int", nullable: false),
                    CancelledStepCount = table.Column<int>(type: "int", nullable: false),
                    SkippedStepCount = table.Column<int>(type: "int", nullable: false),
                    StrategyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrategyVersion = table.Column<int>(type: "int", nullable: false),
                    PluginId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PluginVersion = table.Column<int>(type: "int", nullable: false),
                    TargetSdkVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RuntimeContractVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExecutionPlanSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ExecutionStateSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ExecutionEventSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ExecutionMonitoringSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    ArtifactFileCount = table.Column<int>(type: "int", nullable: false),
                    ArtifactObjectCount = table.Column<int>(type: "int", nullable: false),
                    ArtifactMetricCount = table.Column<int>(type: "int", nullable: false),
                    ArtifactByteCount = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProjectionFailureCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ProjectionReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionRunHistory", x => x.Id);
                    table.UniqueConstraint("AK_ExecutionRunHistory_ExecutionRunId", x => x.ExecutionRunId);
                    table.CheckConstraint("CK_ExecutionRunHistory_Counts", "[DurationTicks]>=0 AND [WarningCount]>=0 AND [AttemptCount]>=0 AND [RetryCount]>=0 AND [StepCount]>=0 AND [CompletedStepCount]>=0 AND [FailedStepCount]>=0 AND [TimedOutStepCount]>=0 AND [CancelledStepCount]>=0 AND [SkippedStepCount]>=0 AND [ArtifactFileCount]>=0 AND [ArtifactObjectCount]>=0 AND [ArtifactMetricCount]>=0 AND [ArtifactByteCount]>=0");
                    table.CheckConstraint("CK_ExecutionRunHistory_Versions", "[HistorySchemaVersion]>0 AND [StrategyVersion]>0 AND [PluginVersion]>0 AND [ExecutionPlanSchemaVersion]>0 AND [ExecutionStateSchemaVersion]>0 AND [ExecutionEventSchemaVersion]>0 AND [ExecutionMonitoringSchemaVersion]>0 AND [SourceInventoryVersion]>0");
                });

            migrationBuilder.CreateTable(
                name: "ExecutionArtifactHistory",
                schema: "history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HistorySchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ArtifactId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ArtifactSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ArtifactType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LogicalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ObjectCount = table.Column<long>(type: "bigint", nullable: true),
                    MetricCount = table.Column<long>(type: "bigint", nullable: true),
                    ByteCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionArtifactHistory", x => x.Id);
                    table.CheckConstraint("CK_ExecutionArtifactHistory_Counts", "([ObjectCount] IS NULL OR [ObjectCount]>=0) AND ([MetricCount] IS NULL OR [MetricCount]>=0) AND [ByteCount]>=0");
                    table.CheckConstraint("CK_ExecutionArtifactHistory_Versions", "[HistorySchemaVersion]>0 AND [ArtifactSchemaVersion]>0");
                    table.ForeignKey(
                        name: "FK_ExecutionArtifactHistory_ExecutionRunHistory_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalSchema: "history",
                        principalTable: "ExecutionRunHistory",
                        principalColumn: "ExecutionRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionAttemptHistory",
                schema: "history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HistorySchemaVersion = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    AttemptOutcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FailureCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RetryScheduled = table.Column<bool>(type: "bit", nullable: false),
                    RetryDelayTicks = table.Column<long>(type: "bigint", nullable: true),
                    CancellationObserved = table.Column<bool>(type: "bit", nullable: false),
                    TimeoutObserved = table.Column<bool>(type: "bit", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionAttemptHistory", x => x.Id);
                    table.CheckConstraint("CK_ExecutionAttemptHistory_Counts", "[DurationTicks]>=0 AND ([RetryDelayTicks] IS NULL OR [RetryDelayTicks]>=0) AND [WarningCount]>=0");
                    table.CheckConstraint("CK_ExecutionAttemptHistory_Versions", "[HistorySchemaVersion]>0 AND [AttemptNumber]>0");
                    table.ForeignKey(
                        name: "FK_ExecutionAttemptHistory_ExecutionRunHistory_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalSchema: "history",
                        principalTable: "ExecutionRunHistory",
                        principalColumn: "ExecutionRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionPolicyHistory",
                schema: "history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HistorySchemaVersion = table.Column<int>(type: "int", nullable: false),
                    TimeoutPolicyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TimeoutPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    TimeoutTicks = table.Column<long>(type: "bigint", nullable: false),
                    RetryPolicyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RetryPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    MaximumAttempts = table.Column<int>(type: "int", nullable: false),
                    RetryDelayClassification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParallelPolicyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParallelPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ParallelMaximumConcurrency = table.Column<int>(type: "int", nullable: false),
                    ThrottlingPolicyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ThrottlingPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ThrottlingMaximumConcurrency = table.Column<int>(type: "int", nullable: false),
                    BatchingPolicyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BatchingPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    BatchingEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPolicyHistory", x => x.Id);
                    table.CheckConstraint("CK_ExecutionPolicyHistory_Values", "[TimeoutTicks]>0 AND [MaximumAttempts]>0 AND [ParallelMaximumConcurrency]>0 AND [ThrottlingMaximumConcurrency]>0");
                    table.CheckConstraint("CK_ExecutionPolicyHistory_Versions", "[HistorySchemaVersion]>0 AND [TimeoutPolicyVersion]>0 AND [RetryPolicyVersion]>0 AND [ParallelPolicyVersion]>0 AND [ThrottlingPolicyVersion]>0 AND [BatchingPolicyVersion]>0");
                    table.ForeignKey(
                        name: "FK_ExecutionPolicyHistory_ExecutionRunHistory_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalSchema: "history",
                        principalTable: "ExecutionRunHistory",
                        principalColumn: "ExecutionRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionStateTransitionHistory",
                schema: "history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HistorySchemaVersion = table.Column<int>(type: "int", nullable: false),
                    TransitionSequence = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ToState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TransitionedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FailureCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EventSchemaVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionStateTransitionHistory", x => x.Id);
                    table.CheckConstraint("CK_ExecutionStateTransitionHistory_Versions", "[HistorySchemaVersion]>0 AND [TransitionSequence]>0 AND [EventSchemaVersion]>0");
                    table.ForeignKey(
                        name: "FK_ExecutionStateTransitionHistory_ExecutionRunHistory_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalSchema: "history",
                        principalTable: "ExecutionRunHistory",
                        principalColumn: "ExecutionRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionStepHistory",
                schema: "history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HistorySchemaVersion = table.Column<int>(type: "int", nullable: false),
                    StepOrdinal = table.Column<int>(type: "int", nullable: false),
                    DependencyCount = table.Column<int>(type: "int", nullable: false),
                    StrategyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrategyVersion = table.Column<int>(type: "int", nullable: false),
                    PluginId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PluginVersion = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    QueueDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    WaitDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    StepOutcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FailureCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    WasThrottled = table.Column<bool>(type: "bit", nullable: false),
                    WasSkipped = table.Column<bool>(type: "bit", nullable: false),
                    WasCancelled = table.Column<bool>(type: "bit", nullable: false),
                    WasTimedOut = table.Column<bool>(type: "bit", nullable: false),
                    ArtifactFileCount = table.Column<int>(type: "int", nullable: false),
                    ArtifactObjectCount = table.Column<int>(type: "int", nullable: false),
                    ArtifactMetricCount = table.Column<int>(type: "int", nullable: false),
                    ArtifactByteCount = table.Column<long>(type: "bigint", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionStepHistory", x => x.Id);
                    table.CheckConstraint("CK_ExecutionStepHistory_Counts", "[DependencyCount]>=0 AND [QueueDurationTicks]>=0 AND [WaitDurationTicks]>=0 AND [ExecutionDurationTicks]>=0 AND [AttemptCount]>=0 AND [RetryCount]>=0 AND [ArtifactFileCount]>=0 AND [ArtifactObjectCount]>=0 AND [ArtifactMetricCount]>=0 AND [ArtifactByteCount]>=0 AND [WarningCount]>=0");
                    table.CheckConstraint("CK_ExecutionStepHistory_Versions", "[HistorySchemaVersion]>0 AND [StepOrdinal]>0 AND [StrategyVersion]>0 AND [PluginVersion]>0");
                    table.ForeignKey(
                        name: "FK_ExecutionStepHistory_ExecutionRunHistory_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalSchema: "history",
                        principalTable: "ExecutionRunHistory",
                        principalColumn: "ExecutionRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionArtifactHistory_ExecutionRunId",
                schema: "history",
                table: "ExecutionArtifactHistory",
                column: "ExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionArtifactHistory_ExecutionRunId_ExecutionStepId",
                schema: "history",
                table: "ExecutionArtifactHistory",
                columns: new[] { "ExecutionRunId", "ExecutionStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionArtifactHistory_ExecutionRunId_ExecutionStepId_ArtifactId",
                schema: "history",
                table: "ExecutionArtifactHistory",
                columns: new[] { "ExecutionRunId", "ExecutionStepId", "ArtifactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionAttemptHistory_ExecutionRunId_ExecutionStepId_AttemptNumber",
                schema: "history",
                table: "ExecutionAttemptHistory",
                columns: new[] { "ExecutionRunId", "ExecutionStepId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPolicyHistory_ExecutionRunId_ExecutionStepId",
                schema: "history",
                table: "ExecutionPolicyHistory",
                columns: new[] { "ExecutionRunId", "ExecutionStepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunHistory_CompletedAt",
                schema: "history",
                table: "ExecutionRunHistory",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunHistory_ExecutionOutcome_CompletedAt",
                schema: "history",
                table: "ExecutionRunHistory",
                columns: new[] { "ExecutionOutcome", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunHistory_ExecutionRunId",
                schema: "history",
                table: "ExecutionRunHistory",
                column: "ExecutionRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunHistory_FailureCategory_CompletedAt",
                schema: "history",
                table: "ExecutionRunHistory",
                columns: new[] { "FailureCategory", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunHistory_ManagedServerId_CompletedAt",
                schema: "history",
                table: "ExecutionRunHistory",
                columns: new[] { "ManagedServerId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunHistory_PluginId_CompletedAt",
                schema: "history",
                table: "ExecutionRunHistory",
                columns: new[] { "PluginId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRunHistory_StrategyCode_CompletedAt",
                schema: "history",
                table: "ExecutionRunHistory",
                columns: new[] { "StrategyCode", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStateTransitionHistory_ExecutionRunId_EntityType_ExecutionStepId_TransitionSequence",
                schema: "history",
                table: "ExecutionStateTransitionHistory",
                columns: new[] { "ExecutionRunId", "EntityType", "ExecutionStepId", "TransitionSequence" },
                unique: true,
                filter: "[ExecutionStepId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStateTransitionHistory_ExecutionRunId_TransitionSequence",
                schema: "history",
                table: "ExecutionStateTransitionHistory",
                columns: new[] { "ExecutionRunId", "TransitionSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStepHistory_ExecutionRunId_ExecutionStepId",
                schema: "history",
                table: "ExecutionStepHistory",
                columns: new[] { "ExecutionRunId", "ExecutionStepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStepHistory_ExecutionRunId_StepOrdinal",
                schema: "history",
                table: "ExecutionStepHistory",
                columns: new[] { "ExecutionRunId", "StepOrdinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionArtifactHistory",
                schema: "history");

            migrationBuilder.DropTable(
                name: "ExecutionAttemptHistory",
                schema: "history");

            migrationBuilder.DropTable(
                name: "ExecutionPolicyHistory",
                schema: "history");

            migrationBuilder.DropTable(
                name: "ExecutionStateTransitionHistory",
                schema: "history");

            migrationBuilder.DropTable(
                name: "ExecutionStepHistory",
                schema: "history");

            migrationBuilder.DropTable(
                name: "ExecutionRunHistory",
                schema: "history");
        }
    }
}
