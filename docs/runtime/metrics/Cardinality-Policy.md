# Metric Cardinality Policy

The code-owned allowlist and prohibition list in
`ExecutionMetricCatalog` are authoritative. Product-controlled strategy/plugin
identity and enums are bounded. Target, execution identity, FQDN, IP address,
machine, artifact/file/path/URL, exception, stack, user and arbitrary metadata
are prohibited.

Every catalog entry links to the shared policy in [README](README.md). Category
placement never creates a second instrument.
