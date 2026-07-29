# Plugin Authoring Guide

Authors may explicitly supply manifest, documentation, contract-test,
safe-failure-result and quality-assessment evidence to the advisory monitoring
readiness evaluator. Missing evidence is reported rather than inferred.

Authors may supply validated advisory certification and safe package metadata.
Neither affects dispatch. Use the five non-production samples to test success,
valid-empty, failure, long-running cancellation and immediate cancellation.

Implement `ICollectorPlugin` in a repository-built project referencing only `PSMOperationsPlatform.CollectorSdk`.

1. Return a stable immutable descriptor targeting SDK 1.0.
2. Declare every execution capability explicitly and remain read-only/cancellation-aware.
3. Validate subject, artifact schema and resolved policy deterministically without target or infrastructure access.
4. Execute asynchronously using immutable context/policy and explicit cancellation.
5. Return a safe immutable result with bounded artifacts and consistent metrics.
6. Register explicitly in approved composition; registration order is not execution order.

Constructors may use `TimeProvider` or SDK-owned stateless helpers. Do not inject `IServiceProvider`, `IConfiguration`, database contexts, credentials, arbitrary HTTP/process/filesystem/registry facilities or Runtime/Dispatcher internals. Production target-access abstractions require a separately approved package.
