# Execution Context Contract

`ExecutionContext` is an immutable per-step contract containing fixed identities, subject, strategy/plugin versions, plan/capability/inventory provenance, schema versions, and `TimeProvider`. `CancellationToken` is passed explicitly to handler execution.

It contains no `DbContext`, `IServiceProvider`, `IConfiguration`, credentials, connection strings, target session, mutable plan/state, registry, policy catalog, command text, or mutable dictionary.
