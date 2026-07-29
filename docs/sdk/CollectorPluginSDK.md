# Collector Plugin SDK

Plugin monitoring readiness evaluates only explicit descriptor and supplied
evidence. It is separate from SDK compatibility, certification and any future
Plugin Quality Score, and never affects Dispatcher eligibility. See
[Plugin Monitoring Readiness](Plugin-Monitoring-Readiness.md).

SDK 1.0 also defines deterministic compatibility badges, advisory
certification metadata and safe optional package metadata. These values never
override Dispatcher validation, authorize deployment or load packages. See
[version policy](SDK-Version-Policy.md), [changelog](CHANGELOG.md), and
[sample plugins](Sample-Plugins.md).

WP-008.6 implements public SDK version 1.0 in dependency-minimal `PSMOperationsPlatform.CollectorSdk`. Plugins implement `ICollectorPlugin`, return immutable descriptors and validation/results, consume immutable `ExecutionContext` and `ExecutionPolicy`, and receive cancellation explicitly.

Descriptors declare stable PluginId/StrategyCode, plugin and schema versions, minimum/target SDK versions, subjects, read-only status, cost, capability prerequisites, cancellation/retry/timeout/parallel/batch support and artifact schema support. Registration is explicit and validates duplicate identifiers, descriptor structure and Runtime compatibility.

All plugins remain repository-built. Third-party binary loading, marketplace, directory scanning, dynamic installation, signing/trust, production artifact storage and real integration are not supported and remain deferred.
