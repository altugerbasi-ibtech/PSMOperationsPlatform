# Plugin Manifest

Status: **IMPLEMENTED — INTEGRATION PENDING**

The immutable `CollectorPluginDescriptor` is the repository manifest. It
declares stable plugin/strategy identity, plugin and SDK versions, subjects,
read-only status, cost, capabilities, artifact schemas, optional advisory
certification and optional safe package metadata. Registration normalizes
collections ordinally and rejects invalid or duplicate identity.

It contains no implementation type, executable path, credentials, command text
or installation behavior. Repository-built explicit registration is the only
supported loading model.
