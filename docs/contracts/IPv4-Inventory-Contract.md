# IPv4 Inventory Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

The independent IPv4 module uses the read-only
`Win32_NetworkAdapterConfiguration` projection. `SettingID` is normalized to
the same GUID-based AdapterKey contract; a valid MAC may be used when GUID is
unavailable. The module does not consume Adapter module results.

`Ipv4Key` is AdapterKey plus canonical IPv4 address. The same address on
different adapters is distinct. IP/subnet arrays must align; subnet masks are
contiguous and converted to prefix length `0..32`. Default gateways must be
canonical IPv4. IPv6 values are outside scope and are not persisted.

A successfully queried, parsed empty collection is valid-empty. Duplicate
IPv4Key, malformed IPv4, gateway, subnet, or correlation identity fails
validation. Persistence retains the composite same-ManagedServer adapter
foreign key. Integration remains pending WP-007.Z.
