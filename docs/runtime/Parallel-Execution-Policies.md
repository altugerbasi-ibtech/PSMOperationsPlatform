# Parallel Execution Policies

`SerialCore` v1 has maximum concurrency one. `ParallelReadOnlyA` v1 permits at most two when dependencies and throttling also allow it. Dispatcher requires explicit plugin parallel capability for a parallel policy. Runtime owns scheduling and cancellation; read-only alone never implies parallel safety. Additional groups and distributed execution are deferred.

Dependencies and throttling override group capacity. Metrics remain per step regardless of completion order. Tests cover serial and bounded parallel behavior, deterministic identity, capability rejection, dependency precedence, failure isolation and cancellation.
