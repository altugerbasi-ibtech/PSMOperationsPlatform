---
title: Collector Environment Validation Security
version: 1.0.0
status: Approved
owner: Security
last_updated: 2026-07-27
product: PSM Operations Platform
---
# Collector Environment Validation Security

## Read-only guarantee

The framework uses read/get/test/resolve/query behavior only. It contains no
remote script execution, PSSession, service control, registry/firewall/WinRM
configuration, AD mutation, certificate bypass, migration command, or SQL
mutation statement. SQL write readiness uses `HAS_PERMS_BY_NAME`.

The one allowed local write is a report file in an existing operator-supplied
directory. No directory is created automatically.

## Authentication

The tool accepts no credential or connection string. WSMan uses the executing
identity with Negotiate and no certificate bypass. SQL uses Integrated Security,
encryption, and no SQL username/password. gMSA validation uses the executing
identity and only calls `Test-ADServiceAccount` when already available.

## Redaction

Central redaction suppresses `Password`, `Pwd`, access tokens, secrets, API
keys, credential/SecureString objects, user/password combinations, and strings
that resemble raw connection strings. Server, database, machine and domain
identity remain reportable. Exception text and stack traces are suppressed.

## Enforcement

Pester parses product scripts with the PowerShell AST and rejects prohibited
mutation/remoting command names. A separate assertion rejects executable SQL
literals beginning with mutation DML/DDL. External tests use injected mocks and
never contact infrastructure.
