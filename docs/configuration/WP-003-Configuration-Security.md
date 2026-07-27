---
title: WP-003 — Configuration Security
version: 1.0.4
status: Approved
owner: Security
last_updated: 2026-07-27
reviewers:
  - Architecture
  - Engineering
product: PSM Operations Platform
---
# WP-003 — Configuration Security

## Purpose

Define security boundaries and handling rules for startup configuration without
introducing a secret-management product or changing collector identities.

## Security Position

WP-003 does not make the application a credential manager. Authentication is
delegated to Windows identities. Runtime hosts use Windows Integrated
Authentication for SQL Server and preserve least privilege.

The platform SHALL NOT store, encrypt, rotate or distribute database passwords.
SQL Authentication is not a fallback.

## Identity Boundary

| Component | Identity model | Configuration consequence |
|---|---|---|
| Web | Dedicated Web identity/gMSA | Only application database permissions |
| Windows Collector | Dedicated Windows Collector gMSA | Windows target access remains separate from SQL target access |
| SQL Collector | Dedicated SQL Collector gMSA | SQL target permissions remain separate from Windows target access |
| Future Worker | Defined by its future work package | No identity or permissions invented in WP-003 |

A common options type or connection-string key SHALL NOT be interpreted as
shared permission. Windows and SQL Collector target permissions MUST NOT be
combined under one identity.

## Connection String Security

The central connection string SHALL reside under:

```text
ConnectionStrings:OperationsDatabase
```

Deployment environment-variable form:

```text
PSM__ConnectionStrings__OperationsDatabase
```

Rules:

- Windows Integrated Authentication is mandatory.
- The connection string is read through the standard named-connection API and
  SHALL NOT be bound or copied into any options property.
- SQL usernames and passwords are forbidden.
- SQL Authentication aliases and explicit password modes are rejected during
  startup validation.
- The full connection string SHALL NOT be logged, audited, returned in health
  output or shown to operators.
- Server and database names SHALL not be included in the startup summary.
- Runtime identities SHALL receive only required database permissions.
- Migration identities and runtime identities remain separate concerns.

WP-003 intentionally does not mandate `Encrypt=True` or
`TrustServerCertificate=False`. This document SHALL NOT be used to infer either
requirement.

WP-003.2 validates with standard SqlClient builders and returns only stable
failure codes. It rejects credential aliases even when their explicit value is
empty. Parser exception text, connection strings, server/database identifiers,
usernames and passwords are not included in validation failures or inner
exceptions.

## Source Control

Repository configuration files MAY contain non-sensitive defaults and section
shape. They SHALL NOT contain:

- usernames or passwords;
- production connection strings;
- private keys, certificates or reusable authentication material;
- User Secrets values;
- environment-specific sensitive topology;
- vault references that imply an unapproved integration.

Examples in documentation and tests SHALL use unmistakably non-production
values. Accidental secret discovery SHALL follow the organization’s incident
and credential-rotation process; WP-003 does not define that process.

## Environment Variables

Environment variables are an approved override mechanism, not a vault. They
override both JSON sources and Development User Secrets. Command-line arguments
remain the highest-precedence source.

Security characteristics:

- process environment can be inspected by sufficiently privileged local
  operators or diagnostic tools;
- service configuration and deployment logs can expose values if handled
  carelessly;
- variable names may be logged, but sensitive values SHALL NOT be logged;
- application variables SHALL use `PSM__` so their ownership and mapping are
  explicit;
- operational tooling SHALL avoid echoing values during deployment.

Windows service ACLs, deployment permissions and gMSA rights remain the primary
controls around production configuration.

## Command-Line Configuration

Command-line arguments have higher precedence than environment variables and
may be visible in process inspection or service definitions. Operators SHOULD
prefer the approved environment mechanism for connection strings. Regardless
of source, command-line values SHALL never be copied to startup logs.

## User Secrets

User Secrets:

- MAY be loaded in Development;
- SHALL NOT be loaded in Production, Test or other non-Development
  environments under WP-003;
- are intended for developer convenience;
- are not encrypted production storage;
- SHALL NOT be treated as proof of enterprise secret-management compliance;
- SHALL NOT be included in source control or startup diagnostics.

The environment gate must be based on the effective host environment before
application options are bound. User Secrets override JSON but remain below
`PSM__`-prefixed environment variables and command-line arguments.

## Sensitive Logging

### Allowed startup fields

- environment;
- OperationsDatabase configured boolean;
- authentication mode label `Integrated`;
- configuration validation succeeded boolean.

### Prohibited startup fields

