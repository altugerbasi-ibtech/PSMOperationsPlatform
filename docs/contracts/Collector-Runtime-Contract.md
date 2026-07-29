# Collector Runtime Contract

`ICollectorRuntime` consumes `PreparedExecutionDispatch`. Runtime has no registry or policy-catalog dependency and never resolves `StrategyCode`; it invokes the supplied fake/test handler under the supplied immutable policy/context.

Status: **IMPLEMENTED — INTEGRATION PENDING**

`ICollectorRuntime.ExecuteAsync` accepts a narrow immutable projection of one
committed Execution Plan plus `CancellationToken`. It validates schema,
ManagedTargetServer subject, readiness, provenance, unique steps, exclusion
separation, read-only policy, dependencies, and policy references.

It creates a new current run, coordinates explicitly registered handlers,
persists transition boundaries, emits lifecycle events, and returns terminal
state. External cancellation persists cancellation where possible and
propagates `OperationCanceledException`. The contract exposes no EF context,
service provider, configuration, credentials, raw upstream facts, command
text, or target session.
