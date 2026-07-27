Set-StrictMode -Version Latest

$script:ReadinessSchemaVersion = '1.0'
$script:ReadinessFrameworkName = 'PSM Collector Environment Validation'
$script:ReadinessFrameworkVersion = '1.0.0'
$script:ReadinessStatuses = @('PASS', 'WARNING', 'FAIL', 'SKIPPED', 'NOT_APPLICABLE')
$script:ReadinessSeverities = @('INFO', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL')
$script:ReadinessCategories = @(
    'CollectorHost', 'Runtime', 'CollectorFiles', 'Configuration', 'Service',
    'Identity', 'Network', 'SQL', 'WinRM'
)

function Protect-ReadinessText {
    [CmdletBinding()]
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Security.SecureString] -or
        $Value -is [System.Management.Automation.PSCredential]) {
        return '[REDACTED]'
    }

    $text = [string]$Value
    $patterns = @(
        '(?i)(Password|Pwd|AccessToken|ApiKey|API\s*Key|Secret)\s*=\s*[^;,\s]+',
        '(?i)(User\s*ID|UID)\s*=\s*[^;]+;\s*(Password|Pwd)\s*=\s*[^;]+',
        '(?i)(Server|Data Source)\s*=.+;\s*(Database|Initial Catalog)\s*=.+;\s*.+='
    )
    foreach ($pattern in $patterns) {
        if ($text -match $pattern) { return '[REDACTED]' }
    }
    return $text
}

function New-ReadinessCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[A-Z0-9]+(\.[A-Z0-9]+)+$')][string]$CheckId,
        [Parameter(Mandatory)][ValidateSet('CollectorHost','Runtime','CollectorFiles','Configuration','Service','Identity','Network','SQL','WinRM')][string]$Category,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name,
        [Parameter(Mandatory)][ValidateSet('PASS','WARNING','FAIL','SKIPPED','NOT_APPLICABLE')][string]$Status,
        [Parameter(Mandatory)][ValidateSet('INFO','LOW','MEDIUM','HIGH','CRITICAL')][string]$Severity,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Summary,
        [AllowNull()][object]$Evidence,
        [AllowNull()][string]$Recommendation,
        [Parameter(Mandatory)][bool]$IsBlocking,
        [Parameter(Mandatory)][bool]$IsMandatory,
        [Parameter(Mandatory)][long]$DurationMilliseconds
    )

    [pscustomobject][ordered]@{
        CheckId = $CheckId
        Category = $Category
        Name = $Name
        Status = $Status
        Severity = $Severity
        Summary = Protect-ReadinessText $Summary
        Evidence = Protect-ReadinessText $Evidence
        Recommendation = Protect-ReadinessText $Recommendation
        IsBlocking = $IsBlocking
        IsMandatory = $IsMandatory
        DurationMilliseconds = [math]::Max(0, $DurationMilliseconds)
    }
}

function Get-ReadinessStatus {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$Checks)

    if ($Checks.Count -eq 0) { return 'NOT_READY' }
    if ($Checks | Where-Object { $_.Status -eq 'FAIL' }) { return 'NOT_READY' }
    if ($Checks | Where-Object { $_.IsMandatory -and $_.Status -eq 'SKIPPED' }) {
        return 'NOT_READY'
    }
    if ($Checks | Where-Object { $_.Status -eq 'WARNING' }) { return 'WARNING' }
    if ($Checks | Where-Object {
            $_.IsMandatory -and $_.Status -ne 'PASS'
        }) { return 'NOT_READY' }
    return 'READY'
}

function Get-ReadinessExitCode {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateSet('READY','WARNING','NOT_READY')][string]$Status)
    switch ($Status) {
        'READY' { return 0 }
        'WARNING' { return 1 }
        default { return 2 }
    }
}

function Get-ReadinessCategories {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$Checks)

    @($script:ReadinessCategories | ForEach-Object {
        $name = $_
        $categoryChecks = @($Checks | Where-Object Category -eq $name)
        if ($categoryChecks.Count -eq 0) {
            [pscustomobject][ordered]@{ Name = $name; Status = 'NOT_APPLICABLE' }
        } else {
            [pscustomobject][ordered]@{
                Name = $name
                Status = Get-ReadinessStatus -Checks $categoryChecks
            }
        }
    })
}

