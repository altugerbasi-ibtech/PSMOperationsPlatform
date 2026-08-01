---
title: Production Readiness Gate Matrix
version: 1.0.0
status: Approved
owner: Operations
last_updated: 2026-08-01
product: PSM Operations Platform
---
# Production Readiness Gate Matrix

This is the authoritative gate inventory. Actual results are populated only from validated evidence.

| Gate ID | Area | Requirement | Evidence class | Mandatory | Expected | Current repository result | Owner | Risk / remediation / retest |
|---|---|---|---|---|---|---|---|---|
| PRD.01.BASELINE | Source | Clean, aligned approved baseline and tag | Repository/release | Yes | PASS | Requires execution evidence | Release Management | Correct divergence; rerun |
| PRD.02.GOVERNANCE | Governance | Freeze, ADR, and lifecycle complete | Repository/human | Yes | PASS | Requires review | Architecture | Resolve governance gaps; rerun |
| PRD.03.QUALITY | Quality | Build, tests, scans, links pass | Repository | Yes | PASS | Requires current run | Engineering | Fix failures; rerun |
| PRD.04.ARTIFACTS | Release | Bundle metadata and checksums align | Release artifact | Yes | PASS | Requires release evidence | Release Management | Rebuild approved bundle; rerun |
| PRD.05.CONFIG | Configuration | Approved secret-free live configuration | Live/human | Yes | PASS | Missing live evidence | Operations | Supply approved configuration; rerun |
| PRD.06.DATABASE | Database | Deployment, schema, permissions pass | Live | Yes | PASS | Missing live evidence | Database | Validate intended database; rerun |
| PRD.07.COLLECTOR | Collector host | Host prerequisites pass | Live | Yes | PASS | Missing live evidence | Infrastructure | Execute approved host validation; rerun |
| PRD.08.IIS | IIS targets | Every configured IIS target passes | Live | Yes | PASS | Missing live evidence | Infrastructure | Execute approved IIS validation; rerun |
| PRD.09.SQL | SQL targets | Auth, encryption, metadata pass | Live | Yes | PASS | Missing live evidence | Database | Execute approved SQL validation; rerun |
| PRD.10.E2E | End-to-end | Complete execution path passes | Live | Yes | PASS | Missing live evidence | Operations | Execute approved E2E validation; rerun |
| PRD.11.PORTAL.AUTH | Portal | Windows Authentication composed | Repository/live | Yes | PASS | Repository composition present; live evidence missing | Application | Validate controlled IIS and HTTP Kerberos; retest |
| PRD.12.MONITORING | Monitoring | Backend posture and risk accepted | Repository/human/live | Conditional | PASS/WARNING | Backend absent; acceptance not inferred | Operations | Record approved posture/risk; retest |
| PRD.13.HISTORY | History | Schema and behavior validated | Live | Yes | PASS | Missing live evidence | Database | Execute approved history validation; rerun |
| PRD.14.RETENTION | Retention | Policy and cleanup plan validated | Live/human | Yes | PASS | No dry-run API or scheduler | Operations | Approve safe operating plan; rerun |
| PRD.15.SCALE | Performance | Controlled live scale evidence | Live | Yes | PASS | NOT PROVEN | Engineering | Controlled performance validation; rerun |
| PRD.16.SECURITY | Security | Authentication and secret policies pass | Repository/live | Yes | PASS | Requires current evidence | Security | Remediate blocking finding; rerun |
| PRD.17.OPERATIONS | Operations | Ownership/change/rollback approvals | Human | Yes | PASS | Must not be inferred | Operations | Obtain named approvals externally; rerun |
| PRD.18.RAT | RAT | Intended release/environment RAT passes | Live/release | Yes | PASS | Missing live evidence | Release Management | Execute approved RAT; rerun |
| PRD.19.RISKS | Risks | Every exception explicitly accepted | Human | Yes | PASS/WARNING | Must not be auto-accepted | Risk owner | Record acceptance; rerun |
| PRD.20.DECISION | Decision | Deterministic precedence applied | Aggregate | Yes | PASS | NOT_READY_FOR_PRODUCTION: Monitoring risk acceptance remains absent | Release Management | Resolve blockers and rerun all affected gates |
