---
title: Repository Review Template
version: 1.0.0
status: Template — no review outcome implied
owner: Engineering
last_updated: 2026-07-29
product: PSM Operations Platform
---
# Repository Review Template

## Changed files

List the work-package-owned files and distinguish pre-existing worktree
changes.

## Build and test results

Record exact commands, configuration, counts, warnings, failures, and
environment limitations.

## Migration impact

Record whether a migration exists, prior IDs remain unchanged, whether
idempotent SQL was generated and statically reviewed, and confirm that no
migration was applied. Mark non-applicable items explicitly.

## Security and secret scan

Record the repository-approved scan and findings without exposing secrets.

## Diff validation

Record `git diff --check`, a concise `git diff` summary, and `git status`.

## Prohibited operations

Confirm which prohibited deployment, infrastructure, database, target,
service/process, migration, commit, and push operations were not performed.

## Integration-pending items

List validation that remains assigned to the approved integration gate.

## Readiness decision

Record `Pending`, `Not Ready`, or `Ready for Human Review`. This template does
not state that the work package passed review.
