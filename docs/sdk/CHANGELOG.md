# Collector Plugin SDK Changelog

## 1.0 — 2026-07-29

Initial supported repository contract:

- immutable plugin, descriptor, validation, execution context/policy, result,
  warning, diagnostic and bounded artifact contracts;
- Runtime 1.0 ↔ SDK 1.0 explicit compatibility;
- explicit registration without scanning or dynamic loading;
- deterministic compatibility badge;
- advisory certification and safe package metadata;
- Hello, NoData, Failure, LongRunning and Cancellation examples.
- advisory plugin monitoring-readiness assessment and local badge;
- bounded Execution Monitoring snapshot/read quality integration.

No APIs are deprecated and no evidence supports an earlier public release.
`PluginVersion`, `RuntimeVersion`, descriptor/artifact schema versions and SDK
version remain distinct.
