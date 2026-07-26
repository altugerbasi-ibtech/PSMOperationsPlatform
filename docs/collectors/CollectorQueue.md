---
title: Collector Command Queue
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Collector Command Queue

Recommended fields: Id, CommandType, CollectorType, TargetId, PayloadJson, Priority, Status, RetryCount, MaxRetryCount, AvailableAfterUtc, LeaseOwner, LeaseExpiresUtc, CreatedUtc, StartedUtc, CompletedUtc, ErrorCode, ErrorMessage and CorrelationId.

States: Pending, Leased, Running, Succeeded, Failed, Cancelled and DeadLetter. Expired leases may be recovered. Handlers should be idempotent where practical.
