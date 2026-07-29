# Execution History Query Contract

`IExecutionHistoryQueryService` exposes exact run lookup and explicit run,
step, attempt, transition, artifact, and policy reads. It returns immutable
models and never exposes `IQueryable`, EF entities, configuration, or a service
locator.

Run lists allow a bounded date range and approved exact filters. Both date
bounds are required together. Offset paging defaults to 50 and is capped at
200. Ordering is `CompletedAt` descending followed by `ExecutionRunId`; child
ordering uses step ordinal, attempt number, transition sequence, and ordinal
artifact ID. Infrastructure uses `AsNoTracking` and propagates cancellation.
