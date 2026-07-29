# Monitoring Query Examples

Status: **DOCUMENTATION EXAMPLES — EXPORTER/BACKEND NOT CONFIGURED**

Repository instrumentation uses dotted names and dotted tag keys. A future
Prometheus exporter commonly normalizes dots to underscores; exact translation
must be verified at WP-007.Z. These examples are not production-validated.

## OpenTelemetry concepts

Select Meter `PSMOperationsPlatform.Execution` version 1.0 and export only the
bounded catalog. Example source instruments include
`psm.execution.runs.failed`, `psm.execution.steps.timed_out`,
`psm.execution.retries`, `psm.execution.dispatch.rejected`,
`psm.execution.run.duration`, `psm.execution.runs.active` and
`psm.execution.steps.active`.

## Prometheus-compatible concepts

Assuming conventional normalization:

```promql
rate(psm_execution_runs_failed_total[5m])
rate(psm_execution_steps_timed_out_total[5m])
rate(psm_execution_runs_cancelled_total[5m])
rate(psm_execution_retries_total[5m])
rate(psm_execution_dispatch_rejected_total[5m])
rate(psm_execution_run_duration_sum[5m]) / rate(psm_execution_run_duration_count[5m])
histogram_quantile(0.95, sum by (le) (rate(psm_execution_run_duration_bucket[5m])))
psm_execution_runs_active
psm_execution_steps_active
sum by (plugin_id) (rate(psm_execution_steps_failed_total[5m]))
histogram_quantile(0.95, sum by (le, strategy_code) (rate(psm_execution_step_duration_bucket[5m])))
```

The corresponding authoritative repository instruments are the dotted names
above; `plugin_id` and `strategy_code` represent normalized `plugin.id` and
`strategy.code`. No target/run/plan/FQDN dimension is permitted.

## Grafana concepts

Future panels may show bounded run outcomes, retry/timeout/rejection rates,
duration percentiles and active counts using the preceding queries. No
dashboard asset, data source or backend is created by WP-008.7.Q.

## .NET Activity filtering

Listen to ActivitySource `PSMOperationsPlatform.Execution` and filter bounded
`strategy.code`, `plugin.id`, `execution.outcome`, `failure.category` and
`reason.code`. Do not filter on target or execution identifiers as metric
dimensions.

## Health snapshot usage

Resolve `IExecutionMonitoringSnapshotProvider` and call
`GetCurrentSnapshot()` for a bounded immutable current view. Treat
`HealthScore` as advisory monitoring health, not target health, SLA evidence or
Runtime control.
