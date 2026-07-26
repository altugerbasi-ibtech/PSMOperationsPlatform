---
title: WinRM Standards
version: 1.0.0
status: Approved
owner: Engineering
last_updated: 2026-07-26
product: PSM Operations Platform
---
# WinRM Standards

Prefer HTTPS; optionally fall back to HTTP only when configured. Record protocol, use bounded timeout and cancellation, classify DNS/firewall/authentication/authorization failures separately and reduce probe frequency for unreachable targets.
