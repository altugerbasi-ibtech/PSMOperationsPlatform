# .NET Collector Decision Rules

Status: **IMPLEMENTED — INTEGRATION PENDING**

Purpose: evaluate ASP.NET Framework logs and future .NET runtime diagnostics.
Inputs are Platform `SupportsIis`, `SupportsAspNetFramework`,
`SupportsDotNetRuntime`, optional `SupportsDotNet10`, and Collection
`CanCollectAspNetFrameworkLogs`.

Framework logs use Normal priority/order 320: IIS absence is NotApplicable,
missing Framework is Blocked (`AspNetFrameworkSupportMissing`), unknown access
is Indeterminate, and complete readiness is Eligible. Runtime diagnostics uses
Low priority/order 400 and represents platform eligibility only; runtime
presence is Eligible while missing/unknown/invalid evidence maps distinctly.
No rule is Disabled.

Both version-1 rules record prerequisite groups, safe provenance and evaluated
RuleVersions. They are read-only, do not run diagnostics or install runtimes,
and defer execution to a future approved package.
