# Execution History Lifecycle

1. Runtime executes without a history transaction.
2. Runtime persists current state and emits typed best-effort events.
3. Terminal state and prepared dispatch form immutable projection input.
4. Explicit mapping produces ordered safe history facts.
5. Infrastructure writes the complete aggregate in a short transaction.
6. Bounded no-tracking readers expose historical facts.

No transaction spans plugin execution, retry delay, timeout, WinRM, target
access, or subscriber callbacks. History failure does not rewrite execution
outcome.
