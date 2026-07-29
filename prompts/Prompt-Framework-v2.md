---
title: Prompt Framework v2
version: 2.0.0
status: Approved and Active
owner: Engineering
last_updated: 2026-07-29
product: PSM Operations Platform
---
# Prompt Framework v2

## Governing rule

> Prompts launch work. Repository specifications define work.

A launch prompt is a short routing instruction. It is not an architecture,
contract, acceptance-test, or implementation specification. The referenced
approved work-package specification is the authoritative source for objective,
scope, requirements, tests, documentation, validation, deferrals, and the
completion line.

## Required launch content

A valid Codex launch prompt identifies:

- the current project phase;
- Architecture Freeze status;
- mandatory repository reading;
- the exact work-package specification path;
- prohibited operations; and
- the exact required readiness line.

The prompt must direct Codex to implement only the referenced specification and
must not copy the complete architecture, engineering handbook, contracts, or
work-package body into the prompt.

## Preconditions

Implementation must not begin when:

- the referenced specification file does not exist;
- its status is not `Approved for Implementation` or `In Implementation`;
- a required section is absent or materially incomplete;
- it contains unresolved replacement fields, `TBD`, `TODO`, ellipses, or
  placeholder work-package identifiers;
- it conflicts with an accepted ADR, Architecture Freeze, security boundary, or
  newer approved repository authority; or
- its required completion line is unresolved.

Codex reports the blocking condition and finishes `NOT READY`. It must not
invent requirements or silently repair scope while implementing.

## Minimal launcher shape

An executable launcher identifies a real work-package ID and path, for example:

```text
CURRENT PROJECT PHASE

Repository Complete

Architecture Freeze v1.0

Read and follow strictly:

- docs/engineering/PSM-Engineering-Standards.md
- docs/contracts/*
- docs/architecture/*
- docs/runtime/*
- docs/sdk/*
- docs/decision-rules/*
- workpackages/WP-008.5.md

Implement the referenced work package only.

Do not violate Architecture Freeze v1.0.

Do not deploy, contact real infrastructure, apply migrations, commit or push.

Finish with exactly:

READY FOR WP-008.5 REVIEW

or:

NOT READY
```

`WP-XXXX` must never be used literally in an executable prompt. All launcher
replacement fields must be resolved before use.

## Authority and amendments

If a launch prompt conflicts with its repository specification, the
specification governs unless the prompt explicitly authorizes a reviewed
specification amendment. Scope changes are made in the specification first,
with status and review impact updated according to
[`workpackages/README.md`](../workpackages/README.md).
