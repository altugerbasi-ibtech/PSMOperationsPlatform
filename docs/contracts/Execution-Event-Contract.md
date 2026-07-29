# Execution Event Contract

WP-008.8 maps supported version-1 lifecycle events explicitly into bounded
transition facts. It does not store arbitrary event JSON, claim replay, or make
events an event-sourcing authority.

WP-008.7 consumes schema-version-1 events through explicit mappings. Logging
and Monitoring are independent best-effort in-process subscribers. Delivery is
non-durable, duplicates/process loss are possible, and exactly-once delivery is
not claimed. Execution State remains authoritative.

`ExecutionEventSchemaVersion = 1` is independent from plan and state schema versions. Dispatcher adds requested, handler-resolved, policy-resolved, prepared, rejected, and submitted events. Runtime lifecycle remains event-based, and safe structured logging is an event subscriber. Delivery is best effort; state/result is authoritative and exactly-once is not claimed.

Status: **IMPLEMENTED — IN-PROCESS ONLY**

Version-1 strongly typed events cover run creation/start/completion/failure/
cancellation and step queue/wait/start/attempt/retry/completion/skip/failure/
timeout/cancellation. Each event has a monotonic run sequence and safe plan,
run, step, strategy, attempt, time, status, reason, failure, and upstream
provenance.

State transitions persist first. Publication failure is classified and does
not replace authoritative state. Delivery is best-effort in-process; exactly
once, durable history, outbox, event bus, and OpenTelemetry export are not
claimed.
