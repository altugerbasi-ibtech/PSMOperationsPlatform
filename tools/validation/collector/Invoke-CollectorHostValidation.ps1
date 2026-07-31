#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ConfigurationPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputPath,
    [ValidateNotNullOrEmpty()][string]$ComputerName=[Environment]::MachineName,
    [switch]$SkipRemoteChecks
)

Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Common.ps1')
. (Join-Path $repositoryRoot 'tools\validation\common\OperationalValidation.Reporting.ps1')
. (Join-Path $PSScriptRoot 'CollectorHost.Checks.ps1')

$started=[datetime]::UtcNow
try {
    try {
        $configuration=Get-OperationalConfiguration -ConfigurationPath $ConfigurationPath -RepositoryRoot $repositoryRoot
        $configurationHash=Get-OperationalConfigurationHash $ConfigurationPath
    } catch {
        [Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message))
        Write-Output 'COLLECTOR HOST VALIDATION: FAIL'
        exit 3
    }

    $operations=New-CollectorValidationOperations
    $results=New-Object Collections.Generic.List[object]
    $groups=@(
        {Invoke-CollectorOperatingSystemChecks $configuration $ComputerName $operations},
        {Invoke-CollectorHardwareChecks $configuration $ComputerName $operations},
        {Invoke-CollectorPowerShellChecks $ComputerName $operations},
        {Invoke-CollectorDotNetChecks $ComputerName $operations},
        {Invoke-CollectorServiceChecks $ComputerName $operations},
        {Invoke-CollectorIdentityChecks $configuration $ComputerName $operations},
        {Invoke-CollectorWinRmChecks $configuration $ComputerName $SkipRemoteChecks.IsPresent $operations},
        {Invoke-CollectorNetworkChecks $configuration $ComputerName $SkipRemoteChecks.IsPresent $operations},
        {Invoke-CollectorSqlChecks $configuration $ComputerName $operations},
        {Invoke-CollectorFileSystemChecks $configuration $OutputPath $operations},
        {Invoke-CollectorLoggingChecks $configuration $OutputPath $ComputerName $operations},
        {Invoke-CollectorSecurityChecks $configuration $ConfigurationPath},
        {Invoke-CollectorReleaseArtifactChecks $configuration $repositoryRoot $ConfigurationPath}
    )
    $groupNumber=0
    foreach($group in $groups) {
        $groupNumber++
        try { foreach($result in @(& $group)){if($null -ne $result){$results.Add($result)}} }
        catch {
            $groupId=('FRAMEWORK.GROUP.{0:D2}.ERROR' -f $groupNumber)
            $results.Add((New-OperationalExceptionResult $groupId 'Framework' 'Validation group' `
                'Independent validation group execution.' $ComputerName $_.Exception CRITICAL $true))
        }
    }
    $report=New-OperationalReport -Configuration $configuration -ConfigurationHash $configurationHash `
        -SourceMachine ([Environment]::MachineName) -TargetMachine $ComputerName `
        -StartedAt $started -Results $results.ToArray()
    $paths=Write-OperationalReports -Report $report -OutputPath $OutputPath
    Write-Output "JSON: $($paths.Json)"
    Write-Output "Markdown: $($paths.Markdown)"
    Write-Output "Log: $($paths.Log)"
    Write-Output "Checks: $(@($report.Results).Count)"
    Write-Output "COLLECTOR HOST VALIDATION: $($report.OverallResult)"
    exit $report.ExitCode
} catch {
    [Console]::Error.WriteLine((Protect-OperationalText $_.Exception.Message))
    Write-Output 'COLLECTOR HOST VALIDATION: FAIL'
    exit 4
}
