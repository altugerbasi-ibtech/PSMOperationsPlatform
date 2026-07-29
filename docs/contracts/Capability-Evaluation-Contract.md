# Capability Evaluation Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

`ICapabilityEngine.Evaluate` synchronously consumes an immutable
`PlatformCapabilityInput` and produces a deterministic
`CapabilityEvaluationResult`. Inputs identify one managed server, one source
`InventoryRunId`, one `InventoryVersion`, capture time, and narrow normalized
IIS, .NET, PowerShell, role, and feature facts.

Results use schema version 1 and contain ordinally ordered typed capability
entries. Every entry declares `ManagedTargetServer`, support status, readiness
status, rule version, stable reason code, safe reason, prerequisites, and safe
fact references. Missing facts are `Unknown`, not automatically unsupported.
Invalid source identity or source status invalidates the evaluation.

Individual rule semantics are explicit code. The engine has no database,
WinRM, logging, configuration, remediation, or strategy-selection dependency.
