---
title: Architecture Review Template
version: 1.0.0
status: Template — no review outcome implied
owner: Architecture
last_updated: 2026-07-29
product: PSM Operations Platform
---
# Architecture Review Template

Use this after implementation to record evidence. A copied template is not an
approval and Codex cannot approve its own work.

## Work package and evidence

Identify the work package, specification version/status, reviewed change set,
and relevant ADRs, Architecture Exceptions, contracts, and architecture
documents.

## Architectural boundary compliance

Record whether the implementation stayed inside the approved component and
responsibility boundaries.

## Architecture Freeze compliance

Identify the applicable freeze version, any detected conflict, and whether an
ADR or Architecture Exception is required before approval.

## Responsibility separation

Review upstream/downstream ownership, dependency direction, immutable versus
mutable concerns, and deferred responsibilities.

## Security and read-only boundary

Review identities, least privilege, target access, secret handling,
authentication, read-only guarantees where applicable, and prohibited write
paths.

## Approved deferrals

List each deferred item and its owning future work package or integration gate.

## Remaining architectural risks

Record concrete unresolved risks, their effect, and the authority needed to
accept or correct them.

## Review disposition

Record `Pending`, `Changes Required`, or `Recommended for Approval`. Final
approval belongs to the designated human reviewer.
