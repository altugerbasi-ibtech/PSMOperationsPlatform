# Retry Policies

`NoRetry` v1 permits one attempt. `StandardReadOnlyRetry` v1 permits two attempts for the implemented timeout and handler-execution categories with a deterministic one-second delay. Dispatcher resolves references and requires retry capability when `MaxAttempts > 1`; Runtime owns attempts, delays, cancellation and retry metrics. Plugins cannot request or control retries. Configurable retry policy is deferred.

Cancellation stops delays and further attempts. Failure after exhaustion remains the terminal step result. Tests cover policy resolution/version, capability rejection, success, retry, exhaustion, timeout eligibility, metrics and cancellation.
