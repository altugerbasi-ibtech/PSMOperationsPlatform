# Monitoring Metric Contract

The structured definitions in `ExecutionMetricCatalog.Definitions` are the
single code authority. Each entry records type, unit, source/condition,
dimension policy, cardinality, duplicate/negative behavior, failure isolation
and instrumentation version. Category documentation begins at the
[runtime metric catalog](../runtime/metrics/README.md).

Status: **IMPLEMENTED — INTEGRATION PENDING**

Instrumentation identity is `PSMOperationsPlatform.Execution`, version 1.0.
Counters use unit `count`, durations use seconds, and artifact sizes use bytes.
The catalog covers run, step, attempt, retry, dispatch rejection,
validation/compatibility, artifact/warning, queue/wait/attempt/step/run/retry
duration and current bounded gauges.

Events record only measurements they can prove. Approved dimensions are
strategy/plugin identity and bounded product-owned outcome/failure/reason,
subject, SDK/Runtime and certification values. Target, server, run, plan, step,
FQDN, address, file/artifact, URL, exception and arbitrary metadata dimensions
are prohibited.
