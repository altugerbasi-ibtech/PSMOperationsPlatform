# Dispatcher Metrics

All entries inherit [catalog policy](README.md).

| Metric | Type | Unit | Description | Source | Recording condition | Cardinality |
|---|---|---|---|---|---|---|
| `psm.execution.dispatch.rejected` | Counter | count | Dispatch rejections | ExecutionDispatchRejected | Valid rejection event | Bounded |
| `psm.execution.plugin.validation_failures` | Counter | count | Plugin validation rejections | ExecutionDispatchRejected | Bounded validation reason | Bounded |
| `psm.execution.sdk.compatibility_failures` | Counter | count | SDK incompatibility | ExecutionDispatchRejected | Bounded SDK reason | Bounded |
| `psm.execution.policy.compatibility_failures` | Counter | count | Policy incompatibility | ExecutionDispatchRejected | Bounded policy reason | Bounded |

Accepted dispatch and handler-resolution failure instruments are not currently
implemented; documentation does not invent them.
