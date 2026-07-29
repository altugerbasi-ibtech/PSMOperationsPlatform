# Physical Disk Inventory Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

The module uses read-only `Win32_DiskDrive` with an explicit PowerShell 5.1
projection. One row represents one provider disk. `DiskKey` prefers valid
serial, PNP device ID, device ID, index plus stable hash, then stable hash and
deterministic occurrence.

Blank and explicit placeholder serials are not identities. Duplicate strongest
keys are ambiguous. Size and bytes per sector must be positive when supplied;
partitions and index must be non-negative. Empty is valid only after successful
command, parse, and validation. Model text never determines media type.

Successful collection atomically replaces current disks with the shared
`InventoryRunId`; valid-empty removes prior rows. Integration is pending
WP-007.Z.
