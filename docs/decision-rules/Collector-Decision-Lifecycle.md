# Collector Decision Lifecycle

Status: **IMPLEMENTED — INTEGRATION PENDING**

Purpose: define version-1 plan lifecycle. After inventory commits, capability
evaluation/persistence completes before decision evaluation begins. The engine
consumes the committed ManagedTargetServer snapshot only.

Rules are explicit code with positive StrategyVersion and capability
RuleVersions. Status is derived from separate eligibility and readiness:
supported/ready is Eligible, confirmed absence/not-ready is Blocked,
insufficient evidence is Indeterminate, platform irrelevance is NotApplicable,
policy suppression is Disabled, and inconsistent evidence is Invalid.

Ordering is Priority, ExecutionOrder, ordinal StrategyCode. Results retain
prerequisite groups, stable reasons/explanations, snapshot/run/version, and safe
provenance. One current plan is atomically replaced. Catastrophic failure
preserves the prior plan; individual rule failure produces Invalid and other
rules continue.

All initial strategies are read-only with no manual approval. No Collector,
WinRM, PowerShell, process, target file, registry, IIS, feature, or runtime
action occurs. Execution remains deferred to a separately approved package.

WP-008.3 hands the committed plan to the Execution Plan Engine. Eligible and
Ready decisions may become immutable steps; every other decision becomes an
explainable exclusion. This handoff does not change the source decision.

WP-008.4 may explicitly load the persisted immutable Execution Plan into the
Collector Runtime. Runtime owns attempts, timeout, retry, cancellation, and
separate mutable Execution State; it cannot change this decision lifecycle.
Only fake handlers are used in tests. Production plugins, monitoring, and
history remain deferred.
