# Runtime–SDK Compatibility Matrix

| Runtime contract | Minimum SDK | Maximum SDK | Status |
|---|---:|---:|---|
| 1.0 | 1.0 | 1.0 | Compatible |

Compatibility badges are derived solely from this matrix and remain advisory.
Unknown compatibility never produces a compatible badge.

All other Runtime or SDK versions are explicitly unsupported in WP-008.6. Missing Runtime identity is `Unknown`, never Compatible. Compatibility is code-owned, ordinal and deterministic; it is not configured from files, databases, packages, assemblies or networks.

Dispatcher validates the normalized descriptor against this matrix before policy resolution and Runtime submission. Rejection creates no attempt state, does not mutate the plan and reports a stable reason and safe explanation.
