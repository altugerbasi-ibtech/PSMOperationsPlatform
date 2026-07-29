# Execution Metric Catalog

Status: **IMPLEMENTED — INTEGRATION PENDING**

`ExecutionMetricCatalog.Definitions` is the single authoritative source for
instrument names, types, units and policy metadata. These category documents
mirror that source; they do not define additional instruments.

Every entry uses instrumentation version 1.0. Allowed dimensions are the
bounded `strategy.code`, `plugin.id`, `execution.outcome`,
`failure.category`, `reason.code`, `subject`, `sdk.major_version`,
`runtime.contract_version` and `certification.status`. Prohibited dimensions
are ManagedServerId, ExecutionRunId, ExecutionPlanId, step/target/FQDN/IP/
machine identity, artifact/file/path/URL, exception/stack text, user identity
and arbitrary plugin metadata. Expected cardinality is bounded by explicit
registration and product enums.

Duplicate events are suppressed while retained in the bounded observer set.
Negative histogram measurements are ignored; counters/gauges never record
negative values. Instrument failures are diagnosed and isolated from
Dispatcher, Runtime and Execution State.

- [Execution metrics](Execution-Metrics.md)
- [Dispatcher metrics](Dispatcher-Metrics.md)
- [Runtime metrics](Runtime-Metrics.md)
- [Plugin metrics](Plugin-Metrics.md)
- [Artifact metrics](Artifact-Metrics.md)
- [Health metrics](Health-Metrics.md)
- [Cardinality policy](Cardinality-Policy.md)
