# Collector Strategy Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

A strategy is declarative and independently versioned. It has a stable code,
display metadata, ManagedTargetServer subject, typed category, priority,
execution order, read-only/manual-approval policy, required and optional
capabilities, statuses, stable reason/explanation, prerequisite groups, source
identity, warnings, and provenance.

Every WP-008.2 strategy is read-only and requires no manual approval. No
strategy stores executable code, commands, credentials, sessions, paths,
process handles, or assembly names. Order is Priority, ExecutionOrder, then
ordinal StrategyCode.
