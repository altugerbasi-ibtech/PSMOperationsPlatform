---
title: WP-003 — Configuration Validation
version: 1.0.4
status: Approved
owner: Architecture
last_updated: 2026-07-27
reviewers:
  - Engineering
  - Security
product: PSM Operations Platform
---
# WP-003 — Configuration Validation

## Purpose

Define fail-fast validation for the named OperationsDatabase connection,
including standard `ValidateOnStart` participation without a secret-bearing
options property, semantic connection-string validation, startup behavior and
safe errors.

## Validation Goals

- reject missing, malformed or unsafe critical configuration before runtime
  work starts;
- use clear, stable and actionable failure rules;
- reject SQL Authentication in every supported keyword form;
- avoid logging or returning sensitive configuration;
- keep simple validation simple and semantic validation explicit;
- avoid speculative rules unrelated to approved requirements.

## Decision

WP-003 SHALL use the standard options validation pipeline with an internal,
property-free capability marker:

1. The capability extension registers the marker without binding a
   configuration section.
2. A dedicated `IValidateOptions<InternalMarker>` implementation receives
   `IConfiguration`.
3. The validator reads
   `GetConnectionString("OperationsDatabase")`, validates the local value and
   discards it.
4. `ValidateOnStart` causes validation during startup only for a service
   provider that registered the capability.
5. Startup diagnostics execute only after validation succeeds.

The internal marker is not `PersistenceOptions`, is not public configuration
surface and contains no properties. It exists only to activate the standard
startup-validation pipeline. The connection string SHALL NOT be copied into the
marker.

DataAnnotations are not applicable because WP-003 has no approved options
properties. They remain the preferred mechanism for simple property rules when
a future Work Package introduces a real strongly typed options model. Creating
an empty `PersistenceOptions` solely to use DataAnnotations is rejected.

## Persistence Validation Rules

`ConnectionStrings:OperationsDatabase` SHALL:

1. exist and contain a non-whitespace value;
2. parse as a SQL Server connection string using the provider’s connection
   string builder;
3. identify a server/data source;
4. identify a database/initial catalog;
5. enable Windows Integrated Authentication through a recognized alias;
6. contain no username keyword or non-empty username;
7. contain no password keyword or non-empty password;
8. contain no explicit SQL-password authentication mode;
9. contain no contradictory authentication settings.

`User ID`, `UID`, `Password` and `PWD` are forbidden regardless of casing or
alias normalization.

WP-003.2 uses `SqlConnectionStringBuilder` for SQL semantics. Because SqlClient
normalizes an explicitly supplied empty credential to its default value, two
independent sentinel-prefixed SqlClient parses detect whether `User ID`/`UID`
or `Password`/`PWD` was explicitly supplied. This is still standard SqlClient
parsing; no custom connection-string parser or raw substring search is used.

Recognized equivalent keyword forms SHALL be normalized by the SQL Server
connection-string parser rather than checked with case-sensitive substrings.

Examples that SHALL pass the authentication rule:

```text
Server=sql01;Database=PSMOperationsPlatform;Integrated Security=True
Server=sql01;Initial Catalog=PSMOperationsPlatform;Trusted_Connection=True
```

Examples that SHALL fail:

```text
Server=sql01;Database=PSMOperationsPlatform;User ID=app;Password=secret
Server=sql01;Database=PSMOperationsPlatform;UID=app;Pwd=secret
Server=sql01;Database=PSMOperationsPlatform;Integrated Security=False
```

The examples are documentation inputs only and SHALL NOT appear in runtime
logs.

## Deliberately Absent TLS Policy

Validation SHALL NOT:

- require `Encrypt=True`;
- require `TrustServerCertificate=False`;
- reject `TrustServerCertificate=True` solely because it is true;
- introduce a new certificate or transport-security policy implicitly.

These omissions are deliberate approved design decisions. Syntax supported by
the SQL client may still be parsed normally; WP-003 adds no TLS-value mandate.

## Cross-Property Validation

WP-003 has no configuration properties and therefore no cross-property rule.
Future rules belong to the Work Package that introduces their real options
model and consumer.

## Startup Behavior

The named connection is critical only for a host that selects the capability.
For that host, startup failure SHALL:

