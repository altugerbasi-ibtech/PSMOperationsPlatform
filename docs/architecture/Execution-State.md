# Execution State

Execution State is the mutable current execution authority. Terminal facts may
be copied into [Execution History](Execution-History.md), but History does not
control or reconstruct active State and may temporarily lag it.

Execution State remains the authoritative mutable runtime record. WP-008.7
current health, metrics and Activities are non-durable observations and do not
replace, mutate or persist Execution State.

WP-008.5 does not add artifact payloads to mutable state. State retains runtime transitions and aggregate metrics; immutable plan, policy, context, and artifacts remain separate.

WP-008.6 preserves this rule: SDK results can contribute approved counts and references only; plugin descriptors, SDK compatibility and artifact payloads do not become mutable state.

Status: **CURRENT RUNTIME STATE IMPLEMENTED — HISTORY DEFERRED**

Execution State is mutable runtime evidence separate from immutable plan
intent. `ExecutionStateSchemaVersion = 1` is independent from upstream,
policy, plugin, event, and runtime versions.

Run, Step, and Attempt are separate. Their strongly typed transitions prevent
terminal state from returning to Running. RowVersion supports optimistic
concurrency. Attempts begin at one; RetryCount is derived as AttemptCount minus
one and `RemainingRetries` is not stored.

Queue, wait, execution, and total durations use `TimeProvider`. Handler-reported
non-negative bytes and objects are retained per attempt/step. Run collected
totals include successful terminal steps; failed-attempt metrics remain
available at attempt level.

Only current state is persisted. Execution history, retention, reporting,
monitoring, and durable event history remain deferred to WP-008.7/WP-008.6.
