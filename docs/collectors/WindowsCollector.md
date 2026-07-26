---
title: Windows Collector
version: 1.0.0
status: Approved
owner: Collector
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Windows Collector

Responsibilities: WinRM connection tests, OS discovery, performance counters, Windows Service discovery, IIS discovery and monitoring, event logs and certificates.

Prefer WinRM HTTPS and optionally fall back to HTTP when policy permits. Record the protocol used. Use bounded timeout and cancellation. Unreachable targets must not be retried forever at the normal frequency.
