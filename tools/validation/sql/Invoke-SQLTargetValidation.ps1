#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ConfigurationPath,
    [ValidateNotNullOrEmpty()][string]$ComputerName=[Environment]::MachineName,
    [ValidateNotNullOrEmpty()][string]$TargetName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputPath,
    [switch]$SkipDatabaseChecks,
    [switch]$SkipPermissionChecks
)
Set-StrictMode -Version Latest;$ErrorActionPreference='Stop'
$repositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Common.ps1')
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Reporting.ps1')
. (Join-Path $PSScriptRoot 'SQLTarget.Queries.ps1')
. (Join-Path $PSScriptRoot 'SQLTarget.Checks.ps1')
$started=[datetime]::UtcNow
try{
    try{$configuration=Get-OperationalConfiguration $ConfigurationPath $repositoryRoot;$hash=Get-OperationalConfigurationHash $ConfigurationPath;$configured=@($configuration.SqlTargets|Sort-Object{$_.Name.ToUpperInvariant()},{$_.Name});if($PSBoundParameters.ContainsKey('TargetName')){$matches=@($configured|Where-Object{[string]::Equals($_.Name,$TargetName,[StringComparison]::OrdinalIgnoreCase)});if($matches.Count -ne 1 -or -not $matches[0].ValidationEnabled){throw 'TargetName must identify exactly one enabled configured SQL target.'};$targets=$matches}else{$targets=@($configured|Where-Object ValidationEnabled)}}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'SQL TARGET VALIDATION: FAIL';exit 3}
    $operations=New-SqlTargetValidationOperations;$results=New-Object Collections.Generic.List[object]
    for($index=0;$index -lt $targets.Count;$index++){try{foreach($result in @(Invoke-SqlTargetChecks $configuration $targets[$index] ($index+1) $ComputerName $SkipDatabaseChecks.IsPresent $SkipPermissionChecks.IsPresent $operations)){$results.Add($result)}}catch{$results.Add((New-OperationalExceptionResult ('T{0:D3}.FRAMEWORK.ERROR' -f ($index+1)) Framework 'SQL target validation' 'Independent SQL target validation.' $targets[$index].Name $_.Exception CRITICAL $true))}}
    $report=New-OperationalReport $configuration $hash $ComputerName (($targets|ForEach-Object Name)-join ', ') $started $results.ToArray() 'SQL Target Validation Report' 'SQLTargetValidation';$paths=Write-OperationalReports $report $OutputPath
    Write-Output "JSON: $($paths.Json)";Write-Output "Markdown: $($paths.Markdown)";Write-Output "Log: $($paths.Log)";Write-Output "Targets: $($targets.Count)";Write-Output "Checks: $(@($report.Results).Count)";Write-Output "SQL TARGET VALIDATION: $($report.OverallResult)";exit $report.ExitCode
}catch{[Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message));Write-Output 'SQL TARGET VALIDATION: FAIL';exit 4}
