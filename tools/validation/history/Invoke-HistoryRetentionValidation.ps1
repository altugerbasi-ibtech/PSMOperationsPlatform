#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ConfigurationPath,[Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputPath,[string]$DatabaseTarget,[switch]$SkipSchemaChecks,[switch]$SkipProjectionChecks,[switch]$SkipQueryChecks,[switch]$SkipRetentionChecks)
Set-StrictMode -Version Latest;$ErrorActionPreference='Stop';$repositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Common.ps1');. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Reporting.ps1');. (Join-Path $repositoryRoot 'tools\validation\sql\SQLTarget.Queries.ps1');. (Join-Path $repositoryRoot 'tools\validation\sql\SQLTarget.Checks.ps1');. (Join-Path $PSScriptRoot 'HistoryRetention.Queries.ps1');. (Join-Path $PSScriptRoot 'HistoryRetention.Checks.ps1')
$started=[datetime]::UtcNow
try{
    try{$configuration=Get-OperationalConfiguration $ConfigurationPath $repositoryRoot;$target=Test-HistoryRetentionConfiguration $configuration $DatabaseTarget;$hash=Get-OperationalConfigurationHash $ConfigurationPath}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'HISTORY AND RETENTION VALIDATION: FAIL';exit 3}
    $results=Invoke-HistoryRetentionChecks $configuration $target $repositoryRoot $SkipSchemaChecks.IsPresent $SkipProjectionChecks.IsPresent $SkipQueryChecks.IsPresent $SkipRetentionChecks.IsPresent (New-HistoryRetentionOperations $repositoryRoot)
    $report=New-OperationalReport $configuration $hash ([Environment]::MachineName) $target.Name $started $results 'History and Retention Validation Report' 'HistoryRetentionValidation';$report|Add-Member LiveHistoryValidation 'NOT EXECUTED';$report|Add-Member LiveRetentionValidation 'NOT EXECUTED';$paths=Write-OperationalReports $report $OutputPath
    Write-Output "JSON: $($paths.Json)";Write-Output "Markdown: $($paths.Markdown)";Write-Output "Log: $($paths.Log)";Write-Output "Checks: $(@($report.Results).Count)";Write-Output "HISTORY AND RETENTION VALIDATION: $($report.OverallResult)";exit $report.ExitCode
}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'HISTORY AND RETENTION VALIDATION: FAIL';exit 4}
