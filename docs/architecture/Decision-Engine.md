# Decision Engine

Status: **IMPLEMENTED — INTEGRATION PENDING**

WP-008.2 synchronously consumes one coherent committed `ManagedTargetServer`
capability snapshot and produces one deterministic current Collector Decision
Plan. It has no inventory DTO, database, WinRM, PowerShell, collector, process,
or target-file dependency. Infrastructure loads and persists boundary models.

Statuses are Eligible, Blocked, Indeterminate, NotApplicable, Disabled, and
Invalid. Separate EligibilityStatus and ExecutionReadinessStatus prevent
platform support from overstating permissions, paths, or connectivity. Unknown
required evidence produces Indeterminate; confirmed absence produces Blocked,
except IIS absence makes IIS strategies NotApplicable.

The version-1 catalog contains Windows core inventory, IIS platform inventory,
IIS logs, Failed Request Tracing logs, ASP.NET Framework logs, ASP.NET Core IIS
logs, .NET runtime diagnostics, and target PowerShell 7 diagnostics. Windows
Feature diagnostics is deferred because WP-008.1 has no suitable aggregate
capability. All strategies are read-only and require no manual approval.

Each strategy owns a positive StrategyVersion. DecisionSchemaVersion 1 is
independent of capability schema, capability rule, strategy, and inventory
versions. Ordering is numeric Priority, numeric ExecutionOrder, then ordinal
StrategyCode; registration and dictionary order do not affect output.

Plans retain source snapshot/run/version, prerequisite groups, stable reason
codes and explanations, capability categories/rule versions, and safe
provenance. One current plan per server is atomically replaced with its child
rows. Catastrophic input failure creates no replacement; individual rule
failure becomes Invalid while independent rules continue.

Decision evaluation follows successful capability persistence in a separate
transaction. Failure cannot roll back inventory or capability state. No
history, execution/retry engine, management strategy, plugin, policy language,
or collector execution is included. A future approved execution package may
consume Eligible decisions but must independently enforce execution safety.

WP-008.3 now consumes the committed Decision Plan as an immutable source. The
Decision Engine continues to own eligibility and explanation only; it does not
assign timeouts, retry references, parallel groups, throttling, or step order.
