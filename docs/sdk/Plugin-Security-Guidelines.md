# Plugin Security Guidelines

Certification and package metadata are not trust or signing. They must contain
no token-bearing URLs, local/executable paths or secrets. Compatibility badges
perform no network access.

Plugins are read-only and repository-built. Never accept credentials, connection strings, Kerberos tickets, gMSA secrets, service providers, configuration roots, database contexts, unrestricted commands/filesystems/registries or mutable Runtime internals.

Do not contact targets during validation. Honor cancellation. Return only normalized safe explanations, warnings, diagnostics and bounded artifact metadata. Never return raw command output, exception stacks, secret values, open handles or arbitrary object graphs.

Kerberos-only authentication, `IncludePortInSPN`, Windows PowerShell 5.1 target compatibility and Collector PowerShell 7.x compatibility are outside plugin control and remain unchanged.
