#requires -Version 5.1
[CmdletBinding()]
param()
Write-Error @'
Install-PSMWindowsCollector.ps1 is retired by WP-006.8.
Fresh service creation is unsupported. Create a validated package with
scripts\deployment\New-CollectorDeploymentPackage.ps1 and run
scripts\deployment\Install-CollectorPackage.ps1 locally for an existing service.
'@
exit 12
