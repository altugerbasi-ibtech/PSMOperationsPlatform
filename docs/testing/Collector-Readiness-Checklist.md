---
title: Collector Readiness Checklist
version: 1.0.0
status: Approved
owner: Engineering
last_updated: 2026-07-27
product: PSM Operations Platform
---
# Collector Readiness Checklist

## Before execution

- WP-005.S1 execution gate and explicit approval recorded
- Collector host, install path, service and expected identity explicit
- Existing output directory selected
- For SmokeTest: dedicated target and SQL database confirmed non-production
- Transport policy/ports and migration status known
- No credential or raw connection string supplied

## CollectorHost categories

- Supported x64 Windows Server 2022 or later
- Domain membership, FQDN, Türkiye timezone and time-service state
- CPU/memory/free-space measurements (no invented thresholds)
- Deployment type and .NET 10 runtime when framework-dependent
- Executable, assembly, deps and version evidence
- OperationsDatabase source, Integrated Authentication and secret scan
- Service registration, binary path/quoting, account, state and process path
- gMSA validation when applicable

## SmokeTest additions

- Target and SQL forward DNS
- Required TCP endpoints
- HTTPS-first WSMan behavior and policy-limited HTTP fallback
- Integrated SQL authentication
- Expected migration and eight inventory tables
- Read and collector-write effective permission metadata

## Completion

- JSON and Markdown generated at fixed paths
- Exit code matches report
- FAIL recommendations are manual only
- No remediation or infrastructure mutation occurred
