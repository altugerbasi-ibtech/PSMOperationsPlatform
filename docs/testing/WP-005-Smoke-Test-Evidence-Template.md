---
title: WP-005 Smoke Test Evidence Template
version: 1.0.0
status: Template
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# WP-005 Smoke Test Evidence Template

## Execution

| Field | Value |
|---|---|
| Phase | Preparation Only / Completed / Stopped |
| Approval/time | |
| Test time/timezone | |
| Collector host, commit/version/hash | |
| Test identity (name only) | |
| Target ID/FQDN and non-production confirmation | |
| Transport mode/ports | |
| Test SQL/database and non-production confirmation | |
| Migration status | |

## Test case

| Field | Value |
|---|---|
| Test case ID | |
| Preconditions | |
| Expected / actual | |
| Attempted/successful transport | |
| Correlation IDs | |
| Safe log reference | |
| Baseline/SQL references | |
| Target unchanged check | |
| Result | Pass / Fail / Not Executed |
| Notes | |

## Baseline comparison

| Boundary | Count/stable keys | Fields | CapturedAt | Result |
|---|---:|---|---|---|
| Computer | | | | |
| Operating System | | | | |
| Memory | | | | |
| Processor | | | | |
| Disk | | | | |
| Volume | | | | |
| Network Adapter | | | | |
| IPv4 | | | | |

Record HTTPS/fallback, session open/disposal, cancellation cleanup, no remote
mutation, no production access, and redaction confirmations. Findings use
Critical/High/Medium/Low with evidence and minimum corrective action.

Remove credentials, tokens, connection strings, unnecessary topology detail,
and unsafe raw exceptions.
