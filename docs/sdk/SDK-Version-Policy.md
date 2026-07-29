# Collector Plugin SDK Version Policy

Status: **IMPLEMENTED — INTEGRATION PENDING**

`CollectorPluginSdkVersion` is the durable public contract version. Current SDK
version is 1.0. Runtime contract 1.0 supports SDK 1.0 only. The SDK version
changes when public plugin contracts or shared semantics change; adding a
plugin, tests or editorial documentation does not change it.

`PluginVersion`, `RuntimeVersion`, `DescriptorSchemaVersion`,
`ArtifactSchemaVersion`, certification/package schema versions and monitoring
schema versions are independent. Assembly, build, timestamp, Git and database
versions are not SDK versions.
