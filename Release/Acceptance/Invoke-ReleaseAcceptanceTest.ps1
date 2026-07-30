#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [string]$OutputDirectory=(Join-Path $PSScriptRoot 'Reports')
)

. (Join-Path $PSScriptRoot 'RAT.Common.ps1')

try{
    $input=Get-Content -Raw -LiteralPath $InputPath|ConvertFrom-Json
    if($null -eq $input.ReadOnlyValidation){throw 'ReadOnlyValidation is mandatory.'}
    $elapsed=[TimeSpan]::Parse([string]$input.ExecutionTime)
    $report=New-RATReport `
        -Checks @($input.Checks) `
        -ProductVersion ([string]$input.ProductVersion) `
        -GitCommit ([string]$input.GitCommit) `
        -ExecutionTime $elapsed `
        -ReadOnlyValidation ([bool]$input.ReadOnlyValidation)
    if(-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)){
        $null=New-Item -ItemType Directory -Path $OutputDirectory
    }
    [IO.File]::WriteAllText(
        (Join-Path $OutputDirectory 'RATReport.json'),
        (ConvertTo-RATJson $report),[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText(
        (Join-Path $OutputDirectory 'RATReport.md'),
        (ConvertTo-RATMarkdown $report),[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText(
        (Join-Path $OutputDirectory 'RATReport.html'),
        (ConvertTo-RATHtml $report),[Text.UTF8Encoding]::new($false))
    Write-RATConsole $report
    exit $report.ExitCode
}catch{
    Write-Error $_.Exception.Message
    exit 2
}
