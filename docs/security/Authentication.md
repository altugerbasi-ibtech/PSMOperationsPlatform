---
title: Authentication
version: 1.0.0
status: Approved
owner: Security
last_updated: 2026-07-26
product: PSM Operations Platform
---
# Authentication

The web application uses Windows Authentication and disables anonymous access. Collector services authenticate with their own identities and do not impersonate interactive users for normal collection.

The current Portal host composes ASP.NET Core IIS Integration using the IIS
default Windows Authentication scheme. An authenticated-user fallback policy
protects Portal endpoints. The generic `/health` liveness endpoint is the sole
explicit anonymous exception; it contains no target, database, migration, or
diagnostic-detail checks.

Production IIS must enable Windows Authentication and disable Anonymous
Authentication for protected Portal traffic. Basic, Forms, cookie, bearer,
custom login, forwarded-user-header trust, and application-managed credentials
are not used. Repository composition does not prove whether a live HTTP request
used Kerberos or NTLM; that requires controlled IIS evidence.
