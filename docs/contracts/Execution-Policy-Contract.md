# Execution Policy Contract

WP-008.5 resolves all versioned plan references into one immutable `ExecutionPolicy` containing schema version, timeout, retry, parallel, throttling, and inactive batching semantics. Dispatcher owns catalog access; Runtime receives the resolved value.

Status: **IMPLEMENTED — VERSION 1**

The explicit catalog resolves plan references for ShortReadOnly (1 minute),
StandardReadOnly (5 minutes), LongReadOnly (15 minutes), NoRetry (one attempt),
StandardReadOnlyRetry (two attempts and one deterministic delay), SerialCore
(one), ParallelReadOnlyA (two), and Lightweight/Standard/Heavy throttling
(four/two/one).

Unknown codes and unsupported versions fail safely. Resolution is deterministic
and performs no infrastructure access. Batching is inactive. Retry state and
execution remain runtime-owned.
