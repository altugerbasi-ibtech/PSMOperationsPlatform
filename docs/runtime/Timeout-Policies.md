# Timeout Policies

Dispatcher resolves `ShortReadOnly` v1 (1 minute), `StandardReadOnly` v1 (5 minutes), and `LongReadOnly` v1 (15 minutes). All durations are positive and fixed. A plugin must declare timeout and cancellation support. Runtime alone creates linked cancellation and enforces timeout; Dispatcher rejects incompatibility. Timeout contributes to attempt/execution metrics and is classified separately. Target configuration and new policy codes are deferred.

Cancellation distinguishes external cancellation from timeout. Tests cover each code/version, positive bounds, compatibility rejection, completion-before-timeout, timeout classification and retry interaction.
