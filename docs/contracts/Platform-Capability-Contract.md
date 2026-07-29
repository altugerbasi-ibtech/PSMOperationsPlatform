# Platform Capability Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

Platform modules discover facts independently. They do not select collectors,
consume another platform module's DTO, invoke the Capability Engine, or modify
the target. Only the Capability Engine combines categories.

The initial managed-target catalog covers IIS presence and conservative log
readiness, Failed Request Tracing facts, .NET Framework, .NET/ASP.NET Core
runtimes, Hosting Bundle, .NET 10 runtime support, SDK presence, Windows
PowerShell 5.1, and PowerShell 7. Collector-side PowerShell tooling is
`NotApplicable` to this subject.

Platform support and operational readiness are separate. Permissions, log
paths, connectivity, and application log configuration are not currently
inventoried, so affected readiness results remain `Unknown`.
