---
title: Implementation Review Template
version: 1.0.0
status: Template — no review outcome implied
owner: Engineering
last_updated: 2026-07-29
product: PSM Operations Platform
---
# Implementation Review Template

## Work package and scope implemented

Identify the specification and map implemented behavior to its in-scope
requirements. Record any omitted or additional behavior.

## Acceptance criteria

List each criterion with repository evidence and a Pass, Fail, or Not Evaluated
result.

## Behavior and edge cases

Review normal behavior, boundaries, invalid inputs, empty cases, ordering,
version compatibility, and safe output.

## Failure classification and cancellation

Verify stable categories, isolation, cancellation propagation, and absence of
exception-text contracts.

## Determinism and immutability

Review ordering, identity, time control, immutable contracts, and mutable-state
boundaries.

## Tests

Record new and changed tests, targeted results, full-suite results, and omitted
coverage.

## Documentation

List required documents and confirm that they describe only implemented
behavior.

## Known limitations and deferred items

Record limitations, integration-pending items, and future work-package
ownership.

## Review disposition

Record `Pending`, `Changes Required`, or `Recommended for Approval`. This
template does not grant approval.
