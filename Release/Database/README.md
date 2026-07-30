# PSM Operations Database Release Package

This directory is the generated output location for WP-009.1. A successful
release build produces exactly:

- `PSMOperations-v{Version}.sql`
- `Manifest.json`
- `Checksums.sha256`

`Manifest.json` contains `ProductVersion`, deterministic `BuildDate` derived
from the Git commit timestamp, `GitCommit`, `SQLScriptName`, and the SQL
`SHA256`. `Checksums.sha256` covers the SQL and manifest payload artifacts.

Generated files are intentionally ignored by Git and published by CI as one
release artifact. The SQL is generated from existing EF Core migrations using
`dotnet ef migrations script --idempotent`.

The application never applies this file. An authorized DBA reviews and
executes it under WP-007.Z change control. Package generation performs no
database connection, migration application, or production validation.

DBA handoff and validation references:

- [`DeploymentGuide.md`](DeploymentGuide.md)
- [`ValidationQueries.sql`](ValidationQueries.sql)
- [`SchemaValidation.md`](SchemaValidation.md)
- [`SchemaValidation.sql`](SchemaValidation.sql)
- [`PermissionValidation.md`](PermissionValidation.md)
- [`PermissionValidation.sql`](PermissionValidation.sql)
