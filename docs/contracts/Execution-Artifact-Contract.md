# Execution Artifact Contract

`ExecutionArtifacts` is an immutable bounded envelope with `ArtifactSchemaVersion = 1`. It groups sorted file references, object references/counts, generated metrics, and warnings. Artifact IDs are stable, nonblank, and unique across groups; sizes/counts are non-negative. Empty and `NoData` results are valid.

The contract carries no streams, handles, arbitrary object graphs, credentials, commands, or unbounded binary payload. WP-008.5 does not implement production artifact storage. Execution State retains aggregate metrics only.
