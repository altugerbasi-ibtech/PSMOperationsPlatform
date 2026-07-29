# Volume Inventory Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

The module uses read-only `Win32_Volume` with an explicit PowerShell 5.1
projection. Drive-letter and mount-point-only volumes are represented without
combining providers. `VolumeKey` prefers normalized device/GUID, serial plus
file system, drive letter, then stable hash and deterministic occurrence.

Drive letters use uppercase `C:` form. Capacity and block size are positive
when supplied; free space is non-negative and cannot exceed capacity.
Duplicate keys and empty results are invalid.

Volume does not consume Disk results and no disk-to-volume correlation is
implemented. Successful non-empty collection replaces current volumes
atomically with the complete-run `InventoryRunId`. Integration is pending
WP-007.Z.
