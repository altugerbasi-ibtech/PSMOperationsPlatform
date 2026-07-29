# Execution Events

Dispatcher lifecycle events share independent `ExecutionEventSchemaVersion = 1` with Runtime lifecycle events. `LoggingExecutionEventSubscriber` converts safe events to structured logs; direct log messages are not the authoritative lifecycle representation.

Execution events are versioned safe in-process lifecycle notifications with a
monotonic sequence per run. They never contain credentials, commands, raw
payload, target output, secret configuration, or stack trace.

State persistence precedes publication. State remains authoritative on sink
failure. Exactly-once and durable delivery are not claimed; durable history is
deferred to WP-008.7 and telemetry export to WP-008.6.
