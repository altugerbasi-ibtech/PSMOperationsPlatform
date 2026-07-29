# Failure Isolation

Dispatch preparation failures are isolated before Runtime invocation and create no attempt state. Runtime handler failures remain isolated per step. Event-subscriber failure does not replace authoritative dispatch results or Execution State.

Handler exception, malformed result, timeout, missing/mismatched descriptor,
policy failure, and dependency failure are safely classified. Attempt and step
state become terminal and independent eligible steps continue. Exception text
does not become a public reason or event message.

External cancellation remains distinct, stops new scheduling and retry, marks
state where possible, emits cancellation, and propagates. Event failure leaves
state authoritative. No failure mutates the plan or an upstream committed
inventory, capability, or decision record.
