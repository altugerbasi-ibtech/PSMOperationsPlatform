# Changelog

## Unreleased
- Complete WP-003 configuration management with deterministic JSON, Development
  User Secrets, `PSM__` environment and command-line provider composition.
- Add capability-selected OperationsDatabase startup validation for Windows
  Integrated Authentication with secret-free failure codes.
- Add one allowlisted post-validation startup diagnostic event without
  connection target details or production-host database registration.
- Add composition, validation, diagnostics, redaction and configuration
  architecture tests.
- Implement WP-002 core domain entities and EF Core 10 SQL Server persistence model.
- Add the `InitialCreate` migration for six schemas and seven tables.
- Add domain, model metadata, migration script and SQLite persistence tests.
- Document controlled migration generation and deployment commands.
- Protect append-only persistence records and add stable persistence logging and
  unavailable-error classification.
- Reject undefined Domain enum values and remove the unused unbounded repository
  list operation.

## 0.1.0
- Initial foundation release
