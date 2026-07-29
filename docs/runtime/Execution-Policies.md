# Execution Policies

Dispatcher resolves policy references and validates them against declared handler capabilities. Runtime enforces the already resolved immutable policy and cannot downgrade it.

Policies are explicit version-1 product mappings. Timeout policy is immutable
and enforced with a linked handler token. Retry is based on completed attempts,
retryable failure category, attempts remaining, and external cancellation.

SerialCore allows one step. ParallelReadOnlyA allows two. Lightweight,
Standard, and Heavy throttle limits are four, two, and one. Dependency
constraints and the lower concurrency limit take precedence. Batching is
inactive because WP-008.3 declared no batch groups.
