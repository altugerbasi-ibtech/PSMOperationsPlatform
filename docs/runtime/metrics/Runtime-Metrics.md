# Runtime Metrics

All entries inherit [catalog policy](README.md).

| Metric | Type | Unit | Description | Source | Recording condition | Cardinality |
|---|---|---|---|---|---|---|
| `psm.execution.retries` | Counter | count | Scheduled retries | ExecutionStepRetryScheduled | Valid typed event | Bounded |
| `psm.execution.retry.delay` | Histogram | s | Retry delay | ExecutionStepRetryScheduled | Non-negative duration present | Bounded |
| `psm.execution.runs.active` | ObservableGauge | count | Current active runs | Bounded projection | Snapshot observation | No dimensions |
| `psm.execution.steps.active` | ObservableGauge | count | Current active steps | Bounded projection | Snapshot observation | No dimensions |
| `psm.execution.steps.waiting` | ObservableGauge | count | Current waiting steps | Bounded projection | Snapshot observation | No dimensions |
| `psm.execution.steps.throttled` | ObservableGauge | count | Current throttled steps | Bounded projection | Snapshot observation | No dimensions |

Timeout and cancellation outcomes use the execution counters. Runtime timeout
policies are unchanged by monitoring.
