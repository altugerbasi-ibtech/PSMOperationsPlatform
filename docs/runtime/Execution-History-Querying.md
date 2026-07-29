# Execution History Querying

Use exact read methods rather than exposing EF queries. Every list request has
validated paging and a maximum size of 200. Date filters are closed bounded
ranges; string filters are exact bounded product codes.

Runs order newest first with execution-run ID as a deterministic tie-breaker.
Children use explicit logical ordering. Deep offset paging may cost more;
keyset paging is deferred until measured evidence justifies it.
