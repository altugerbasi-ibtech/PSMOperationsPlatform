# Collector Plugin

## WP-008.5 minimal boundary

Handlers are fake/test-only, read-only, explicitly registered, and described by immutable descriptors with explicit cancellation, retry, timeout, parallel, and batching capability declarations. Dispatcher validates those declarations against policy before Runtime is invoked. The complete production SDK remains deferred to WP-008.6.

## WP-008.6 developer-facing SDK

`PSMOperationsPlatform.CollectorSdk` is the versioned public contract inside the existing frozen Collector Plugin Boundary; it is not a new architectural layer. Runtime contract 1.0 supports SDK 1.0. Plugins remain repository-built, explicitly registered, read-only and free of Infrastructure dependencies. Dynamic loading and third-party trust remain deferred. See [ADR-0006](../../adr/ADR-0006-Explicit-Versioned-Collector-Plugin-SDK.md).

Status: **MINIMAL HANDLER BOUNDARY IMPLEMENTED — FULL MODEL DEFERRED**

WP-008.4 implements only `ICollectorExecutionHandler` and the immutable
descriptor needed for test fakes. Registration is explicit by StrategyCode.
Descriptors declare positive plugin/schema versions, supported subjects,
read-only intent, cancellation support, estimated cost, and safe capability
code references.

The production Collector Plugin model remains deferred to WP-008.5. There is
no external package, marketplace, directory scan, assembly probing, reflection
discovery, hot reload, installation, production handler, implementation class
name, or executable delegate in a plan.
