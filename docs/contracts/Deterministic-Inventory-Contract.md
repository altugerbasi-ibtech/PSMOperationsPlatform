# Deterministic Inventory Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

Equal logical state produces equal normalized values, keys, ordering, duplicate
decisions, and validation regardless of CIM enumeration order.

Normalize stable identity fields, sort with ordinal comparison using documented
fields, then assign occurrence indexes. Fallback hashing uses SHA-256, UTF-8,
explicit field order, the unit-separator delimiter, and `<NULL>` for missing
values. `GetHashCode`, culture-sensitive comparison, and enumeration-order
identity are prohibited.

Repeated runs replace current state atomically without duplicates or unbounded
growth. Only run metadata—`InventoryRunId`, `CapturedAt`, `InventoryVersion`,
duration, and logging timestamps—may differ.
