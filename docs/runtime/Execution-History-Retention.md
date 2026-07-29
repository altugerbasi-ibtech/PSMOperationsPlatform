# Execution History Retention

Retention v1.0 follows the
[retention contract](../contracts/Execution-History-Retention-Contract.md).
Selection is stable by completion time then execution-run ID and bounded by
batch size. Children are removed before their run.

Cleanup is repository-capable but unscheduled. Production authorization,
cadence, capacity validation, and execution remain integration work.
