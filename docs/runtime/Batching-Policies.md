# Batching Policies

`NoBatch` v1 is the only implemented batching policy and is disabled. Dispatcher resolves it and would require explicit plugin batch capability for enabled batching. Runtime does not combine executions, and the SDK provides no batch call. Batch execution and configuration remain deferred.

Cancellation and metrics therefore remain per step. Unknown/enabled batch references fail safely. Tests verify the disabled default, immutable version, no combined calls and compatibility rejection behavior.
