---
title: WP-001 — Solution Skeleton
version: 1.0.0
status: Draft
owner: Engineering
last_updated: 2026-07-26
product: PSM Operations Platform
---
# WP-001 — Solution Skeleton

## Objective
Create the .NET 10 solution and empty project structure without business features.

## In scope
Solution, projects, references, central package management, nullable/analyzer settings, basic configuration, logging, health endpoints and test baseline.

## Out of scope
Domain entities, migrations, WinRM, IIS, SQL discovery, command queue, SignalR, role mapping and real remote actions.

## Acceptance criteria
- `dotnet build` succeeds
- `dotnet test` succeeds
- Web starts locally
- Both collectors start locally
- Dependency direction follows ADR-001