function New-InternalErrorCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CheckId,
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][string]$Name
    )
    New-ReadinessCheck -CheckId $CheckId -Category $Category -Name $Name `
        -Status FAIL -Severity CRITICAL `
        -Summary 'The validation check encountered an internal error.' `
        -Evidence 'Exception details were suppressed.' `
        -Recommendation 'Review the local framework implementation and rerun the validation.' `
        -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0
}

function New-ReadinessManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Mode,
        [Parameter(Mandatory)][hashtable]$Context,
        [Parameter(Mandatory)][object[]]$Checks,
        [Parameter(Mandatory)][datetime]$GeneratedAt
    )
    $orderedChecks = @($Checks | Sort-Object Category, CheckId)
    $overall = Get-ReadinessStatus -Checks $orderedChecks
    [pscustomobject][ordered]@{
        SchemaVersion = $script:ReadinessSchemaVersion
        FrameworkName = $script:ReadinessFrameworkName
        FrameworkVersion = $script:ReadinessFrameworkVersion
        GeneratedAt = $GeneratedAt.ToString('yyyy-MM-ddTHH:mm:ss.fffK')
        GeneratedOnMachine = [Environment]::MachineName
        ExecutingIdentity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        OperatingSystem = [Environment]::OSVersion.VersionString
        Mode = $Mode
        CollectorVersion = $Context.CollectorVersion
        CollectorServiceName = $Context.CollectorServiceName
        CollectorInstallPath = $Context.CollectorInstallPath
        TargetFqdn = $Context.TargetFqdn
        TransportPolicy = $Context.TransportPolicy
        SqlServer = $Context.SqlServer
        DatabaseName = $Context.DatabaseName
        Categories = @(Get-ReadinessCategories -Checks $orderedChecks)
        OverallStatus = $overall
        ExitCode = Get-ReadinessExitCode -Status $overall
        Checks = $orderedChecks
    }
}

function ConvertTo-ReadinessMarkdown {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Manifest)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Collector Environment Readiness Report')
    $lines.Add('')
    $lines.Add('## Manifest')
    $lines.Add('')
    $lines.Add("| Field | Value |")
    $lines.Add("|---|---|")
    foreach ($field in @('SchemaVersion','FrameworkVersion','GeneratedAt','GeneratedOnMachine',
            'ExecutingIdentity','PowerShellVersion','OperatingSystem','Mode',
            'CollectorVersion','CollectorServiceName','CollectorInstallPath',
            'TargetFqdn','TransportPolicy','SqlServer','DatabaseName')) {
        $value = Protect-ReadinessText $Manifest.$field
        $lines.Add("| $field | $value |")
    }
    $lines.Add('')
    $lines.Add('## Overall Result')
    $lines.Add('')
    $lines.Add("**$($Manifest.OverallStatus)** (exit code $($Manifest.ExitCode))")
    $lines.Add('')
    $lines.Add('## Category Summary')
    $lines.Add('')
    $lines.Add('| Category | Status |')
    $lines.Add('|---|---|')
    foreach ($category in $Manifest.Categories) {
        $lines.Add("| $($category.Name) | $($category.Status) |")
    }
    $sections = @(
        [pscustomobject]@{ Title='Blocking Failures'; Checks=@($Manifest.Checks | Where-Object { $_.Status -eq 'FAIL' -and $_.IsBlocking }) },
        [pscustomobject]@{ Title='Warnings'; Checks=@($Manifest.Checks | Where-Object { $_.Status -eq 'WARNING' }) }
    )
    foreach ($section in $sections) {
        $lines.Add('')
        $lines.Add("## $($section.Title)")
        $lines.Add('')
        $selected = @($section.Checks)
        if ($selected.Count -eq 0) { $lines.Add('None') }
        else { foreach ($check in $selected) { $lines.Add("- $($check.CheckId): $($check.Summary)") } }
    }
    $lines.Add('')
    $lines.Add('## Detailed Checks')
    $lines.Add('')
    $lines.Add('| Check | Category | Status | Summary | Evidence |')
    $lines.Add('|---|---|---|---|---|')
    foreach ($check in $Manifest.Checks) {
        $summary = ([string]$check.Summary).Replace('|','\|')
        $evidence = ([string]$check.Evidence).Replace('|','\|')
        $lines.Add("| $($check.CheckId) | $($check.Category) | $($check.Status) | $summary | $evidence |")
    }
    $lines.Add('')
    $lines.Add('## Required Manual Actions')
    $lines.Add('')
    $actions = @($Manifest.Checks | Where-Object { $_.Status -in @('FAIL','WARNING') -and $_.Recommendation })
    if ($actions.Count -eq 0) { $lines.Add('None') }
    else { foreach ($check in $actions) { $lines.Add("- $($check.CheckId): $($check.Recommendation)") } }
    $lines.Add('')
    $lines.Add('## Security and Redaction Confirmation')
    $lines.Add('')
    $lines.Add('No credential, token, password, raw connection string, stack trace, or target-side artifact is included.')
    $lines.Add('')
    $lines.Add('## Execution Scope')
    $lines.Add('')
    $lines.Add('This report contains read-only validation results. The framework performs no remediation.')
    return ($lines -join [Environment]::NewLine)
}

function Write-ReadinessReports {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$OutputDirectory,
        [bool]$GenerateJson,
        [bool]$GenerateMarkdown
    )
    $result = [ordered]@{ JsonPath = $null; MarkdownPath = $null }
    if ($GenerateJson) {
        $result.JsonPath = Join-Path $OutputDirectory 'collector-readiness.json'
        $Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $result.JsonPath -Encoding UTF8
    }
    if ($GenerateMarkdown) {
        $result.MarkdownPath = Join-Path $OutputDirectory 'collector-readiness.md'
        ConvertTo-ReadinessMarkdown -Manifest $Manifest |
            Set-Content -LiteralPath $result.MarkdownPath -Encoding UTF8
    }
    [pscustomobject]$result
}
