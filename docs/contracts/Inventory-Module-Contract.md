# Inventory Module Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

Each Windows inventory module implements `IInventoryModule<T>` and receives an
`InventoryModuleContext` containing only target identity, target FQDN,
`InventoryRunId`, the shared WinRM command session, `TimeProvider`, and safe
logging. The context excludes persistence, credentials, configuration secrets,
service location, and other module results.

A module owns its explicit CIM projection, raw contract, parsing,
normalization, validation, deterministic key generation, and valid-empty
decision. It returns normalized data plus success, validation, failure
category, valid-empty, counts, duration, warnings, and safe diagnostic
metadata. Raw CIM output and persistence behavior are excluded.

The complete-core pipeline opens the one Kerberos session, invokes modules,
starts persistence only after every module succeeds, replaces all current
state in one transaction, and increments `InventoryVersion` only on commit.
Modules never open sessions or call the database.

Module DTOs, entities, and results remain independent. Broad hardware DTOs and
cross-module consumption are prohibited.
