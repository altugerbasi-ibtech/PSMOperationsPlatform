# WP-007.Z.2 Deployment Summary

## Status

`BLOCKED`

This is the sanitized repository evidence index. The live deployment has not
been attempted because the approved environment inputs, release payload,
change authorization, identities, and backup decision were not supplied.

## Deployment Record

| Field | Value |
|---|---|
| Package version | NOT PROVIDED |
| Git commit from manifest | NOT PROVIDED |
| Target SQL version/edition | NOT VALIDATED |
| Target database | NOT PROVIDED |
| Start time (UTC/local) | NOT STARTED |
| Finish time (UTC/local) | NOT STARTED |
| First execution | NOT RUN |
| Idempotency execution | NOT RUN |
| Schema validation | NOT RUN |
| Collector permission validation | NOT RUN |
| Portal permission validation | NOT RUN |
| SQL Collector permission validation | NOT RUN |
| Service-identity connectivity | NOT RUN |
| Collector execution | PROHIBITED / NOT RUN |

## Files Changed

- `docs/work-packages/WP-007.Z.2-Operations-Database-Deployment.md`
- `docs/index.md`
- `.gitignore`
- `artifacts/validation/WP-007.Z.2/` evidence structure

## Repository Validation

Populate with exact command, timestamp, result, and retained output after local
validation. Do not claim a live SQL result from an offline test.

| Test | Result | Evidence |
|---|---|---|
| Release PowerShell/Pester tests | PASS - 50/50 | Console output; no live target used |
| Database package tests | PASS - included in 50/50 | Console output; no live target used |
| Solution build/tests | PASS - build 0 warnings/errors; 476/476 tests | Console output |
| Release bundle generation | PASS - offline Pester generation contract | `ReleaseBundle.Tests.ps1`; a real bundle requires the absent immutable SQL payload |
| Checksum generation/verification | PASS - offline Pester contract | `PSMOperationsSqlPackage.Tests.ps1` and `ReleaseBundle.Tests.ps1` |
| Runtime migration prohibition | PASS | Release and architecture tests |

## Open Risks and Blockers

- Approved target and confidential environment values are absent.
- The immutable WP-009.1 payload is not present in the repository by design.
- Runtime principal names and Security-approved grants are absent.
- Backup/restore ownership and deployment authorization are absent.
- Live schema, permission, TLS, connectivity, and idempotency results therefore
  cannot be asserted.

## Evidence Index

- `PreDeploymentResults/` - environment and pre-change output
- `PostDeploymentResults/` - post-change and second-run comparisons
- `SchemaValidationResults/` - schema validator output
- `PermissionValidationResults/` - validator and per-identity connectivity
- `Checksums/` - manifest, checksum catalog, and verification transcript
- `Logs/` - unmodified deployment and repository-test output

Do not add passwords, connection strings, confidential host names, account
names, certificate private material, or unredacted infrastructure exports.
