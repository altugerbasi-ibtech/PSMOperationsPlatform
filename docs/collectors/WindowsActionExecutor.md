---
title: Windows Action Executor
version: 1.0.0
status: Draft
owner: Collector
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Windows Action Executor

Deferred until privileged actions are introduced. It uses a separate gMSA and processes only `WindowsAction` commands.

Candidate actions include Windows Service start/stop/restart, IIS application pool recycle, IIS site start/stop and later controlled reboot. Every action requires permission checks, audit, correlation and final-state verification.