- raw or partially masked connection strings;
- server and database names;
- user names or passwords;
- environment-variable values;
- command-line values;
- User Secrets values or identifiers;
- serialized configuration or options;
- exception messages that echo configuration input.

Connection strings SHALL be omitted rather than redacted. Redaction is fragile
because keyword aliases, formatting and future additions can leak data.

## Error and Diagnostic Handling

Validation errors SHALL use safe rule identifiers and descriptions. Health
checks, Problem Details, Windows Event Log entries and console logs SHALL not
expose raw configuration.

The startup summary is diagnostic logging, not an audit record. It runs only
when the host has selected the operations database capability and validation
has succeeded. It confirms configuration posture without proving remote
connectivity or permissions. Current Web, Windows Collector and SQL Collector
hosts do not consume persistence, so they SHALL NOT register the capability or
emit this summary in WP-003.

WP-003.3 implements one Information event, ID `2200` and name
`OperationsDatabaseConfigurationValidated`. Its structured state is restricted
to environment name, configured `true`, authentication mode `Integrated` and
configuration validation succeeded `true`. The diagnostic component has no
configuration accessor, connection-string builder or connection-string field.
Tests inspect formatted output, structured state, exception and scope data with
server, database, SQL-user, password and unrelated-environment-value sentinels.
None may appear.

## Secret Management Boundary

No Vault, CyberArk, KeyVault, Azure App Configuration or custom secret store is
introduced.

A future approved integration can use the standard .NET configuration-provider
boundary:

```text
Future approved provider
        |
        v
IConfiguration pipeline
        |
        v
Existing options binding and validation
```

This is the extension point. WP-003 SHALL NOT add a vault interface, provider
package, placeholder secret URI model or provider-selection option. A future ADR
must define provider precedence, identity, failure mode, caching, rotation and
operational ownership before implementation.

## Threat Analysis

| Threat | Control |
|---|---|
| SQL credential committed to source | Windows Authentication only; repository review and tests |
| SQL Authentication enabled through alias | Parse and validate normalized connection string |
| Secret leaked through startup summary | Allowlist fields; never serialize options |
| Secret leaked in validation exception | Stable safe errors; do not echo parser input |
| Production loads developer secrets | Explicit Development-only provider gate |
| Unprefixed environment variable overrides policy | Add only `PSM__` application environment provider |
| Collector permission boundary erodes | Separate hosts, identities and deployment permissions |
| Command line exposes connection string | Operational preference for environment source; never log arguments |
| Future vault abstraction becomes speculative framework | Require future ADR and standard provider extension point |

## Security Test Strategy

- verify all SQL Authentication keyword aliases are rejected;
- verify Windows Integrated Authentication aliases are accepted;
- scan startup and validation output for connection-string fragments;
- prove Production does not load User Secrets;
- prove unprefixed application environment variables do not override values;
- prove each collector host has no dependency on the other collector host;
- prove the connection string is absent from every options property;
- prove hosts without the capability do not require a connection string;
- prove no credential properties or vault placeholders exist;
- review repository JSON and samples for sensitive values;
- verify no options object is serialized to logs.

## Risks

- Windows Authentication removes stored passwords but does not remove the need
  for least-privilege database grants.
- Environment variables and command line are visible to privileged local
  observers.
- Server/database identifiers may be sensitive infrastructure information even
  without credentials; they are excluded from summary logging.
- Prefix integration can accidentally disturb framework-owned host variables;
  implementation must keep framework host configuration separate and test the
  standard provider behavior.

## References

- [`WP-003-Configuration-Architecture.md`](WP-003-Configuration-Architecture.md)
- [`WP-003-Configuration-Validation.md`](WP-003-Configuration-Validation.md)
- [`../tasks/WP-003-Configuration-Management.md`](../tasks/WP-003-Configuration-Management.md)
- [`../architecture/08-Security-Architecture.md`](../architecture/08-Security-Architecture.md)
- [`../adr/ADR-003-Collector-Separation-by-Security-Boundary.md`](../adr/ADR-003-Collector-Separation-by-Security-Boundary.md)
- [`../handbook/Logging.md`](../handbook/Logging.md)

## Revision History

| Version | Date | Description |
|---|---|---|
| 1.0.0 | 2026-07-26 | Initial security design with precedence reconciliation |
| 1.0.1 | 2026-07-26 | Removed connection string from options and limited diagnostics |
| 1.0.2 | 2026-07-26 | Recorded WP-003.2 credential detection and redaction evidence |
| 1.0.3 | 2026-07-26 | Recorded WP-003.3 allowlisted diagnostic event and redaction evidence |
| 1.0.4 | 2026-07-27 | Closed security and secret-handling review |
