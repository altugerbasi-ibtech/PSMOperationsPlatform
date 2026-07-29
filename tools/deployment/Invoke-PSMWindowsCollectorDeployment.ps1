#requires -Version 5.1
[CmdletBinding()]
param()
Write-Error @'
Invoke-PSMWindowsCollectorDeployment.ps1 is retired by WP-006.8.
Remote orchestration is unsupported. Transfer the validated package by an
operator-approved method and run Install-CollectorPackage.ps1 locally.
'@
exit 12
