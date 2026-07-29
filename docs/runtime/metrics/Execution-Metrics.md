# Execution Metrics

All entries inherit the version, dimension, duplicate, negative-value and
failure-isolation policies in [README](README.md).

| Metric | Type | Unit | Description | Source | Recording condition | Cardinality |
|---|---|---|---|---|---|---|
| `psm.execution.runs.started` | Counter | count | Runs started | ExecutionRunStarted | Valid typed event | Bounded |
| `psm.execution.runs.completed` | Counter | count | Runs completed | ExecutionRunCompleted | Valid typed event | Bounded |
| `psm.execution.runs.failed` | Counter | count | Runs failed | ExecutionRunFailed | Valid typed event | Bounded |
| `psm.execution.runs.cancelled` | Counter | count | Runs cancelled | ExecutionRunCancelled | Valid typed event | Bounded |
| `psm.execution.steps.started` | Counter | count | Steps started | ExecutionStepStarted | Valid typed event | Bounded |
| `psm.execution.steps.completed` | Counter | count | Steps completed | ExecutionStepCompleted | Valid typed event | Bounded |
| `psm.execution.steps.failed` | Counter | count | Steps failed | ExecutionStepFailed | Valid typed event | Bounded |
| `psm.execution.steps.timed_out` | Counter | count | Steps timed out | ExecutionStepTimedOut | Valid typed event | Bounded |
| `psm.execution.steps.cancelled` | Counter | count | Steps cancelled | ExecutionStepCancelled | Valid typed event | Bounded |
| `psm.execution.steps.skipped` | Counter | count | Steps skipped | ExecutionStepSkipped | Valid typed event | Bounded |
| `psm.execution.attempts.started` | Counter | count | Attempts started | ExecutionStepAttemptStarted | Valid typed event | Bounded |
| `psm.execution.attempts.completed` | Counter | count | Attempts completed | ExecutionStepAttemptCompleted | Valid typed event | Bounded |
| `psm.execution.attempts.failed` | Counter | count | Failed attempts | ExecutionStepAttemptCompleted | Failure category is non-None | Bounded |
| `psm.execution.queue.duration` | Histogram | s | Queue duration | Typed event duration | Non-negative proven duration | Bounded |
| `psm.execution.wait.duration` | Histogram | s | Wait duration | Typed event duration | Non-negative proven duration | Bounded |
| `psm.execution.attempt.duration` | Histogram | s | Attempt duration | ExecutionStepAttemptCompleted | Non-negative duration present | Bounded |
| `psm.execution.step.duration` | Histogram | s | Step duration | Terminal step event | Non-negative duration present | Bounded |
| `psm.execution.run.duration` | Histogram | s | Run duration | Terminal run event | Non-negative duration present | Bounded |
