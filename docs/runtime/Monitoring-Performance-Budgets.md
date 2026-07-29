# Monitoring Performance Budgets

Status: **ADVISORY LOCAL REGRESSION GOALS**

## Correctness requirements

Monitoring remains bounded, deterministic, failure-isolated, non-durable and
free of target/database access. Correctness takes precedence over timing.

## Local goals

| Operation | Goal |
|---|---:|
| Simple metric-only event processing median | below 1 ms |
| Event processing with Activity update median | below 2 ms |
| Health projection update median | below 1 ms |
| Snapshot generation median | below 5 ms |

Health assessment and plugin-readiness evaluation must remain bounded and
comfortably below 5 ms for synthetic fixed inputs. These are not Runtime
timeouts and never modify timeout, retry or throttling policy.

## Allocation and concurrency

Do not retain event payloads or artifacts, build exception text on success, or
grow collections without a fixed cap. Concurrent snapshot reads must be safe.
No critical lock is held across an external callback, target operation or
database access.

## Repository validation

BenchmarkDotNet is not present and no dependency is added. Deterministic tests
run 10,000 synthetic snapshot/health/readiness calculations with fixed
`TimeProvider`, require completion within a generous 15-second boundedness
guard, and assert collection caps. This gate is correctness-oriented, not a
microbenchmark claim.

Informational result format:

| Benchmark | Event/input | Iterations | Measurement | Allocation | Classification |
|---|---|---:|---|---|---|
| Quality calculation loop | synthetic fixed run event and SDK descriptor | 10,000 | recorded by test runner; machine-variable | not enforced | informational boundedness guard |

No production performance, exporter, backend or target result is claimed.
Production measurements remain WP-007.Z.
