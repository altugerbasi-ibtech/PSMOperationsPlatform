# Execution State Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

`ExecutionRunState`, `ExecutionStepState`, and `ExecutionAttemptState` are
mutable runtime evidence under `ExecutionStateSchemaVersion = 1`. Statuses are
strongly typed and transitions validated. Attempts have positive sequential
numbers; retry count is derived.

State retains execution-plan and upstream provenance, safe failure category,
reason, summary, timestamps, durations, non-negative metrics, and RowVersion.
It contains no plan mutation, credentials, session, command, payload, or
`RemainingRetries`. Only current state is implemented; history is deferred.
