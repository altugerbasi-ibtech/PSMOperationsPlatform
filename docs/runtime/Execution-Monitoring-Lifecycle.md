# Execution Monitoring Lifecycle

Concurrent consumers obtain a copied immutable snapshot through the narrow
read provider. Snapshot and assessment calculation invoke no external callback
and remain bounded.

Dispatcher and Runtime publish typed events. The composite sink invokes
logging and monitoring subscribers independently. Monitoring validates schema,
rejects duplicate/out-of-order input conservatively, records instruments,
projects current health and returns without affecting execution.

Delivery is in-process, non-durable and best effort. Process termination may
lose events and duplicate delivery can affect cumulative instruments. No
exactly-once guarantee, bus, broker, outbox or durable queue exists.
