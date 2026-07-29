# ASP.NET Core Collector Decision Rules

Status: **IMPLEMENTED — INTEGRATION PENDING**

Purpose: evaluate version-1 `AspNetCoreIisLogStrategy` at Normal priority/order
330. Inputs are Platform `SupportsIis`, `SupportsAspNetCore`,
`HasAspNetCoreHostingBundle`, and Collection `CanCollectAspNetCoreIisLogs`.

All supported and ready is Eligible. Missing runtime or Hosting Bundle is
Blocked (`AspNetCoreRuntimeMissing` or `HostingBundleMissing`). IIS absence is
NotApplicable. Any unknown required capability is Indeterminate; SDK presence
is not an input and cannot establish eligibility. Invalid evidence is Invalid;
the strategy is not Disabled.

Provenance retains safe source identities and every evaluated RuleVersion.
StrategyVersion changes for semantic prerequisite/readiness/priority changes.
The strategy is read-only, performs no IIS/runtime change, respects the
Collector security boundary, and defers execution.
