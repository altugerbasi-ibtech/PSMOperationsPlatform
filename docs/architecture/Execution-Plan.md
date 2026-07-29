# Execution Plan

WP-008.5 leaves persisted plans immutable and unchanged. Plans retain `StrategyCode` and versioned policy references; Dispatcher resolves those references without modifying the plan.

Status: **IMPLEMENTED — INTEGRATION PENDING**

The Execution Plan Engine synchronously converts one committed
ManagedTargetServer Decision Plan into an immutable operational arrangement.
It answers how approved strategies are arranged; the Decision Engine continues
to answer what should run.

Eligible, Ready, read-only, no-approval decisions become one step per
StrategyCode. Other decisions become explainable exclusions. Version-1 policy
mappings assign bounded Short/Standard/Long read-only timeouts, declarative
NoRetry/StandardReadOnlyRetry references, explicit SerialCore or
ParallelReadOnlyA groups, and Lightweight/Standard/Heavy throttling classes.
Batch groups and dependencies are empty because no genuine current operational
need exists.

Order is dependency-safe Priority, ExecutionOrder, then ordinal StrategyCode;
StepSequence is assigned last. Step identity is deterministically derived from
DecisionPlanId and StrategyCode using SHA-256, not database identity or
GetHashCode. ExecutionPlanSchemaVersion 1 is independent of all upstream and
policy versions.

One current plan per managed server is replaced atomically with steps and
exclusions. Cancellation or catastrophic failure preserves the prior plan.
Planning performs no infrastructure access or execution.

WP-008.4 loads this committed plan through a narrow read-only projection.
Runtime creates separate mutable state but cannot change selection, identity,
order, policy references, grouping, throttling, dependencies, or read-only
declarations.
