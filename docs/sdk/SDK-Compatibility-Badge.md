# SDK Compatibility Badge

SDK compatibility and plugin monitoring readiness are independent. A
compatible plugin is not automatically Monitoring Ready.

Status: **IMPLEMENTED — ADVISORY**

`SdkCompatibilityBadgeGenerator` derives schema-version-1 label, message,
reason and supported Runtime text exclusively from
`IRuntimePluginCompatibilityMatrix`. Runtime 1.0 plus SDK 1.0 produces
`Compatible with PSM Runtime 1.0`. Unknown or incompatible results never use a
compatible label.

Markdown is deterministic and local; no remote badge service or URL is used.
The badge does not override Dispatcher compatibility validation.
