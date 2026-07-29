# Capability Engine

Status: **IMPLEMENTED — INTEGRATION PENDING**

The Capability Engine transforms a coherent committed WP-007.6 platform fact
set into one deterministic current capability snapshot. It is synchronous,
in-memory, infrastructure-free rule evaluation.

Input identifies `ManagedTargetServer`, source run/version, capture time, and
narrow immutable platform facts. Output contains schema version 1, evaluation
status, explicit support/readiness states, stable rule/reason versions,
prerequisites, and safe provenance. `Unknown`, `NotSupported`,
`NotApplicable`, and `Invalid` remain distinct.

Every entry has a strongly typed category and independent positive integer
`RuleVersion`. Unknown means insufficient evidence and is never false or
unsupported. Safe provenance retains normalized module, fact category, stable
key, source run/version, and rule version; raw provider data is excluded.
Stable reason codes, prerequisite groups, and deterministic explanations make
each result explainable.

Rules are explicitly registered in code and entries are sorted ordinally.
Discovery modules remain independent. The engine neither discovers facts nor
selects/executes a collection strategy, and performs no remediation.

After successful inventory commit, capability evaluation and snapshot
replacement run in their own atomic transaction. Failure preserves the
committed inventory and prior capability snapshot. Integration validation is
deferred to WP-007.Z.
