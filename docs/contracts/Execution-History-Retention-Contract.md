# Execution History Retention Contract

Version-1 repository defaults are:

| Fact | Retention |
|---|---:|
| Runs, steps, attempts, policy and artifact metadata | 180 days |
| State transitions | 90 days |
| Failed projection diagnostics | 90 days |

`TimeProvider` determines stable cutoffs. Cleanup selects at most 500 terminal
run aggregates by default, removes children before parents, supports
cancellation, is idempotent, and returns committed counts. It never deletes
active Execution State.

No production cleanup scheduler is introduced by WP-008.8.
