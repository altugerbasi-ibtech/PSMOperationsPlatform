# Changelog

## Unreleased
- Complete WP-004 Windows Collector foundation and target connectivity after
  final architecture, security, persistence, scope and quality review.
- Complete WP-004.5 with last-known connectivity persistence, deterministic
  failure backoff capped at 60 minutes, per-result scoped saves and one bounded
  rowversion retry; no history, alert, inventory or remote action is included.
- Complete WP-004.4 with an in-process PowerShell/WSMan connectivity probe,
  HTTPS-first conditional fallback, bounded timeout/cancellation, deterministic
  cleanup and at most 20 concurrent target probes; persistence remains pending.
- Complete WP-004.3A target connectivity configuration model with validated
  target-specific WinRM mode, ports and timeout, controlled existing-row
  backfill and extended eligible-target projection; WinRM remains unimplemented.
- Complete WP-004.3 with a scoped, cancellation-aware eligible Windows target
  query, immutable no-tracking projection and controlled nullable eligibility
  migration; no remote probe or connectivity-state mutation is included.
- Complete WP-004.2 Windows Collector host foundation with Windows Service
  hosting, OperationsDatabase composition, scoped no-op polling lifecycle and
  safe correlated logging; target connectivity remains unimplemented.
- Add WP-004.1 analysis and proposed documentation for Windows Collector
  foundation and target connectivity; no runtime implementation is included.
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
