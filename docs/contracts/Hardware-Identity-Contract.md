# Hardware Identity Contract

Status: **IMPLEMENTED — INTEGRATION PENDING**

Plural hardware identity priority is:

1. vendor stable identifier;
2. firmware stable identifier;
3. stable operating-system identifier;
4. deterministic normalized hardware hash;
5. deterministic occurrence index.

An identifier must be present, normalized, structurally valid, stable for the
object, not a module-owned placeholder, and not ambiguously duplicated.
Placeholder sets are explicit per module; there is no broad global erasure
rule.

Distinct normalized rows with the same strongest identity fail validation.
They are never merged or selected arbitrarily. Exact provider duplicates may
be collapsed only where the module contract proves they cannot represent
distinct hardware. Collection order is never identity.
