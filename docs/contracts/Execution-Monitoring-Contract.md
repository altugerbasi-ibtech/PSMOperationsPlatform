# Execution Monitoring Contract

Monitoring and History may observe the same lifecycle independently.
Monitoring remains non-durable and History failure does not change monitoring
or Runtime outcomes.

`ExecutionMonitoringSnapshotSchemaVersion = 1` identifies the immutable
current-read contract. It includes TimeProvider-generated identity/window
times, instrumentation identity, current status/score/rating, pipeline flags,
bounded activity/outcome counts, safe terminal timestamps and ordinal bounded
warning/alert/reason collections. `GetCurrentSnapshot()` is read-only,
thread-safe, synchronous and bounded. It cannot control Runtime or Dispatcher.

Status: **IMPLEMENTED — INTEGRATION PENDING**

`ExecutionMonitoringSchemaVersion` is 1 and independent of event/state/plan,
SDK, Runtime, plugin and artifact versions. `ExecutionMonitoringSubscriber`
accepts immutable `ExecutionEvent` version 1, uses explicit mappings and
exposes immutable snapshots through `IExecutionMonitoring`.

Unsupported, invalid, duplicate or out-of-order events produce bounded safe
diagnostics and do not throw into execution. Caller cancellation propagates.
No `DbContext`, service provider, configuration, mutable state, raw result or
artifact payload enters the monitoring contract.
