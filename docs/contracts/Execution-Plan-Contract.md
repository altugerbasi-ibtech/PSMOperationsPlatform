# Execution Plan Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

`IExecutionPlanEngine.Build` synchronously consumes one immutable committed
Decision Plan projection. Input contains only managed-target identity, upstream
schema/source versions, decision metadata, strategy statuses/order/policy
flags, reasons, warnings, and safe provenance.

ExecutionPlanSchemaVersion 1 produces immutable Ready, PartiallyReady, Empty,
or Invalid plan semantics. Executable steps retain deterministic logical IDs,
sequence, priority/order, explicit timeout/retry policy versions, parallel
group, throttling class, optional batch group, empty current dependency sets,
read-only policy, and upstream provenance.

Non-executable decisions are exclusions with stable dispositions, reasons,
explanations, blocking/unknown capabilities, and provenance. A strategy cannot
be both a step and exclusion. Contracts contain no DbContext, service provider,
plugin, delegate, command, session, credential, raw fact, attempt, runtime
status, or retry schedule.