- prevent the host from entering normal runtime;
- prevent background services from beginning work;
- produce a non-successful process startup outcome;
- identify the options section and stable validation rule;
- omit raw input and connection-string values.

Hosts that do not register the capability do not require the connection string,
do not run this validation and do not emit its success summary.

Validation is not a readiness substitute. A syntactically valid connection
string does not prove SQL availability. Connectivity health checks are a
separate operational concern and are not added by this design.

## Failure Message Design

Implemented WP-003.2 identifiers:

| Failure code | Condition |
|---|---|
| `OperationsDatabase.Missing` | Named connection is null, empty or whitespace |
| `OperationsDatabase.Malformed` | SQL syntax, data source or initial catalog is invalid |
| `OperationsDatabase.IntegratedAuthenticationRequired` | Integrated authentication is disabled or absent |
| `OperationsDatabase.SqlAuthenticationNotSupported` | A SQL credential key is explicitly supplied |

Only the code is returned in `ValidateOptionsResult`. Parser messages, inner
exceptions and configuration values are not propagated.

Messages SHALL NOT echo the invalid value, offending keyword value, server,
database, username or password.

## Validation and Logging Boundary

WP-003.3 adds a one-shot diagnostic hosted service after the existing
`ValidateOnStart` registration. Standard host startup validation executes before
hosted services start. Consequently, the success diagnostic cannot run when
validation fails.

The diagnostic emits only Information event `2200`,
`OperationsDatabaseConfigurationValidated`, with the approved safe metadata. It
does not add a validation-failure log and does not receive the options marker,
configuration, named-connection accessor or parser object. Validation failures
therefore retain the WP-003.2 stable-code behavior without duplicate diagnostic
logging.

## Test Strategy

### Unit tests

- missing, empty and whitespace values fail;
- malformed values fail safely;
- missing server or database fails;
- both Windows Authentication aliases pass;
- SQL username/password aliases fail regardless of casing;
- explicit non-integrated authentication fails;
- contradictory keywords fail;
- `Encrypt` omitted passes;
- `Encrypt=False` is not rejected by a WP-003-specific rule;
- `TrustServerCertificate=True` is not rejected by a WP-003-specific rule;
- failures contain no source value or connection-string fragment.

### Integration tests

- startup validation executes for a test service provider or minimal test host
  that selects the capability;
- no current production host is forced to select the capability;
- an invalid environment override fails startup;
- invalid configuration prevents hosted service execution;
- validation observes the final approved provider precedence;
- Development User Secrets override JSON but are overridden by prefixed
  environment variables and command line;
- Production does not load User Secrets.

### Architecture tests

- validation remains outside Domain and Application;
- no validator loads SQL-backed configuration;
- no `IOptionsMonitor<T>` is registered or consumed;
- options registration is host-selective.

### Acceptance tests

- Windows Authentication configuration starts successfully;
- SQL Authentication configuration is rejected;
- startup output is sanitized;
- no TLS-policy requirement beyond the approved decisions appears.

## Risks

- Naive substring matching can miss aliases or misclassify values.
- Parser exception messages may include input details; they must not be logged
  blindly.
- A future validator could accidentally perform network I/O. Options validation
  SHALL remain deterministic and side-effect-free.
- Testing only direct validator calls can miss host startup ordering; integration
  coverage is required.

## References

- [`WP-003-Configuration-Architecture.md`](WP-003-Configuration-Architecture.md)
- [`WP-003-Configuration-Security.md`](WP-003-Configuration-Security.md)
- [`../tasks/WP-003-Configuration-Management.md`](../tasks/WP-003-Configuration-Management.md)
- [`../handbook/Logging.md`](../handbook/Logging.md)

## Revision History

| Version | Date | Description |
|---|---|---|
| 1.0.0 | 2026-07-26 | Initial design with precedence and alias reconciliation |
| 1.0.1 | 2026-07-26 | Replaced secret-bearing options with internal validation marker |
| 1.0.2 | 2026-07-26 | Recorded WP-003.2 validator, failures and redaction behavior |
| 1.0.3 | 2026-07-26 | Recorded validation-success ordering for WP-003.3 diagnostics |
| 1.0.4 | 2026-07-27 | Closed validation review against fail-fast and redaction evidence |
