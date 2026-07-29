# Execution History

Status: **IMPLEMENTED — INTEGRATION PENDING**

Execution History is the durable historical projection inside the frozen flow:

```text
typed terminal Execution State and Execution Events
    -> explicit terminal history projection
    -> Operations Database history schema
    -> bounded read boundary
```

Execution State remains authoritative while work is active. Monitoring remains
current, non-durable observation. History may lag and never controls Runtime or
Dispatcher. Audit is a separate future capability.

Version 1 uses a terminal-write-preferred model. Immutable safe facts are
constructed after terminal state and the normalized aggregate is persisted in
one short transaction. Missing non-authoritative facts produce explicit
`Partial` history; known terminal truth is preserved and no fact is fabricated.

Logical uniqueness makes redelivery idempotent. Delivery is not exactly once:
WP-008.8 adds no outbox, event bus, broker, replay, or event sourcing.

See [ADR-0008](../../adr/ADR-0008-Durable-Execution-History-Projection.md),
[history contract](../contracts/Execution-History-Contract.md), and
[History versus Audit](Execution-History-vs-Audit.md).
