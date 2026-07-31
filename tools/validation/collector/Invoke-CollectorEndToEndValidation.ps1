#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ConfigurationPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputPath,
    [ValidateNotNullOrEmpty()][string]$TargetCollector,
    [switch]$SkipRemoteChecks
)
Set-StrictMode -Version Latest;$ErrorActionPreference='Stop'
$script:EndToEndRepositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$script:EndToEndConfigurationPath=$ConfigurationPath
. (Join-Path $script:EndToEndRepositoryRoot 'tools\validation\common\OperationalValidation.Common.ps1')
. (Join-Path $script:EndToEndRepositoryRoot 'tools\validation\common\OperationalValidation.Reporting.ps1')
. (Join-Path $PSScriptRoot 'CollectorHost.Checks.ps1')
. (Join-Path $script:EndToEndRepositoryRoot 'tools\validation\iis\IISTarget.Checks.ps1')
. (Join-Path $script:EndToEndRepositoryRoot 'tools\validation\sql\SQLTarget.Queries.ps1')
. (Join-Path $script:EndToEndRepositoryRoot 'tools\validation\sql\SQLTarget.Checks.ps1')
. (Join-Path $PSScriptRoot 'CollectorEndToEnd.Checks.ps1')
$started=[datetime]::UtcNow
try{
    try{$configuration=Get-OperationalConfiguration $ConfigurationPath $script:EndToEndRepositoryRoot;$hash=Get-OperationalConfigurationHash $ConfigurationPath;if(-not $PSBoundParameters.ContainsKey('TargetCollector')){$TargetCollector=$configuration.Collector.Server};[void](Test-CollectorEndToEndConfiguration $configuration $TargetCollector)}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'COLLECTOR NOT READY';exit 3}
    $operations=New-CollectorEndToEndOperations $SkipRemoteChecks.IsPresent;$outcome=Invoke-CollectorEndToEndChecks $configuration $ConfigurationPath $OutputPath $TargetCollector $SkipRemoteChecks.IsPresent $operations
    $report=New-OperationalReport $configuration $hash ([Environment]::MachineName) $TargetCollector $started $outcome.Results 'Collector End-to-End Validation Report' 'CollectorEndToEndValidation';$report|Add-Member ReadinessDecision $outcome.ReadinessDecision;$report|Add-Member BlockedExecutionReasons @($outcome.BlockedExecutionReasons);$paths=Write-OperationalReports $report $OutputPath
    Write-Output "JSON: $($paths.Json)";Write-Output "Markdown: $($paths.Markdown)";Write-Output "Log: $($paths.Log)";Write-Output "Checks: $(@($report.Results).Count)";Write-Output $outcome.ReadinessDecision
    if($outcome.ReadinessDecision -eq 'COLLECTOR READY'){exit 0};if($outcome.ReadinessDecision -eq 'COLLECTOR READY WITH WARNINGS'){exit 1};exit 2
}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'COLLECTOR NOT READY';exit 4}
