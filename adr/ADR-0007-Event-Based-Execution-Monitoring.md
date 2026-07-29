# ADR-0007 — Event-Based Execution Monitoring

## Status

Accepted

## Date

2026-07-29

## Context

The frozen Monitoring responsibility needs safe current operational signals
without coupling Dispatcher or Runtime to a backend or introducing history.

## Decision

Monitor execution through independent subscribers to the existing typed event
boundary. Use standard .NET `ActivitySource` and `Meter`, an explicit
code-owned metric catalog, bounded product-owned dimensions and a bounded
current-health projection. Execution State remains authoritative. Monitoring
is non-durable, best effort and failure-isolated. Logging and Monitoring
subscribers coexist independently. Exporters and durable history are deferred.

## Consequences

Repository code can measure and trace current execution without a vendor
dependency. Process loss and duplicate delivery can affect observations, and
exactly-once delivery is not promised.

## Security Impact

Metrics and Activities exclude target identifiers, credentials, raw output and
arbitrary plugin values. Monitoring performs no target operation and changes
no authentication boundary.

## Migration/Compatibility Impact

No persistence migration or exporter configuration is required. Existing
event schema version 1 remains the input contract.

## Alternatives Considered

Direct Runtime instrumentation dependencies, a monitoring database, event bus,
outbox, vendor exporter and unbounded in-memory history were rejected.

## Related Documents

- [Execution Monitoring](../docs/architecture/Execution-Monitoring.md)
- [Monitoring Contract](../docs/contracts/Execution-Monitoring-Contract.md)
- [WP-008.7](../workpackages/WP-008.7.md)

## Supersession Rules

Only a later accepted ADR explicitly superseding ADR-0007 may change monitoring
authority, delivery, cardinality or durability.
