---
title: System Overview
version: 1.0.0
status: Approved
owner: Architecture
last_updated: 2026-07-26
product: PSM Operations Platform
---
# System Overview

```text
Operators
   |
   v
Web Application
   |
   v
Central SQL Server
   |-- configuration, inventory, monitoring, alerts, audit, queue
   +--> Windows Collector --> Windows/IIS/Services
   +--> SQL Collector -----> SQL Server
   +--> Windows Action Executor (future)
```

The web application never directly connects to managed targets. Collectors do not depend on the web process being online.
