#requires -Version 5.1
Set-StrictMode -Version Latest

function ConvertTo-OperationalMarkdown {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Report)
    $lines = New-Object Collections.Generic.List[string]
    $title = if ($Report.PSObject.Properties['ReportTitle']) { $Report.ReportTitle } else { 'Collector Host Validation Report' }
    $lines.Add("# $title")
    $lines.Add('')
    $lines.Add("**Overall result: $($Report.OverallResult)**")
    if ($Report.PSObject.Properties['ReadinessDecision']) {
        $lines.Add('')
        $lines.Add("**Readiness decision: $($Report.ReadinessDecision)**")
        $lines.Add('')
        $lines.Add('## Blocked Execution Reasons')
        $lines.Add('')
        if (@($Report.BlockedExecutionReasons).Count -eq 0) { $lines.Add('None') }
        else { foreach ($reason in $Report.BlockedExecutionReasons) { $lines.Add("- $reason") } }
    }
    $lines.Add('')
    $lines.Add('| Field | Value |')
    $lines.Add('|---|---|')
    foreach ($name in @('Timestamp','ProductVersion','GitCommit','SourceMachine','TargetMachine',
            'CurrentIdentity','ConfigurationHash','DurationMilliseconds')) {
        $lines.Add("| $name | $($Report.$name) |")
    }
    $lines.Add('')
    $lines.Add('## Target Summary')
    $lines.Add('')
    $lines.Add('| Target | Result |')
    $lines.Add('|---|---|')
    if ($Report.PSObject.Properties['Targets']) {
        foreach ($target in $Report.Targets) { $lines.Add("| $($target.Target) | $($target.Status) |") }
    }
    $lines.Add('')
    $lines.Add('## Category Summary')
    $lines.Add('')
    $lines.Add('| Category | Result |')
    $lines.Add('|---|---|')
    foreach ($category in $Report.Categories) { $lines.Add("| $($category.Category) | $($category.Status) |") }
    $lines.Add('')
    $lines.Add('## Checks')
    $lines.Add('')
    $lines.Add('| Check | Category | Status | Severity | Message |')
    $lines.Add('|---|---|---|---|---|')
    foreach ($check in $Report.Results) {
        $message = ([string]$check.Message).Replace('|','\|')
        $lines.Add("| $($check.CheckId) | $($check.Category) | $($check.Status) | $($check.Severity) | $message |")
    }
    $lines.Add('')
    $lines.Add('## Recommendations')
    $lines.Add('')
    $recommendations = @($Report.Results | Where-Object Recommendation)
    if ($recommendations.Count -eq 0) { $lines.Add('None') }
    else { foreach ($check in $recommendations) { $lines.Add("- $($check.CheckId): $($check.Recommendation)") } }
    $lines.Add('')
    $lines.Add('## Skipped and Not Applicable')
    $lines.Add('')
    $skipped = @($Report.Results | Where-Object Status -in @('SKIPPED','NOT_APPLICABLE'))
    if ($skipped.Count -eq 0) { $lines.Add('None') }
    else { foreach ($check in $skipped) { $lines.Add("- $($check.CheckId): $($check.Message)") } }
    return ($lines -join [Environment]::NewLine)
}

function New-OperationalReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Configuration,
        [Parameter(Mandatory)][string]$ConfigurationHash,
        [Parameter(Mandatory)][string]$SourceMachine,
        [Parameter(Mandatory)][string]$TargetMachine,
        [Parameter(Mandatory)][datetime]$StartedAt,
        [Parameter(Mandatory)][object[]]$Results,
        [string]$ReportTitle = 'Collector Host Validation Report',
        [string]$ReportBaseName = 'CollectorHostValidation'
    )
    $ordered = @($Results | Sort-Object Category,CheckId)
    Assert-OperationalResults $ordered
    $overall = Get-OperationalOverallStatus $ordered
    $categories = @($ordered | Group-Object Category | Sort-Object Name | ForEach-Object {
        [pscustomobject][ordered]@{
            Category=$_.Name
            Status=Get-OperationalOverallStatus @($_.Group)
        }
    })
    $targets = @($ordered | Group-Object Target | Sort-Object Name | ForEach-Object {
        [pscustomobject][ordered]@{
            Target=$_.Name
            Status=Get-OperationalOverallStatus @($_.Group)
        }
    })
    [pscustomobject][ordered]@{
        ReportTitle=$ReportTitle
        ReportBaseName=$ReportBaseName
        Timestamp=[datetime]::UtcNow.ToString('o')
        ProductVersion=$Configuration.Deployment.ProductVersion
        GitCommit=$Configuration.Deployment.GitCommit
        SourceMachine=$SourceMachine
        TargetMachine=$TargetMachine
        CurrentIdentity=[Security.Principal.WindowsIdentity]::GetCurrent().Name
        ConfigurationHash=$ConfigurationHash
        DurationMilliseconds=[math]::Max(0,[long]([datetime]::UtcNow-$StartedAt).TotalMilliseconds)
        Targets=$targets
        Categories=$categories
        OverallResult=$overall
        ExitCode=Get-OperationalExitCode $overall
        Results=$ordered
    }
}

function Write-OperationalReports {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Report,[Parameter(Mandatory)][string]$OutputPath)
    if (-not (Test-Path -LiteralPath $OutputPath -PathType Container)) {
        [void][IO.Directory]::CreateDirectory($OutputPath)
    }
    $baseName = if ($Report.PSObject.Properties['ReportBaseName']) { $Report.ReportBaseName } else { 'CollectorHostValidation' }
    $jsonPath = Join-Path $OutputPath ($baseName + 'Report.json')
    $markdownPath = Join-Path $OutputPath ($baseName + 'Report.md')
    $logPath = Join-Path $OutputPath ($baseName + '.log')
    $Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
    ConvertTo-OperationalMarkdown $Report | Set-Content -LiteralPath $markdownPath -Encoding UTF8
    @(
        "Timestamp=$($Report.Timestamp)"
        "Target=$($Report.TargetMachine)"
        "OverallResult=$($Report.OverallResult)"
        "CheckCount=$(@($Report.Results).Count)"
        $(if ($Report.PSObject.Properties['ReadinessDecision']) { "ReadinessDecision=$($Report.ReadinessDecision)" })
    ) | Set-Content -LiteralPath $logPath -Encoding UTF8
    [pscustomobject]@{Json=$jsonPath;Markdown=$markdownPath;Log=$logPath}
}
