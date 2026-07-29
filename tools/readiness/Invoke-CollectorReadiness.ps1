[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('CollectorHost','SmokeTest')][string]$Mode,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorInstallPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorServiceName,
    [ValidateNotNullOrEmpty()][string]$TargetFqdn,
    [ValidateSet('Auto','HttpsOnly','HttpOnly')][string]$TransportPolicy,
    [ValidateRange(1,65535)][int]$WinRmHttpsPort = 5986,
    [ValidateRange(1,65535)][int]$WinRmHttpPort = 5985,
    [ValidateNotNullOrEmpty()][string]$SqlServer,
    [ValidateRange(1,65535)][int]$SqlPort = 1433,
    [ValidateNotNullOrEmpty()][string]$DatabaseName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ExpectedServiceAccount,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputDirectory,
    [switch]$SkipSqlAuthenticationTest,
    [switch]$SkipWinRmAuthenticationTest,
    [bool]$GenerateMarkdown = $true,
    [bool]$GenerateJson = $true
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
@(
    'Readiness.Common.ps1', 'CollectorHostValidation.ps1',
    'DotNetValidation.ps1', 'CollectorFilesValidation.ps1',
    'ConfigurationValidation.ps1', 'ServiceValidation.ps1',
    'GmsaValidation.ps1', 'NetworkValidation.ps1',
    'SqlValidation.ps1', 'WinRmValidation.ps1'
) | ForEach-Object { . (Join-Path $scriptRoot $_) }

if (-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)) {
    Write-Output '[FAIL] CollectorHost - Output directory does not exist; no directory was created.'
    Write-Output 'Overall: NOT_READY'
    Write-Output 'Exit Code: 2'
    exit 2
}

$parameters = @{
    Mode = $Mode
    CollectorInstallPath = $CollectorInstallPath
    CollectorServiceName = $CollectorServiceName
    TargetFqdn = $TargetFqdn
    TransportPolicy = $TransportPolicy
    WinRmHttpsPort = $WinRmHttpsPort
    WinRmHttpPort = $WinRmHttpPort
    SqlServer = $SqlServer
    SqlPort = $SqlPort
    DatabaseName = $DatabaseName
    ExpectedServiceAccount = $ExpectedServiceAccount
    SkipSqlAuthenticationTest = [bool]$SkipSqlAuthenticationTest
    SkipWinRmAuthenticationTest = [bool]$SkipWinRmAuthenticationTest
}

$checks = New-Object System.Collections.Generic.List[object]
$validators = @(
    @{ Command='Test-CollectorHostReadiness'; Id='HOST.INTERNAL.ERROR'; Category='CollectorHost' },
    @{ Command='Test-DotNetReadiness'; Id='DOTNET.INTERNAL.ERROR'; Category='Runtime' },
    @{ Command='Test-CollectorFilesReadiness'; Id='FILES.INTERNAL.ERROR'; Category='CollectorFiles' },
    @{ Command='Test-ConfigurationReadiness'; Id='CONFIG.INTERNAL.ERROR'; Category='Configuration' },
    @{ Command='Test-ServiceReadiness'; Id='SERVICE.INTERNAL.ERROR'; Category='Service' },
    @{ Command='Test-IdentityReadiness'; Id='IDENTITY.INTERNAL.ERROR'; Category='Identity' }
)
if ($Mode -eq 'SmokeTest') {
    $validators += @(
        @{ Command='Test-NetworkReadiness'; Id='NETWORK.INTERNAL.ERROR'; Category='Network' },
        @{ Command='Test-WinRmReadiness'; Id='WINRM.INTERNAL.ERROR'; Category='WinRM' },
        @{ Command='Test-SqlReadiness'; Id='SQL.INTERNAL.ERROR'; Category='SQL' }
    )
}
foreach ($validator in $validators) {
    try {
        $commandName = $validator.Command
        foreach ($check in @(& $commandName -Parameters $parameters)) { $checks.Add($check) }
    } catch {
        $checks.Add((New-InternalErrorCheck -CheckId $validator.Id `
            -Category $validator.Category -Name "$($validator.Category) validation"))
    }
}

$collectorVersion = $null
$versionCheck = $checks | Where-Object CheckId -eq 'FILES.VERSION' | Select-Object -First 1
if ($versionCheck -and $versionCheck.Status -eq 'PASS') { $collectorVersion = $versionCheck.Evidence }
$context = @{
    CollectorVersion = $collectorVersion
    CollectorServiceName = $CollectorServiceName
    CollectorInstallPath = $CollectorInstallPath
    TargetFqdn = $TargetFqdn
    TransportPolicy = $TransportPolicy
    SqlServer = $SqlServer
    DatabaseName = $DatabaseName
}
$completion = Complete-ReadinessRun -Mode $Mode -Context $context `
    -Checks $checks -GeneratedAt (Get-Date) -OutputDirectory $OutputDirectory `
    -GenerateJson $GenerateJson -GenerateMarkdown $GenerateMarkdown
foreach ($check in $completion.Checks) {
    Write-Output "[$($check.Status)] $($check.Category) - $($check.Summary)"
}
Write-Output "Overall: $($completion.OverallStatus)"
if ($completion.Paths -and $completion.Paths.JsonPath) {
    Write-Output "JSON: $($completion.Paths.JsonPath)"
}
if ($completion.Paths -and $completion.Paths.MarkdownPath) {
    Write-Output "Markdown: $($completion.Paths.MarkdownPath)"
}
Write-Output "Exit Code: $($completion.ExitCode)"
exit $completion.ExitCode
