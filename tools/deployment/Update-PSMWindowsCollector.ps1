#requires -Version 5.1
[CmdletBinding()]
param()
Write-Error @'
Update-PSMWindowsCollector.ps1 is retired by WP-006.8.
Use scripts\deployment\Install-CollectorPackage.ps1 locally.
'@
exit 12
