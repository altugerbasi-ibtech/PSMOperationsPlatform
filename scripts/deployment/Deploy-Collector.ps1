#requires -Version 5.1
[CmdletBinding()]
param()

Write-Error @'
Deploy-Collector.ps1 has been retired by the WP-006.8 local-first revision.
Build a validated package with New-CollectorDeploymentPackage.ps1, transfer it
through an approved enterprise mechanism, then run Install-CollectorPackage.ps1
locally on the Collector server.
'@
exit 11
