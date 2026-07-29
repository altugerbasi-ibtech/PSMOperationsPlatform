# Execution Monitoring

Monitoring remains current non-durable observation. Durable terminal facts
belong to [Execution History](Execution-History.md); monitoring metrics,
Activities, snapshots, and health assessments are not stored as History.

WP-008.7.Q exposes the bounded current projection through
`IExecutionMonitoringSnapshotProvider`. The immutable snapshot and its
deterministic 100-point health assessment are advisory read models, not a new
state authority. Metric definitions remain code-owned. Monitoring snapshots
are neither Execution History nor Audit; see
[Execution History versus Audit](Execution-History-vs-Audit.md).

Status: **IMPLEMENTED — INTEGRATION PENDING**

Execution Monitoring is the observation responsibility already frozen after
Execution State. Dispatcher and Runtime publish typed lifecycle events to
independent logging and monitoring subscribers. Monitoring maps supported
events explicitly into standard .NET `Meter` and `ActivitySource`
instrumentation and a bounded current-health projection.

Monitoring observes; it never resolves or executes plugins, controls Runtime,
changes policy, retries/cancels work, or mutates plans, contexts, results or
Execution State. State remains authoritative. Delivery is in-process,
best-effort and non-durable. No exporter, backend, notification or history is
implemented. History is WP-008.8; live integration is WP-007.Z.
