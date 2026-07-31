#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Path
)

$ErrorActionPreference = 'Continue'
$diagnostics = [Collections.Generic.List[string]]::new()

function Add-Diagnostic([string]$message) {
    $diagnostics.Add($message)
}

function Test-RequiredObject($parent, [string]$name) {
    if ($null -eq $parent -or $null -eq $parent.PSObject.Properties[$name] -or
        $null -eq $parent.$name -or $parent.$name -isnot [pscustomobject]) {
        Add-Diagnostic "Missing or invalid required object: $name"
        return $false
    }
    return $true
}

function Test-AllowedProperties($parent, [string]$path, [string[]]$allowed) {
    if ($null -eq $parent) { return }
    foreach ($property in $parent.PSObject.Properties.Name) {
        if ($property -notin $allowed) {
            Add-Diagnostic "Schema violation: unknown property $path.$property."
        }
    }
}

function Test-RequiredString($parent, [string]$path, [string]$name, [string]$pattern = '') {
    if ($null -eq $parent -or $null -eq $parent.PSObject.Properties[$name] -or
        $parent.$name -isnot [string] -or [string]::IsNullOrWhiteSpace($parent.$name)) {
        Add-Diagnostic "Missing or empty required value: $path.$name"
        return
    }
    if ($pattern -and $parent.$name -notmatch $pattern) {
        Add-Diagnostic "Schema violation: $path.$name has an invalid format."
    }
}

function Test-Boolean($parent, [string]$path, [string]$name) {
    if ($null -eq $parent -or $null -eq $parent.PSObject.Properties[$name] -or
        $parent.$name -isnot [bool]) {
        Add-Diagnostic "Schema violation: $path.$name must be a Boolean."
    }
}

function Test-Port($value, [string]$path) {
    if ($value -isnot [int] -or $value -lt 1 -or $value -gt 65535) {
        Add-Diagnostic "Invalid port: $path must be an integer from 1 through 65535."
    }
}

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    Write-Error "Configuration file was not found: $Path"
    exit 1
}

try {
    $configuration = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop |
        ConvertFrom-Json -ErrorAction Stop
} catch {
    Write-Error 'Deployment configuration contains invalid JSON.'
    exit 1
}

$sectionNames = @('Deployment','SqlServer','Collector','Portal','SqlCollector','Security','Validation')
Test-AllowedProperties $configuration '$' $sectionNames
foreach ($sectionName in $sectionNames) {
    [void](Test-RequiredObject $configuration $sectionName)
}

Test-AllowedProperties $configuration.Deployment 'Deployment' @(
    'EnvironmentName','ProductVersion','ReleaseVersion','GitCommit')
Test-RequiredString $configuration.Deployment 'Deployment' 'EnvironmentName' '^[A-Za-z0-9][A-Za-z0-9._-]*$'
Test-RequiredString $configuration.Deployment 'Deployment' 'ProductVersion' '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$'
Test-RequiredString $configuration.Deployment 'Deployment' 'ReleaseVersion' '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$'
Test-RequiredString $configuration.Deployment 'Deployment' 'GitCommit' '^[0-9A-Fa-f]{40}$'

Test-AllowedProperties $configuration.SqlServer 'SqlServer' @(
    'Server','Instance','Port','Database','DataPath','LogPath',
    'CompatibilityLevel','RecoveryModel','Collation')
