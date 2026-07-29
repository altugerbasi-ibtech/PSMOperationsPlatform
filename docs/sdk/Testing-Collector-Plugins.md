# Testing Collector Plugins

Monitoring-readiness tests cover Ready, PartiallyReady, NotReady and Unknown,
deterministic local badge output, missing evidence and separation from
compatibility, certification and dispatch.

Contract suites must cover descriptor/package/certification validation,
compatibility badge output, deterministic artifacts/results, `NoData`, safe
failure, timeout cooperation and cancellation. Samples must remain absent from
production registration.

Tests should construct fixed context and resolved policy values, validate the normalized descriptor and Runtime compatibility, exercise normal validation failures without exceptions, verify cancellation, assert all result outcomes and metric consistency, and compare logical artifact ordering. Use fake time and deterministic identifiers where relevant.

Architecture tests must verify the SDK has no Infrastructure, EF Core or SQL client reference and plugin surfaces expose no service locator, configuration, database, credentials, commands or mutable Runtime state.
