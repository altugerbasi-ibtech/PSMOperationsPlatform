# Execution History Contract

`ExecutionHistorySchemaVersion = 1` is positive and independent from State,
Event, Monitoring, Plan, Artifact, SDK, Plugin, Runtime, and migration versions.

An immutable `ExecutionHistoryProjection` contains one terminal run plus
ordinal steps, positive ordered attempts, explicit typed transitions, bounded
safe artifact metadata, and resolved policy provenance. Status is `Completed`
or explicitly `Partial`; failures use bounded history-specific categories.

The projector accepts only matching terminal state, prepared dispatch,
version-1 typed events, approved artifact contracts, and `TimeProvider`. It
stores no raw event JSON, payload, command, exception, credential, path, secret
URL, or arbitrary plugin metadata.

`IExecutionHistoryWriter` is cancellation-aware and returns Created, Duplicate,
or Failed without changing Runtime or Dispatcher results.