foreach ($name in @('Server','Instance','Database','DataPath','LogPath','Collation')) {
    Test-RequiredString $configuration.SqlServer 'SqlServer' $name
}
if ($configuration.SqlServer.Database -is [string] -and
    $configuration.SqlServer.Database -notmatch '^[A-Za-z0-9_.-]+$') {
    Add-Diagnostic 'Schema violation: SqlServer.Database has an invalid format.'
}
if ($configuration.SqlServer.Collation -is [string] -and
    $configuration.SqlServer.Collation -notmatch '^[A-Za-z0-9_]+$') {
    Add-Diagnostic 'Schema violation: SqlServer.Collation has an invalid format.'
}
Test-Port $configuration.SqlServer.Port 'SqlServer.Port'
if ($configuration.SqlServer.CompatibilityLevel -isnot [int] -or
    $configuration.SqlServer.CompatibilityLevel -ne 160) {
    Add-Diagnostic 'Schema violation: SqlServer.CompatibilityLevel must be 160.'
}
if ($configuration.SqlServer.RecoveryModel -notin @('SIMPLE','FULL','BULK_LOGGED')) {
    Add-Diagnostic 'Schema violation: SqlServer.RecoveryModel is not allowed.'
}

Test-AllowedProperties $configuration.Collector 'Collector' @('Server','ServiceAccount','LogPath')
Test-AllowedProperties $configuration.Portal 'Portal' @('Server','ServiceAccount')
Test-AllowedProperties $configuration.SqlCollector 'SqlCollector' @('Server','ServiceAccount')
Test-RequiredString $configuration.Collector 'Collector' 'Server'
Test-RequiredString $configuration.Collector 'Collector' 'ServiceAccount' '^[^\s]+\\[^\s]+\$$'
Test-RequiredString $configuration.Collector 'Collector' 'LogPath'
Test-RequiredString $configuration.Portal 'Portal' 'Server'
Test-RequiredString $configuration.Portal 'Portal' 'ServiceAccount' '^[^\s]+\\[^\s]+\$$'
Test-RequiredString $configuration.SqlCollector 'SqlCollector' 'Server'
Test-RequiredString $configuration.SqlCollector 'SqlCollector' 'ServiceAccount' '^[^\s]+\\[^\s]+\$$'

Test-AllowedProperties $configuration.Security 'Security' @(
    'WindowsAuthentication','KerberosOnly','WinRMPort','IncludePortInSPN','UseTLS')
foreach ($name in @('WindowsAuthentication','KerberosOnly','IncludePortInSPN','UseTLS')) {
    Test-Boolean $configuration.Security 'Security' $name
}
if ($configuration.Security.WindowsAuthentication -is [bool] -and
    -not $configuration.Security.WindowsAuthentication) {
    Add-Diagnostic 'Schema violation: Security.WindowsAuthentication must be true.'
}
Test-Port $configuration.Security.WinRMPort 'Security.WinRMPort'

Test-AllowedProperties $configuration.Validation 'Validation' @(
    'RunSchemaValidation','RunPermissionValidation','RunReleaseAcceptanceTest')
foreach ($name in @('RunSchemaValidation','RunPermissionValidation','RunReleaseAcceptanceTest')) {
    Test-Boolean $configuration.Validation 'Validation' $name
}

$servers = @(
    @{ Role = 'SqlServer'; Value = $configuration.SqlServer.Server },
    @{ Role = 'Collector'; Value = $configuration.Collector.Server },
    @{ Role = 'Portal'; Value = $configuration.Portal.Server },
    @{ Role = 'SqlCollector'; Value = $configuration.SqlCollector.Server }
)
$duplicates = $servers |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_.Value) } |
    Group-Object { $_.Value.Trim().ToUpperInvariant() } |
    Where-Object Count -gt 1
foreach ($duplicate in $duplicates) {
    $roles = ($duplicate.Group | ForEach-Object Role) -join ', '
    Add-Diagnostic "Duplicate server definition is not allowed across roles: $roles."
}

$accounts = @(
    $configuration.Collector.ServiceAccount,
    $configuration.Portal.ServiceAccount,
    $configuration.SqlCollector.ServiceAccount
)
if (@($accounts | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Group-Object { $_.Trim().ToUpperInvariant() } | Where-Object Count -gt 1).Count -gt 0) {
    Add-Diagnostic 'Runtime service account names must be distinct.'
}

if ($diagnostics.Count -gt 0) {
    $diagnostics | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output 'PASS: Deployment configuration is valid.'
exit 0
