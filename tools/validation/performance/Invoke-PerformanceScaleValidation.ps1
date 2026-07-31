#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ConfigurationPath,[Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputPath,[ValidateSet('Smoke','Standard','Extended')][string]$ValidationProfile,[switch]$SkipRuntimeChecks,[switch]$SkipMonitoringChecks,[switch]$SkipHistoryChecks,[switch]$SkipDatabaseChecks,[switch]$SkipToolingChecks,[switch]$EnableInformationalBenchmarks)
Set-StrictMode -Version Latest;$ErrorActionPreference='Stop';$repositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Common.ps1');. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Reporting.ps1');. (Join-Path $PSScriptRoot 'PerformanceScale.Profiles.ps1');. (Join-Path $PSScriptRoot 'PerformanceScale.Checks.ps1')
$started=[datetime]::UtcNow
try{
    try{$configuration=Get-OperationalConfiguration $ConfigurationPath $repositoryRoot;$profile=Test-PerformanceValidationConfiguration $configuration $ValidationProfile;$hash=Get-OperationalConfigurationHash $ConfigurationPath}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'PERFORMANCE AND SCALE VALIDATION: FAIL';exit 3}
    $execution=Invoke-PerformanceScaleChecks $configuration $profile $repositoryRoot $SkipRuntimeChecks.IsPresent $SkipMonitoringChecks.IsPresent $SkipHistoryChecks.IsPresent $SkipDatabaseChecks.IsPresent $SkipToolingChecks.IsPresent $EnableInformationalBenchmarks.IsPresent $null
    $report=New-OperationalReport $configuration $hash 'repository-machine-category' $profile.Name $started $execution.Results 'Performance and Scale Validation Report' 'PerformanceScaleValidation'
    $report|Add-Member ValidationProfile $profile; $report|Add-Member ScaleMatrix $execution.ScaleMatrix; $report|Add-Member LivePerformanceValidation 'NOT EXECUTED';$report|Add-Member ProductionScaleReadiness 'NOT PROVEN'
    $paths=Write-OperationalReports $report $OutputPath
    Write-Output "JSON: $($paths.Json)";Write-Output "Markdown: $($paths.Markdown)";Write-Output "Log: $($paths.Log)";Write-Output "Checks: $(@($report.Results).Count)";Write-Output "PERFORMANCE AND SCALE VALIDATION: $($report.OverallResult)";exit $report.ExitCode
}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'PERFORMANCE AND SCALE VALIDATION: FAIL';exit 4}
