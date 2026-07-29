# Architecture Freeze v1.0

WP-008.8 activates the already frozen Execution History boundary as a durable
terminal projection. It does not add a layer: Execution State remains current
authority, Monitoring remains current observation, and Audit remains separate.
See [ADR-0008](../../adr/ADR-0008-Durable-Execution-History-Projection.md).

WP-008.7.Q adds only quality contracts inside the frozen Monitoring boundary:
read-only snapshot, advisory score/readiness, documentation and budgets.
Execution State authority and non-durability are unchanged.

WP-008.7 activates the already frozen Monitoring responsibility as an
independent typed-event subscriber using standard .NET diagnostics. This is not
a new layer. Monitoring observes only; Execution State remains authoritative,
and durable history remains WP-008.8.

Decision record: [ADR-0001](../../adr/ADR-0001-Architecture-Freeze.md). WP-008.5 verifies the active frozen separation by assigning handler and policy resolution to Execution Dispatcher while Collector Runtime retains execution mechanics and mutable state.

WP-008.6 adds a versioned developer-facing contract inside the existing Collector Plugin Boundary as recorded by [ADR-0006](../../adr/ADR-0006-Explicit-Versioned-Collector-Plugin-SDK.md); no layer is added.

Status: **Approved and Active**

```text
Inventory Pipeline
  -> Capability Engine
  -> Collector Decision Engine
  -> Execution Plan Engine
  -> Execution Dispatcher
  -> Collector Runtime
  -> Collector Plugin Boundary
  -> Execution State
  -> Monitoring
  -> History
```

Before WP-007.Z, a new architectural layer requires an explicit architecture
exception and replacement of an established engine requires an approved ADR.
New work extends this baseline. It must not introduce a generic framework, new
infrastructure technology, target-side write capability, platform-module
cross-dependency, reflection plugin scan, mutable plan state, or retry in a
Decision/Plan engine.

Kerberos-only authentication, separate Collector identities,
IncludePortInSPN, forward-only schema evolution, and the WP-007.Z gate remain
binding. Defects may be corrected without violating these boundaries.
WP-008.5 through WP-008.7 are not implemented by this proposal.

Development specification, launch, amendment, validation, and review are
governed separately by
[`Development-Process-Freeze-v1.0.md`](../engineering/Development-Process-Freeze-v1.0.md).
That process reference does not alter the product architecture frozen here.
