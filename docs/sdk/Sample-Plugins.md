# Collector Plugin SDK Samples

Status: **IMPLEMENTED — NON-PRODUCTION**

The sample project contains Hello, NoData, Failure, LongRunning and
Cancellation plugins. Each has unique identifiers, targets SDK 1.0, is
read-only, supports cancellation and returns deterministic fake behavior.
LongRunning and Cancellation demonstrate cooperative cancellation. Failure
returns a safe typed failure. NoData returns valid empty artifacts.

Samples perform no network, filesystem, registry, process, WinRM, PowerShell or
SQL operation and are absent from normal production registration.
