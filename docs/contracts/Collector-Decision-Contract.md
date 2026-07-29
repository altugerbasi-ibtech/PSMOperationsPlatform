# Collector Decision Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

`ICollectorDecisionEngine.Evaluate` is synchronous and deterministic over one
immutable managed-target capability snapshot. Input carries server, snapshot,
source inventory run/version, schema, subject, timestamp/status, and typed
capability entries only.

DecisionSchemaVersion 1 retains every outcome: Eligible, Blocked,
Indeterminate, NotApplicable, Disabled, or Invalid. Eligibility and execution
readiness are separate. Unknown required evidence yields Indeterminate.
Catastrophic input inconsistency rejects the plan; an individual rule failure
yields one Invalid strategy.

The contract contains no DbContext, inventory DTO, logger, service provider,
credentials, session, raw facts, connection, command, delegate, or collector.
