---
title: Collector Environment Validation Troubleshooting
version: 1.0.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# Collector Environment Validation Troubleshooting

| Result | Meaning | Manual next step |
|---|---|---|
| Output directory failure | Directory is absent | Select/create it through a separately approved local process, then rerun |
| Runtime missing | Framework-dependent deployment lacks .NET 10 | Use approved runtime installation process |
| Version warning | File version metadata unavailable | Verify artifact provenance/hash externally |
| Configuration FAIL | Missing/malformed/credential-bearing database configuration | Correct approved deployment configuration |
| Service stopped WARNING | Registration exists but service is stopped | Start only under the separately approved S1 runbook |
| gMSA WARNING | AD module unavailable | Have an authorized operator validate the account |
| DNS FAIL | FQDN did not resolve | Correct DNS; do not use IP fallback |
| TCP FAIL | Endpoint unavailable | Review existing routing/firewall policy; tool does not change it |
| WinRM FAIL | Safe category reported | Review certificate/auth/policy without bypass or TrustedHosts |
| SQL authentication FAIL | Integrated open failed | Verify TLS, database existence and Windows permissions |
| Migration/schema FAIL | Expected migration/table missing | Review controlled migration plan; tool never applies it |
| Permission FAIL | Effective permission metadata insufficient | Database owner reviews least privilege |

Mandatory `SKIPPED` is intentionally `NOT_READY`. An internal-error check means
the environmental conclusion is unsafe; review the local framework and rerun.
Reports intentionally omit raw exception details.
