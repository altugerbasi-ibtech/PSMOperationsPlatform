# Plugin Metrics

Plugin dimensions are limited to explicitly registered normalized `plugin.id`
and `strategy.code`; arbitrary metadata is prohibited. Current plugin
validation and SDK compatibility instruments are documented under
[Dispatcher metrics](Dispatcher-Metrics.md). No additional plugin instrument is
implemented merely for this category.

Monitoring readiness is advisory SDK metadata and is not currently emitted as
a metric.
