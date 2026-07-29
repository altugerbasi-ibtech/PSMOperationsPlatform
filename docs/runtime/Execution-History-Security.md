# Execution History Security

History stores operational metadata: internal IDs, safe versions/codes,
timestamps, durations, outcomes, counts, resolved policy, and approved logical
artifact metadata.

It excludes credentials, connection strings, tokens, Kerberos material,
authentication headers, user sessions, raw PowerShell/commands/artifacts,
files, stack traces, exception text, local paths, secret URLs, arbitrary plugin
metadata, and unapproved user identity. History is not Audit.
