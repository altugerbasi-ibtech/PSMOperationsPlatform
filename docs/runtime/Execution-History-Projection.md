# Execution History Projection

Projection is terminal-write preferred. Run, step, attempt, transition,
artifact, and policy facts are mapped explicitly; reflection and arbitrary
serialization are prohibited. Steps use plan order, attempts use positive
attempt number, and transitions use event sequence.

If terminal truth exists but intermediate events or artifact facts are absent,
the projection is `Partial` with `HistoryFactsIncomplete`. Missing facts remain
missing instead of becoming fabricated zeroes. Unsupported schemas or
inconsistent sequences are rejected before persistence.
