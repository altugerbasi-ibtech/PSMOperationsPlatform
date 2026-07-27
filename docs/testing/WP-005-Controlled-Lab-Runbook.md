---
title: WP-005 Controlled Lab Runbook
version: 1.0.0
status: Prepared
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# WP-005 Controlled Lab Runbook

## 1. Hold point

Record every gate input from
[WP-005.S1](../tasks/WP-005.S1-Controlled-Lab-Smoke-Test.md), approver, and
approval time. Until then do not connect to WinRM or SQL. Migration and cleanup
are separate hold points.

## 2. Freeze scope

Record collector commit/hash, service identity, target ID/FQDN, transport
mode/ports, test database, migration, clocks/timezones, and non-production
confirmations. Ensure only approved lab targets are enabled and due in the
dedicated database.

Run the official readiness entry point documented in
[Collector Environment Validation Usage](Collector-Environment-Validation-Usage.md)
with `-Mode SmokeTest`. Continue only when its fixed JSON/Markdown reports and
exit code show `READY`. `WARNING` requires recorded review; `NOT_READY` stops
the smoke test. Do not use a second readiness orchestrator.

## 3. Pre-run state and baseline

Run the read-only SQL verification and securely export all target-scoped sets.
Then use the same narrow product projections:

```powershell
$s = New-PSSession -ComputerName '<LAB_TARGET_FQDN>' -UseSSL `
  -Port <HTTPS_PORT> -Authentication Negotiate
Invoke-Command -Session $s -ScriptBlock {
  Get-CimInstance Win32_ComputerSystem -Property Name,Domain,Manufacturer,Model
  Get-CimInstance Win32_BIOS -Property SerialNumber
  Get-CimInstance Win32_OperatingSystem `
    -Property Caption,Version,BuildNumber,OSArchitecture,InstallDate,LastBootUpTime
  Get-CimInstance Win32_ComputerSystem -Property TotalPhysicalMemory
  Get-CimInstance Win32_Processor `
    -Property DeviceID,Name,Manufacturer,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed
  Get-CimInstance -Namespace root/Microsoft/Windows/Storage -ClassName MSFT_Disk `
    -Property UniqueId,Number,FriendlyName,SerialNumber,Size,BusType,PartitionStyle
  Get-CimInstance -Namespace root/Microsoft/Windows/Storage -ClassName MSFT_Volume `
    -Property UniqueId,DriveLetter,FileSystem,FileSystemLabel,Size,SizeRemaining
  Get-CimInstance -Namespace root/StandardCimv2 -ClassName MSFT_NetAdapter `
    -Property InterfaceGuid,InterfaceIndex,Name,InterfaceDescription,PermanentAddress,Speed,InterfaceOperationalStatus
  Get-CimInstance -Namespace root/StandardCimv2 -ClassName MSFT_NetIPAddress `
    -Filter 'AddressFamily = 2' -Property InterfaceIndex,IPAddress,PrefixLength
}
Remove-PSSession $s
```

For approved HTTP omit `-UseSSL` and use its port. Never use `Select *`.

## 4. Execute collector

There is no one-shot mode. Use only the deployed Windows Service and supported
Service Control Manager lifecycle, with explicit approval. Observe one polling
cycle, then gracefully stop when required. Never kill the process and never
start/stop/reconfigure a target service.

Expected order: Computer, OperatingSystem, Memory, Processor, Disk, Volume,
NetworkAdapter. A module failure is logged and later modules continue; host
cancellation stops remaining modules. The cycle attempts one disposal of the
successful session in `finally`.

## 5. Verify and repeat

Correlate safe logs by target ID, polling cycle ID, and inventory correlation
ID. In `Auto`, HTTPS is first; HTTP fallback is limited to `TlsFailure`,
`ConnectionRefused`, `Timeout`, `WinRmUnavailable`, and `ProtocolFailure`.
Authentication, authorization, DNS, unexpected, and cancellation do not fall
back.

Run SQL checks and compare baseline values/stable keys. `CapturedAt` is Türkiye
local time in `datetime2(3)`. Allow one further normal due cycle for ST-05.
Singular counts remain at most one and stable keys unique. Replace-all row GUIDs
may change; compare stable keys and values.

ST-12 may use graceful service stop only when approved and safely observable.
Confirm later modules cease, the in-flight module has no partial commit, prior
state remains, and cleanup was attempted. Otherwise mark Not Executed.

On a stop condition, gracefully stop, preserve evidence, and make no further
mutation.
