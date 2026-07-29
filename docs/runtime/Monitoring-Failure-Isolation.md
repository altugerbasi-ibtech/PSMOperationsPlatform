# Monitoring Failure Isolation

Snapshot, health-score, readiness-badge, documentation validation and local
performance-measurement failures remain observational. Public contracts expose
only stable bounded codes and never raw exception text.

Logging and Monitoring are independent subscribers. The composite event sink
continues after one observational subscriber faults and propagates only caller
cancellation. Monitoring catches projection/instrument failures, records safe
bounded diagnostics and never changes Dispatcher/Runtime/plugin results or
Execution State.

State remains authoritative. Event delivery is not exactly once and is not
durable.
