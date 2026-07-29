# Execution Dispatcher Contract

`ExecutionDispatchRequest` contains one committed immutable Execution Plan input. `PreparedExecutionDispatch` contains one run identity and a read-only collection of prepared steps. Each prepared step contains the unchanged plan step, explicitly resolved handler, immutable descriptor, immutable resolved policy, and immutable context.

`IExecutionDispatcher.DispatchAsync` validates, resolves, prepares, emits lifecycle events, and calls `ICollectorRuntime.ExecuteAsync` only after every executable step is compatible. Rejection returns a stable disposition, failure category, reason, and safe explanation with no Runtime result.

Resolution is ordinal, deterministic, version-aware, and infrastructure-free. Duplicate registration fails. Cancellation propagates. Dispatcher never executes the handler or creates attempt state.
