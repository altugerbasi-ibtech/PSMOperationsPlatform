# Network Adapter Inventory Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

The independent Adapter module uses an explicit, read-only
`Win32_NetworkAdapter` projection compatible with Windows PowerShell 5.1.
`AdapterKey` prefers Interface GUID, valid MAC address, PNP device ID, then a
canonical SHA-256 hardware hash and deterministic occurrence index.

All-zero and all-FF MAC addresses are explicit placeholders. Malformed fields
or duplicate strongest keys fail validation. A successfully queried and parsed
empty collection is valid-empty.

The module receives the complete-run context and shared WinRM session. It does
not open a session, consume IPv4 results, persist data, or log raw CIM output.
Integration remains pending WP-007.Z.
