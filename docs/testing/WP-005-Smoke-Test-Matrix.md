---
title: WP-005 Smoke Test Matrix
version: 1.0.0
status: Prepared
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# WP-005 Smoke Test Matrix

| ID | Safe precondition | Expected | Phase A disposition |
|---|---|---|---|
| ST-01 | HTTPS; `Auto`/`HttpsOnly` | HTTPS only, inventory, disposal attempt | Pending |
| ST-02 | HTTPS already unavailable; HTTP; `Auto` | Allowed failure then HTTP success | Not Executed unless natural |
| ST-03 | `HttpsOnly`; HTTPS naturally unavailable | No HTTP/inventory; failure persisted; old inventory retained | Pending safe condition |
| ST-04 | Eligible reachable target | Seven ordered modules and current state | Pending |
| ST-05 | Two normal due cycles | No duplicates; replace-all; isolation | Pending |
| ST-06 | Safe double/prepared scenario | Successful empty clears owned set | Not Executed; relational tests |
| ST-07 | Disconnected adapter already visible | Persisted | Not Executed unless natural |
| ST-08 | Virtual adapter already visible | Not filtered | Pending observation |
| ST-09 | Natural APIPA | Canonical address persists | Not Executed unless natural |
| ST-10 | Source query returns loopback | Persisted if returned | Otherwise Not Observable |
| ST-11 | Natural unlettered volume | Persists with null drive letter | Not Executed unless natural |
| ST-12 | Safe graceful service stop | Remaining modules stop; no partial commit; prior state; cleanup | Optional |
| ST-13 | Invalid lab target/offline VM | Classified; no inventory; backoff; no tight retry | Pending approval |

Do not force ST-02 or ST-06 through ST-11 by target mutation. Every test records
expected/actual result, safe logs, SQL evidence, and Pass/Fail/Not Executed.

Repository coverage references include `WindowsConnectivityProbeTests`,
`WindowsTargetProviderTests`, `ConnectivityResultPersistenceTests`,
backoff cases in `ConnectivityResultPersistenceTests`,
`WindowsInventoryOrchestratorTests`,
`WindowsCollectorCycleTests`, module tests, and Infrastructure inventory-store
tests.
