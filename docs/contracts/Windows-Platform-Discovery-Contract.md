# Windows Platform Discovery Contract

Status: **APPROVED — INTEGRATION PENDING**

WP-007.6 discovers installed Windows platform capabilities as current state. It
uses the core inventory pipeline's shared Kerberos WinRM session,
`InventoryRunId`, capture timestamp, and all-core persistence transaction.

## Modules

| Module | Read-only source | Empty result |
|---|---|---|
| Windows Roles | `Get-WindowsFeature` | Valid |
| Windows Features | `Get-WindowsFeature` | Valid |
| IIS Platform | IIS installation registry metadata | Valid |
| .NET Platform | .NET installation registry metadata | Valid |
| PowerShell Platform | `Get-Command` for `powershell.exe` and `pwsh.exe` | Valid |

Each module owns its projection, normalization, validation, and deterministic
key. Modules do not share DTOs, consume another module's results, persist data,
or open WinRM sessions.

Only installed roles and features are retained. IIS discovery records platform
installation and version only; it does not enumerate websites, applications,
bindings, or application pools. .NET discovery classifies Framework, Runtime,
ASP.NET Runtime, Hosting Bundle, and SDK installations without inspecting
application binaries or configuration. PowerShell discovery records edition,
version, and executable path.

## Persistence

Successful results replace the five category collections in the complete-core
transaction. All rows receive the run's single `InventoryRunId`. Any module or
persistence failure preserves the previously committed complete current state,
and `InventoryVersion` advances only after commit.

Target commands are Windows PowerShell 5.1 compatible and read-only. They do
not install software, alter registry data, launch installers, change services,
or write files.

## Integration gate

WP-007.Z must validate the forward migration, schema, replacement behavior,
rollback, actual provider output, PowerShell 5.1 compatibility, and a second
idempotent inventory run. No runtime validation is claimed by WP-007.6.
