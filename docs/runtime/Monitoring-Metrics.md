# Execution Monitoring Metrics

This file is retained as navigation. The structured category catalog is
[docs/runtime/metrics](metrics/README.md); source code remains authoritative.

The explicit catalog defines:

- run/step/attempt start and terminal counters;
- retry, dispatch-rejection and validation/compatibility counters;
- artifact and warning counters where typed evidence is available;
- queue, wait, attempt, step, run, retry-delay and artifact-byte histograms
  where the event supplies a valid measurement;
- active run/step/waiting/throttled observable gauges.

Counts use `count`, durations use `s`, and size uses `By`. Negative durations
are ignored. Instrumentation failure is diagnosed and isolated.
