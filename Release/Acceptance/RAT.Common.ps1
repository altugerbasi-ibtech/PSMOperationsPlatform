#requires -Version 5.1
Set-StrictMode -Version Latest

function Get-RATProductionReadinessDecision {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('PASS','WARNING','FAIL')]
        [string]$OverallResult
    )
    switch($OverallResult){
        'PASS' {
            [pscustomobject][ordered]@{
                Status='READY_FOR_PRODUCTION'
                Message='PSM Release Status: READY FOR PRODUCTION'
            }
        }
        'WARNING' {
            [pscustomobject][ordered]@{
                Status='READY_WITH_WARNINGS'
                Message='PSM Release Status: READY WITH WARNINGS'
            }
        }
        default {
            [pscustomobject][ordered]@{
                Status='NOT_READY_FOR_PRODUCTION'
                Message='PSM Release Status: NOT READY FOR PRODUCTION'
            }
        }
    }
}

function Get-RATOverallResult {
    param([Parameter(Mandatory)][object[]]$Checks)
    if(@($Checks|Where-Object{$_.Result -eq 'FAIL'}).Count){return 'FAIL'}
    if(@($Checks|Where-Object{$_.Result -eq 'WARNING'}).Count){return 'WARNING'}
    'PASS'
}

function Get-RATExitCode {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('PASS','WARNING','FAIL')]
        [string]$OverallResult
    )
    switch($OverallResult){'PASS'{0}'WARNING'{1}default{2}}
}

function New-RATReport {
    param(
        [Parameter(Mandatory)][object[]]$Checks,
        [Parameter(Mandatory)][string]$ProductVersion,
        [Parameter(Mandatory)][string]$GitCommit,
        [Parameter(Mandatory)][TimeSpan]$ExecutionTime,
        [Parameter(Mandatory)][bool]$ReadOnlyValidation
    )
    $normalized=New-Object System.Collections.Generic.List[object]
    foreach($check in $Checks){
        $result=[string]$check.Result
        if($result -notin @('PASS','WARNING','FAIL')){throw "Invalid RAT check result: $result"}
        $normalized.Add([pscustomobject][ordered]@{
            Name=[string]$check.Name
            Result=$result
            Diagnostics=if($check.PSObject.Properties.Name -contains 'Diagnostics'){
                [string]$check.Diagnostics
            }else{''}
        })
    }
    if(-not $ReadOnlyValidation){
        $normalized.Add([pscustomobject][ordered]@{
            Name='Read-only Validation'
            Result='FAIL'
            Diagnostics='Mandatory read-only validation evidence is absent.'
        })
    }
    $overall=Get-RATOverallResult $normalized.ToArray()
    $decision=Get-RATProductionReadinessDecision $overall
    if($overall -eq 'FAIL' -and $decision.Status -ne 'NOT_READY_FOR_PRODUCTION'){
        throw 'A FAIL result cannot produce a ready production decision.'
    }
    [pscustomobject][ordered]@{
        OverallResult=$overall
        ProductionReadinessStatus=$decision.Status
        ProductionReadinessMessage=$decision.Message
        ProductVersion=$ProductVersion
        GitCommit=$GitCommit
        ExecutionTime=$ExecutionTime.ToString('hh\:mm\:ss')
        ReadOnlyValidation=$ReadOnlyValidation
        Checks=$normalized.ToArray()
        ExitCode=Get-RATExitCode $overall
    }
}

function ConvertTo-RATMarkdown {
    param([Parameter(Mandatory)]$Report)
    $lines=New-Object System.Collections.Generic.List[string]
    $lines.Add('# Production Readiness Decision')
    $lines.Add('')
    $lines.Add("**$($Report.ProductionReadinessMessage)**")
    $lines.Add('')
    $lines.Add('# Release Acceptance Test')
    $lines.Add('')
    $lines.Add('| Check | Result | Diagnostics |')
    $lines.Add('|---|---|---|')
    foreach($check in $Report.Checks){
        $diagnostics=([string]$check.Diagnostics).Replace('|','\|')
        $lines.Add("| $($check.Name) | $($check.Result) | $diagnostics |")
    }
    $lines.Add('')
    $lines.Add('## Final Summary')
    $lines.Add('')
    $lines.Add("- Overall Result: **$($Report.OverallResult)**")
    $lines.Add("- $($Report.ProductionReadinessMessage)")
    $lines.Add("- Product Version: $($Report.ProductVersion)")
    $lines.Add("- Git Commit: $($Report.GitCommit)")
    $lines.Add("- Execution Time: $($Report.ExecutionTime)")
    $lines.Add("- Read-only Validation: $($Report.ReadOnlyValidation)")
    ($lines -join "`r`n")+"`r`n"
}

function ConvertTo-RATHtml {
    param([Parameter(Mandatory)]$Report)
    function Encode([string]$value){[Net.WebUtility]::HtmlEncode($value)}
    $rows=@($Report.Checks|ForEach-Object{
        "<tr><td>$(Encode $_.Name)</td><td>$(Encode $_.Result)</td><td>$(Encode $_.Diagnostics)</td></tr>"
    }) -join "`r`n"
    @"
<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>PSM Release Acceptance Test</title>
<style>body{font-family:Arial,sans-serif}table{border-collapse:collapse}th,td{border:1px solid #333;padding:.4rem}.decision{border:3px solid #000;padding:1rem;font-weight:bold}</style></head>
<body>
<header>
<h1>Production Readiness Decision</h1>
<p class="decision">$(Encode $Report.ProductionReadinessMessage)</p>
</header>
<main>
<h1>PSM Release Acceptance Test</h1>
<table><thead><tr><th>Check</th><th>Result</th><th>Diagnostics</th></tr></thead><tbody>
$rows
</tbody></table>
<section>
<h2>Final Summary</h2>
<p>Overall Result: <strong>$(Encode $Report.OverallResult)</strong></p>
<p><strong>$(Encode $Report.ProductionReadinessMessage)</strong></p>
<p>Product Version: $(Encode $Report.ProductVersion)</p>
<p>Git Commit: $(Encode $Report.GitCommit)</p>
<p>Execution Time: $(Encode $Report.ExecutionTime)</p>
<p>Read-only Validation: $(Encode ([string]$Report.ReadOnlyValidation))</p>
</section>
</main>
</body>
</html>
"@
}

function ConvertTo-RATJson {
    param([Parameter(Mandatory)]$Report)
    ($Report|ConvertTo-Json -Depth 6)+"`r`n"
}

function Write-RATConsole {
    param([Parameter(Mandatory)]$Report)
    Write-Output '===================================================='
    Write-Output 'PSM Operations Platform - Release Acceptance Test'
    Write-Output '===================================================='
    foreach($check in $Report.Checks){
        Write-Output ("{0,-34} {1}" -f $check.Name,$check.Result)
    }
    Write-Output '----------------------------------------------------'
    Write-Output "OVERALL RESULT: $($Report.OverallResult)"
    Write-Output "Product Version: $($Report.ProductVersion)"
    Write-Output "Git Commit: $($Report.GitCommit)"
    Write-Output "Execution Time: $($Report.ExecutionTime)"
    Write-Output '===================================================='
    Write-Output $Report.ProductionReadinessMessage
    Write-Output '===================================================='
}
