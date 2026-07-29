# Monitoring Health

The snapshot exposes an advisory deterministic health score and rating. The
score never replaces monitoring status, controls Runtime, affects dispatch
eligibility, represents target health or triggers remediation.

Current health is an in-memory bounded projection using `TimeProvider`. A
15-minute window and 256-item per-category cap prevent history growth.
Thresholds are three Runtime failures, three timeouts or three dispatch
rejections; a subscriber failure signals immediately. Signals are projections
only and neither notify nor control Runtime.

The Windows Collector's existing health-check registry includes a narrow
`execution-monitoring` adapter. It reads only the bounded projection and adds
no endpoint or target/SQL query. Production endpoint exposure and validation
remain integration-pending for WP-007.Z.
