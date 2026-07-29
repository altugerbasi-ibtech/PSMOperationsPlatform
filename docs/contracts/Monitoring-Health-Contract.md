# Monitoring Health Contract

The advisory health assessment uses eight explicit dimensions totaling 100:
subscriber 25, metric pipeline 20, Activity pipeline 15, event integrity 10,
failure pressure 10, timeout pressure 10, rejection pressure 5 and warning
pressure 5. Insufficient observation is Unknown; 90–100 is Healthy, 70–89
Degraded and 0–69 Unhealthy. It never controls execution or represents target
health.

Status: **IMPLEMENTED — INTEGRATION PENDING**

The immutable snapshot reports schema/status, active/waiting/throttled counts,
recent adverse counts, last-event/run timestamps, pipeline flags, bounded
alerts and the latest 32 diagnostics. The adverse window is 15 minutes and
each queue is capped at 256.

Status is Unknown before observation, Healthy with healthy pipelines and no
recent adverse signals, Degraded for recent failures/timeouts/cancellations or
rejections, and Unhealthy for instrumentation/subscriber failure. This is
monitoring-pipeline/current-execution health, not target health, connectivity,
capability support or SLA compliance.
