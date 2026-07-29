# PowerShell Collector Decision Rules

Status: **IMPLEMENTED — INTEGRATION PENDING**

Purpose: conservatively evaluate Windows core inventory and optional target
PowerShell 7 diagnostics. Inputs are Platform
`SupportsWindowsPowerShell51`, Collection
`CanRunWindowsPowerShell51Collection`, and Diagnostics `SupportsPowerShell7`.

Windows core inventory is Critical priority/order 100. Compatibility plus
unknown operational access is Indeterminate (`OperationalPermissionUnknown`);
missing 5.1 is Blocked (`PowerShell51Missing`). PowerShell 7 diagnostics is Low
priority/order 410 and platform-only; missing 7 is Blocked and unknown is
Indeterminate. PowerShell 7 is never required for standard collection.
NotApplicable/Disabled are not used by these version-1 rules.

Safe provenance and RuleVersions are retained. Both strategies are read-only.
No command is invoked, Collector-side tooling is not inferred, target
Windows PowerShell 5.1 compatibility remains required, and execution is
deferred.
