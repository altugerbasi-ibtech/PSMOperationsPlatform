# Collector Plugin SDK Contract

SDK version 1.0 owns `ICollectorPlugin`, version/descriptor, validation, immutable context/policy, result, warning/diagnostic and artifact contracts. Application references SDK; SDK references no Application or Infrastructure project.

Descriptor and registration validation is deterministic, ordinal and explicit. Runtime contract 1.0 supports SDK 1.0 only. Dispatcher validates SDK compatibility, plugin validation and policy capabilities before Runtime submission. Plugins receive no Runtime state, retry controller, catalog, registry, Dispatcher, persistence or service locator.

Result metrics are non-negative. When file/object artifacts exist, byte/object totals equal their deterministic sums. Artifacts contain bounded metadata only; production storage is absent.
