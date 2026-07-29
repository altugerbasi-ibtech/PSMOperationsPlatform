# Execution History Idempotency

Application duplicate checks are an optimization; SQL uniqueness is the
concurrency backstop:

- run: execution run ID;
- step: run and execution step;
- attempt: run, execution step, attempt number;
- transition: run and transition sequence, with entity identity;
- artifact: run, step, artifact ID;
- policy: run and step.

Repeated terminal delivery is a deterministic Duplicate no-op. A concurrent
unique conflict is rechecked safely. Exactly-once delivery is not claimed.
