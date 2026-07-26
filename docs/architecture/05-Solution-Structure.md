---
title: Solution Structure
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Solution Structure

```text
src/
  PSMOperationsPlatform.Domain
  PSMOperationsPlatform.Application
  PSMOperationsPlatform.Contracts
  PSMOperationsPlatform.Infrastructure
  PSMOperationsPlatform.Web
  PSMOperationsPlatform.Collectors.Common
  PSMOperationsPlatform.WindowsCollector
  PSMOperationsPlatform.SqlCollector
  PSMOperationsPlatform.WindowsActionExecutor  # later
```

`Collectors.Common` is limited to queue leasing, heartbeat, correlation, retry and shared execution contracts. It must not become a plugin framework.
