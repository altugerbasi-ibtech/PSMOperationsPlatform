# IIS Collector Decision Rules

Status: **IMPLEMENTED — INTEGRATION PENDING**

Purpose: evaluate read-only IIS platform, IIS log, and Failed Request Tracing
strategies for ManagedTargetServer. Inputs are Platform `SupportsIis` and
Collection `CanCollectIisPlatformInventory`, `CanCollectIisLogs`, and
`CanCollectFailedRequestTracingLogs`.

Support plus ready collection capability is Eligible. Known IIS absence is
NotApplicable. Known collection non-support/readiness is Blocked. Unknown
support/readiness is Indeterminate (`IisAccessUnknown`,
`FailedRequestTracingUnknown`, or `RequiredCapabilityUnknown`). No IIS strategy
is Disabled in version 1. Priorities are High (200) for platform inventory and
Normal (300) for logs; explicit execution orders distinguish them.

Each version-1 rule retains prerequisite groups, safe explanation, snapshot,
inventory run/version, category, and evaluated RuleVersion. Every strategy is
read-only, crosses no Windows/SQL identity boundary, and defers execution.
