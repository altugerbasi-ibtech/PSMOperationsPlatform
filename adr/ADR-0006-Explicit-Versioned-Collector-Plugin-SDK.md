# ADR-0006 — Explicit Versioned Collector Plugin SDK

## Status

Accepted

## Date

2026-07-29

## Context

The frozen Collector Plugin Boundary needs a stable developer-facing contract without dynamic loading, infrastructure leakage, or Runtime responsibility changes.

## Decision

Provide dependency-minimal `PSMOperationsPlatform.CollectorSdk` contracts at SDK version 1.0. Repository-built plugins implement `ICollectorPlugin`, declare minimum/target SDK versions, immutable descriptors, explicit execution capabilities and supported artifact schemas, and register explicitly by stable PluginId and StrategyCode. A code-owned Runtime–SDK matrix and Dispatcher validation reject incompatible plugins before Runtime. Plugins are read-only, cancellation-aware, and receive immutable context/policy only. Reflection scanning, dynamic assembly loading, directory discovery and service-location are prohibited.

## Consequences

Plugin authors gain a supported contract and deterministic example. Contract changes require version and compatibility review. Third-party loading and trust remain unsupported.

## Security Impact

The SDK exposes no credentials, infrastructure services, unrestricted target access, mutable Runtime state or authentication controls. Existing Kerberos-only and target read-only boundaries remain unchanged.

## Migration/Compatibility Impact

There is no database migration. Application now references the SDK contract project; Runtime contract version 1.0 supports SDK version 1.0 only.

## Alternatives Considered

Application-owned public types, reflection discovery, configuration/database matrices, dynamic packages and service-provider access were rejected.

## Related Documents

- [Collector Plugin Architecture](../docs/architecture/Collector-Plugin.md)
- [SDK Guide](../docs/sdk/CollectorPluginSDK.md)
- [Compatibility Matrix](../docs/sdk/Compatibility-Matrix.md)
- [WP-008.6](../workpackages/WP-008.6.md)

## Supersession Rules

Only a later accepted ADR explicitly superseding ADR-0006 may change the public SDK ownership, versioning, registration or loading decisions.
