# Architecture Decision Records

This directory is the authoritative index of accepted architecture decisions. ADRs record decisions; they do not authorize work outside an approved work package. Amendments require a new ADR that explicitly supersedes the earlier record.

| ADR | Title | Status | Purpose | Related authority |
|---|---|---|---|---|
| [0001](ADR-0001-Architecture-Freeze.md) | Architecture Freeze v1.0 | Accepted | Freeze product layers and responsibility boundaries | [Architecture Freeze](../docs/architecture/Architecture-Freeze-v1.0.md) |
| [0002](ADR-0002-Development-Process-Freeze.md) | Development Process Freeze v1.0 | Accepted | Freeze specification-driven delivery and review | [Process Freeze](../docs/engineering/Development-Process-Freeze-v1.0.md) |
| [0003](ADR-0003-Kerberos-Only-Authentication.md) | Kerberos-Only Authentication | Accepted | Preserve the Windows Collector authentication boundary | [Security](../docs/architecture/08-Security-Architecture.md) |
| [0004](ADR-0004-Repository-First-Development.md) | Repository-First Development | Accepted | Keep implementation and validation repository-first | [Engineering Standards](../docs/engineering/PSM-Engineering-Standards.md) |
| [0005](ADR-0005-Current-State-Inventory.md) | Current-State Inventory | Accepted | Define authoritative current inventory semantics | [Inventory Pipeline](../docs/architecture/Inventory-Pipeline.md) |
| [0006](ADR-0006-Explicit-Versioned-Collector-Plugin-SDK.md) | Explicit Versioned Collector Plugin SDK | Accepted | Define the public, read-only, explicitly registered SDK contract | [Collector Plugin](../docs/architecture/Collector-Plugin.md) |
| [0007](ADR-0007-Event-Based-Execution-Monitoring.md) | Event-Based Execution Monitoring | Accepted | Define independent, bounded, non-durable execution observation | [Execution Monitoring](../docs/architecture/Execution-Monitoring.md) |
| [0008](ADR-0008-Durable-Execution-History-Projection.md) | Durable Execution History Projection | Accepted | Define terminal-write durable history, idempotency, bounded queries and retention | [Execution History](../docs/architecture/Execution-History.md) |

[ADR template](ADR-Template.md)
