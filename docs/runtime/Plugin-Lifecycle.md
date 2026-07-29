# Plugin Lifecycle

WP-008.5 supports explicit in-process fake/test handler registration only. Dispatcher resolves and validates the immutable descriptor and capabilities; Runtime invokes the supplied handler. Discovery, loading, packaging, and production lifecycle remain deferred to WP-008.6.

WP-008.4 explicitly resolves a minimal handler by exact ordinal StrategyCode,
validates its immutable descriptor, captures PluginVersion, supplies a narrow
context, and invokes one cancellable asynchronous operation per attempt.

Only test fakes are implemented. Production plugin packaging, discovery,
installation, configuration, validation, and lifecycle are deferred to
WP-008.5. No reflection or dynamic assembly loading exists.
