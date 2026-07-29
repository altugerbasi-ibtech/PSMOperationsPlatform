# Events SDK Boundary

Execution lifecycle notifications use strongly typed in-process events with `ExecutionEventSchemaVersion = 1`, ordered sequence values within each publisher, safe provenance, and no payloads or secrets. Logging subscribes to events; events are not authoritative state and exactly-once delivery is not promised.

Exporters and monitoring integration are deferred.

Dispatcher rejection events carry stable safe SDK compatibility and plugin provenance through normalized reason codes; artifact payloads and arbitrary plugin text are never event content.
