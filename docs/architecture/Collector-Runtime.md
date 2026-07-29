# Collector Runtime

Runtime continues to own execution and current State. It has no dependency on
the Infrastructure History writer; terminal State and typed events are safe
projection inputs after execution.

Runtime publishes typed lifecycle events and has no dependency on the WP-008.7
monitoring implementation. Monitoring failures cannot change Runtime results,
retry, timeout, cancellation, throttling, dependencies or state.

## WP-008.5 dispatcher handoff

Runtime accepts `PreparedExecutionDispatch`; it does not resolve `StrategyCode`, query the handler registry, retrieve descriptors, or query the policy catalog. It executes the supplied handler and immutable resolved policy/context while retaining dependency, throttling, timeout, retry, cancellation, isolation, event, and Execution State ownership.

WP-008.6 changes the supplied boundary to public `ICollectorPlugin` without changing Runtime ownership. Runtime does not validate SDK compatibility or construct plugins.

Status: **IMPLEMENTED — INTEGRATION PENDING**

The Collector Runtime consumes one committed immutable managed-target
Execution Plan. It validates schema, subject, provenance, step uniqueness,
policies, dependencies, read-only intent, and exclusions before creating
version-1 mutable state. It does not reevaluate upstream engines or mutate the
plan.

Handlers resolve by exact ordinal StrategyCode from explicit registration.
Descriptors must be immutable, positively versioned, cancellable, read-only,
and support ManagedTargetServer. The context exposes only safe identity,
step/attempt identity, and `TimeProvider`.

The runtime owns timeout and retry. SerialCore is limited to one concurrent
step; ParallelReadOnlyA to two. Lightweight, Standard, and Heavy throttling
limits are four, two, and one; the lower applicable limit wins. Dependencies
override grouping. Batching is inactive.

State transitions persist before in-process events. Event failure is classified
while state stays authoritative. Handler exceptions, malformed results,
timeouts, missing handlers, and failed dependencies are isolated. External
cancellation propagates and is distinct from timeout.

Production automatic triggering is disabled. WP-008.4 registers no production
handler and performs no target or infrastructure access.
