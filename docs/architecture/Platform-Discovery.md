# Platform Discovery

Status: **IMPLEMENTED — INTEGRATION PENDING**

WP-007.6 modules emit normalized IIS, .NET, PowerShell, role, and feature facts
through independent contracts. They make no cross-module decision and contain
no configuration or remediation behavior.

The Capability Engine is the sole approved consumer that combines these fact
categories. Discovery remains read-only, PowerShell 5.1 compatible on targets,
and uses the existing shared Kerberos WinRM session.

The Decision Engine consumes committed capabilities only. Discovery modules do
not select strategies, consume decision contracts, or emit execution plans.
