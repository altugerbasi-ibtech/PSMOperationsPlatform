# Artifact Metrics

All entries inherit [catalog policy](README.md). Artifact identity, names,
paths and payloads are never dimensions.

| Metric | Type | Unit | Description | Source | Recording condition | Cardinality |
|---|---|---|---|---|---|---|
| `psm.execution.artifacts.files` | Counter | count | File artifact count | Typed safe summary | Only when typed evidence exists | Bounded |
| `psm.execution.artifacts.objects` | Counter | count | Object artifact count | Typed safe summary | Only when typed evidence exists | Bounded |
| `psm.execution.artifacts.metrics` | Counter | count | Metric artifact count | Typed safe summary | Only when typed evidence exists | Bounded |
| `psm.execution.artifact.bytes` | Histogram | By | Artifact byte size | Typed safe summary | Non-negative size exists | Bounded |
| `psm.execution.warnings` | Counter | count | Product warning count | Typed safe summary | Bounded warning code exists | Bounded |

The existing event contract does not currently carry all artifact summaries;
unproven measurements remain unrecorded.
