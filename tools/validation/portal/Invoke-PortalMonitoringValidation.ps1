#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ConfigurationPath,[Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputPath,[string]$PortalName,[switch]$SkipPortalChecks,[switch]$SkipMonitoringChecks,[switch]$SkipDatabaseChecks)
Set-StrictMode -Version Latest;$ErrorActionPreference='Stop';$repositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Common.ps1');. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Reporting.ps1');. (Join-Path $repositoryRoot 'tools\validation\iis\IISTarget.Checks.ps1');. (Join-Path $repositoryRoot 'tools\validation\sql\SQLTarget.Queries.ps1');. (Join-Path $repositoryRoot 'tools\validation\sql\SQLTarget.Checks.ps1');. (Join-Path $PSScriptRoot 'PortalMonitoring.Checks.ps1')
$started=[datetime]::UtcNow
try{
    try{$configuration=Get-OperationalConfiguration $ConfigurationPath $repositoryRoot;[void](Test-PortalMonitoringConfiguration $configuration $PortalName);if(-not $configuration.Portal.ValidationEnabled -and -not $PortalName){throw 'No enabled Portal is configured.'};$hash=Get-OperationalConfigurationHash $ConfigurationPath}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'PORTAL AND MONITORING VALIDATION: FAIL';exit 3}
    $operations=New-PortalMonitoringOperations $repositoryRoot;$results=Invoke-PortalMonitoringChecks $configuration $repositoryRoot $SkipPortalChecks.IsPresent $SkipMonitoringChecks.IsPresent $SkipDatabaseChecks.IsPresent $operations
    $report=New-OperationalReport $configuration $hash ([Environment]::MachineName) $configuration.Portal.Name $started $results 'Portal and Monitoring Validation Report' 'PortalMonitoringValidation';$paths=Write-OperationalReports $report $OutputPath
    Write-Output "JSON: $($paths.Json)";Write-Output "Markdown: $($paths.Markdown)";Write-Output "Log: $($paths.Log)";Write-Output "Checks: $(@($report.Results).Count)";Write-Output "PORTAL AND MONITORING VALIDATION: $($report.OverallResult)";exit $report.ExitCode
}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'PORTAL AND MONITORING VALIDATION: FAIL';exit 4}
