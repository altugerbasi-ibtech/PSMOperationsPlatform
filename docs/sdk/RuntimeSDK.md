# Runtime SDK Boundary

Runtime accepts only `PreparedExecutionDispatch`. It uses the supplied handler, policy, and context while retaining timeout, retry, throttling, dependencies, cancellation, isolation, events, and mutable state ownership. It has no handler-registry or policy-catalog dependency.

Runtime contract version 1.0 supports Collector Plugin SDK 1.0 only. Dispatcher validates this before Runtime. Runtime executes the supplied `ICollectorPlugin` and never resolves registration, SDK versions or policies.
