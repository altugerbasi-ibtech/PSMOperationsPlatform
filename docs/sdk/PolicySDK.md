# Policy SDK Boundary

Plan steps retain versioned policy references. Dispatcher resolves them through the explicit product-owned catalog into one immutable `ExecutionPolicy`. Runtime consumes that value and never queries the catalog. Policy scripting, configuration reload, and silent downgrade are prohibited.

This is an internal contract, not a user-editable policy SDK.

Plugin authors inspect immutable resolved policy only to validate declared compatibility and perform the single operation. Runtime enforces it. See the separate timeout, retry, throttling, parallel and batching documents under `docs/runtime/`.
