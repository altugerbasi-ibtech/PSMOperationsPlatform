# Runtime Metrics

Artifact contracts do not move metrics into the immutable plan. Runtime continues to aggregate handler-reported non-negative counts into mutable Execution State; production artifact payload storage is absent.

QueueDuration runs from queueing to dependency eligibility. WaitDuration is
time eligible but waiting for runtime capacity. ExecutionDuration is summed
handler-attempt duration. TotalDuration runs from initial queueing to terminal
state.

AttemptCount includes every started attempt; RetryCount is AttemptCount minus
one. BytesCollected and ObjectsCollected are optional non-negative handler
reports, never inferred from payloads. Run totals use successful terminal step
results; failed-attempt values remain attempt evidence. Checked arithmetic
prevents silent overflow.
