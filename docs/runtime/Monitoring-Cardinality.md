# Monitoring Cardinality

The detailed policy and every category entry are indexed under
[metrics/Cardinality-Policy.md](metrics/Cardinality-Policy.md).

Metric dimensions are bounded and code-owned. Permitted keys are
`strategy.code`, `plugin.id`, `execution.outcome`, `failure.category`,
bounded `reason.code`, `subject`, SDK major version, Runtime contract version
and certification status.

ManagedServerId, ExecutionRunId, ExecutionPlanId, step ID, FQDN, IP/machine
name, artifact/file identity or path, URL, user, exception/stack text and
arbitrary plugin metadata are prohibited. Safe correlation IDs may remain in
structured logs but never metric dimensions.
