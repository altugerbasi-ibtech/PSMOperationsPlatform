# Collector Plugin Contract

WP-008.5 descriptors add stable `PluginId` and explicit support declarations for cancellation, retry, timeout, parallel execution, batching, and read-only behavior. Registration is explicit and ordinal. This remains a minimal fake/test handler boundary, not third-party plugin support.

Status: **MINIMAL WP-008.4 HANDLER BOUNDARY — WP-008.5 DEFERRED**

`ICollectorExecutionHandler` exposes an immutable descriptor and one
cancellable asynchronous operation. Descriptor validation requires exact
StrategyCode, positive plugin/schema versions, ManagedTargetServer support,
read-only behavior, and cancellation support.

The runtime context contains safe managed-server, plan, run, step, attempt, and
time identities only. The immutable result reports Success, Failed, Cancelled,
or NoData plus safe reason/summary and optional non-negative byte/object
metrics. It contains no collected payload, credential, command, stream,
process, persistence service, or mutable state.

Only fake handlers exist in tests. Full production plugin packaging,
validation, discovery, and lifecycle remain WP-008.5 scope.
