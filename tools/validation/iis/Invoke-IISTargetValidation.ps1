#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ConfigurationPath,
    [ValidateNotNullOrEmpty()][string]$ComputerName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Common.ps1')
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Reporting.ps1')
. (Join-Path $PSScriptRoot 'IISTarget.Checks.ps1')

$started=[datetime]::UtcNow
try {
    try {
        $configuration=Get-OperationalConfiguration -ConfigurationPath $ConfigurationPath -RepositoryRoot $repositoryRoot
        $configurationHash=Get-OperationalConfigurationHash $ConfigurationPath
        $configured=@($configuration.IisTargets|Sort-Object { $_.ToUpperInvariant() },{ $_ })
        if($PSBoundParameters.ContainsKey('ComputerName')){
            $matches=@($configured|Where-Object{[string]::Equals($_,$ComputerName,[StringComparison]::OrdinalIgnoreCase)})
            if($matches.Count -ne 1){throw 'ComputerName must identify exactly one configured IIS target.'}
            $targets=$matches
        } else {$targets=$configured}
    } catch {
        [Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message))
        Write-Output 'IIS TARGET VALIDATION: FAIL'
        exit 3
    }
    $operations=New-IisValidationOperations
    $results=New-Object Collections.Generic.List[object]
    for($index=0;$index -lt $targets.Count;$index++){
        try{foreach($result in @(Invoke-IisTargetChecks $configuration $targets[$index] ($index+1) $operations)){$results.Add($result)}}
        catch{$results.Add((New-OperationalExceptionResult ('T{0:D3}.FRAMEWORK.ERROR' -f ($index+1)) Framework 'Target validation' 'Independent IIS target validation.' $targets[$index] $_.Exception CRITICAL $true))}
    }
    $report=New-OperationalReport -Configuration $configuration -ConfigurationHash $configurationHash `
        -SourceMachine ([Environment]::MachineName) -TargetMachine ($targets -join ', ') `
        -StartedAt $started -Results $results.ToArray() -ReportTitle 'IIS Target Validation Report' `
        -ReportBaseName 'IISTargetValidation'
    $paths=Write-OperationalReports -Report $report -OutputPath $OutputPath
    Write-Output "JSON: $($paths.Json)"
    Write-Output "Markdown: $($paths.Markdown)"
    Write-Output "Log: $($paths.Log)"
    Write-Output "Targets: $($targets.Count)"
    Write-Output "Checks: $(@($report.Results).Count)"
    Write-Output "IIS TARGET VALIDATION: $($report.OverallResult)"
    exit $report.ExitCode
} catch {
    [Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message))
    Write-Output 'IIS TARGET VALIDATION: FAIL'
    exit 4
}
