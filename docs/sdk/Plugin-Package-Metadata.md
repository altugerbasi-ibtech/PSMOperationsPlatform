# Plugin Package Metadata

Status: **IMPLEMENTED — METADATA ONLY**

Schema version 1 optionally records bounded author, company, license, support,
project and repository references. Values are immutable, trimmed and
deterministically serialized. Local/rooted/file/executable references, unsafe
schemes and credential-like query values are rejected.

This metadata performs no package distribution, installation, download,
loading, trust or network validation.
