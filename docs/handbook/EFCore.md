---
title: EF Core Standards
version: 1.0.0
status: Approved
owner: Engineering
last_updated: 2026-07-26
product: PSM Operations Platform
---
# EF Core Standards

Use `IEntityTypeConfiguration<T>`, disable lazy loading, use `AsNoTracking` for reads, do not expose IQueryable outside Infrastructure, review migrations and add indexes for known queries.
