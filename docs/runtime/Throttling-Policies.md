# Throttling Policies

`Lightweight` v1 allows at most four, `Standard` v1 at most two, and `Heavy` v1 at most one concurrent operation within the implemented in-process boundary. Dispatcher resolves the declared class; Runtime enforces the bound and cancellation and records wait duration. Plugins cannot change throttling. Distributed or target-configured throttling is deferred.

Cancellation interrupts waiting; a throttling fault is classified separately. Tests cover each fixed bound, Heavy ≤ Standard ≤ Lightweight, wait metrics and cancellation.
