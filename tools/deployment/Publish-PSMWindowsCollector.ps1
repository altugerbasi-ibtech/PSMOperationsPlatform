#requires -Version 5.1
[CmdletBinding()]
param()
Write-Error @'
Publish-PSMWindowsCollector.ps1 is retired by WP-006.8.
Use scripts\deployment\New-CollectorDeploymentPackage.ps1.
'@
exit 12
