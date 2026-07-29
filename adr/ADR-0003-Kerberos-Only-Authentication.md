# ADR-0003 — Kerberos-Only Authentication

## Status

Accepted

## Date

2026-07-29

## Context

The Windows Collector security boundary requires authenticated, delegated access without embedded credentials or downgrade paths.

## Decision

Use Windows Authentication and Kerberos-only WinRM with `AuthenticationMechanism.Kerberos`, the actual per-target configured port (`WinRmHttpsPort`, default 5986, or the explicitly approved `WinRmHttpPort`, default 5985), and `IncludePortInSPN` enabled. The Collector runs under its approved gMSA/process identity and supplies no explicit credentials. Negotiate and NTLM fallback, TrustedHosts, Basic, and CredSSP are prohibited.

## Consequences

SPNs, DNS, port configuration, and gMSA rights must be correct at the WP-007.Z integration gate. Failures do not trigger authentication downgrade.

## Security Impact

This prevents credential storage and weaker authentication fallback and preserves the Windows Collector identity boundary.

## Migration/Compatibility Impact

No implementation or configuration change is made by this ADR.

## Alternatives Considered

NTLM/Negotiate fallback, Basic, CredSSP, explicit credentials, and TrustedHosts were rejected.

## Related Documents

- [Security Architecture](../docs/architecture/08-Security-Architecture.md)
- [Engineering Standards](../docs/engineering/PSM-Engineering-Standards.md)

## Supersession Rules

Any authentication change requires a later accepted security ADR explicitly superseding ADR-0003.
