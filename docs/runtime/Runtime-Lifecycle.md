# Runtime Lifecycle

WP-008.5 inserts a preparation lifecycle before run creation: requested → handler resolved → policy resolved → prepared → submitted, or rejected. Runtime lifecycle begins only after a complete `PreparedExecutionDispatch` is submitted.

A committed Ready or PartiallyReady Execution Plan is loaded, validated, and
represented by Created, Queued, then Running state. Executable steps are queued,
wait for dependencies/capacity, run attempts, and terminate as Completed,
Failed, TimedOut, Cancelled, or Skipped.

All-success runs are Completed. Isolated step failures/timeouts produce
CompletedWithFailures. External cancellation produces Cancelled. Catastrophic
or corrupt orchestration produces Failed. Plan exclusions are never executed
and do not count as runtime failures.
