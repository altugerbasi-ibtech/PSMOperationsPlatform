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

$sectionNames = @('Deployment','SqlServer','Collector','Portal','SqlCollector','IisTargets','SqlTargets','MonitoringValidation','PerformanceValidation','HistoryValidation','Security','Validation')
Test-AllowedProperties $configuration '$' $sectionNames
foreach ($sectionName in @('Deployment','SqlServer','Collector','Portal','SqlCollector','MonitoringValidation','PerformanceValidation','HistoryValidation','Security','Validation')) {
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
Test-AllowedProperties $configuration.Portal 'Portal' @('Name','Server','ServiceAccount','ValidationEnabled','DeploymentExpected','HostingModel','Scheme','Port','BasePath','HealthPath','AuthenticationMode','ExpectedProductVersion','ApplicationPath','ConfigurationPath','LogPath')
Test-AllowedProperties $configuration.SqlCollector 'SqlCollector' @('Server','ServiceAccount')
Test-RequiredString $configuration.Collector 'Collector' 'Server'
Test-RequiredString $configuration.Collector 'Collector' 'ServiceAccount' '^[^\s]+\\[^\s]+\$$'
Test-RequiredString $configuration.Collector 'Collector' 'LogPath'
Test-RequiredString $configuration.Portal 'Portal' 'Server'
Test-RequiredString $configuration.Portal 'Portal' 'ServiceAccount' '^[^\s]+\\[^\s]+\$$'
Test-RequiredString $configuration.Portal 'Portal' 'Name' '^[A-Za-z0-9][A-Za-z0-9._-]*$'
foreach($name in @('ValidationEnabled','DeploymentExpected')){Test-Boolean $configuration.Portal 'Portal' $name}
Test-Port $configuration.Portal.Port 'Portal.Port'
foreach($item in @(@('HostingModel','^AspNetCoreIIS$'),@('Scheme','^https$'),@('BasePath','^/(?:[A-Za-z0-9._~-]+(?:/[A-Za-z0-9._~-]+)*)?$'),@('HealthPath','^/health$'),@('AuthenticationMode','^Windows$'),@('ApplicationPath','^[A-Za-z]:\\'),@('ConfigurationPath','^[A-Za-z]:\\'),@('LogPath','^[A-Za-z]:\\'))){Test-RequiredString $configuration.Portal 'Portal' $item[0] $item[1]}
if($configuration.Portal.PSObject.Properties['ExpectedProductVersion'] -and ($configuration.Portal.ExpectedProductVersion -isnot [string] -or $configuration.Portal.ExpectedProductVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')){Add-Diagnostic 'Schema violation: Portal.ExpectedProductVersion is invalid.'}
Test-RequiredString $configuration.SqlCollector 'SqlCollector' 'Server'
Test-RequiredString $configuration.SqlCollector 'SqlCollector' 'ServiceAccount' '^[^\s]+\\[^\s]+\$$'

if ($null -eq $configuration.PSObject.Properties['IisTargets'] -or
    $configuration.IisTargets -isnot [array] -or $configuration.IisTargets.Count -eq 0) {
    Add-Diagnostic 'Schema violation: IisTargets must be a non-empty array.'
} else {
    $normalizedIisTargets = @{}
    for ($index = 0; $index -lt $configuration.IisTargets.Count; $index++) {
        $target = $configuration.IisTargets[$index]
        if ($target -isnot [string] -or [string]::IsNullOrWhiteSpace($target) -or
            $target.Length -gt 255 -or
            $target -notmatch '^[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?$') {
            Add-Diagnostic "Schema violation: IisTargets[$index] must be a valid DNS/server name."
            continue
        }
        $key = $target.ToUpperInvariant()
        if ($normalizedIisTargets.ContainsKey($key)) {
            Add-Diagnostic "Duplicate IIS target is not allowed: $target."
        } else {
            $normalizedIisTargets[$key] = $true
        }
    }
}

Test-AllowedProperties $configuration.MonitoringValidation 'MonitoringValidation' @('InstrumentationName','InstrumentationVersion','HealthValidationEnabled','MetricsValidationEnabled','ActivityValidationEnabled','SnapshotValidationEnabled','ExporterExpected','BackendExpected','ExporterType','ExporterEndpoint')
Test-RequiredString $configuration.MonitoringValidation 'MonitoringValidation' 'InstrumentationName' '^PSMOperationsPlatform\.Execution$'
Test-RequiredString $configuration.MonitoringValidation 'MonitoringValidation' 'InstrumentationVersion' '^1\.0$'
foreach($name in @('HealthValidationEnabled','MetricsValidationEnabled','ActivityValidationEnabled','SnapshotValidationEnabled','ExporterExpected','BackendExpected')){Test-Boolean $configuration.MonitoringValidation 'MonitoringValidation' $name}
if($configuration.MonitoringValidation.BackendExpected -and -not $configuration.MonitoringValidation.ExporterExpected){Add-Diagnostic 'Schema violation: MonitoringValidation.BackendExpected requires ExporterExpected.'}
if($configuration.MonitoringValidation.ExporterExpected){Test-RequiredString $configuration.MonitoringValidation 'MonitoringValidation' 'ExporterType' '^OpenTelemetry$';Test-RequiredString $configuration.MonitoringValidation 'MonitoringValidation' 'ExporterEndpoint' '^https://[^/?#@]+(?::[0-9]{1,5})?/[A-Za-z0-9._~/-]*$'}
elseif($configuration.MonitoringValidation.PSObject.Properties['ExporterType'] -or $configuration.MonitoringValidation.PSObject.Properties['ExporterEndpoint']){Add-Diagnostic 'Schema violation: exporter fields require ExporterExpected=true.'}

$performanceProperties=@('PerformanceValidationEnabled','ValidationProfile','SyntheticTargetCount','SyntheticRunCount','SyntheticStepCount','MaximumParallelism','WarmupIterations','MeasurementIterations','LivePerformanceValidationEnabled','QueryPlanValidationEnabled')
Test-AllowedProperties $configuration.PerformanceValidation 'PerformanceValidation' $performanceProperties
foreach($name in @('PerformanceValidationEnabled','LivePerformanceValidationEnabled','QueryPlanValidationEnabled')){Test-Boolean $configuration.PerformanceValidation 'PerformanceValidation' $name}
Test-RequiredString $configuration.PerformanceValidation 'PerformanceValidation' 'ValidationProfile' '^(Smoke|Standard|Extended)$'
$profileValues=@{Smoke=@(5,10,5,1,1,3);Standard=@(20,100,10,2,2,5);Extended=@(100,1000,20,4,3,7)}
$numericNames=@('SyntheticTargetCount','SyntheticRunCount','SyntheticStepCount','MaximumParallelism','WarmupIterations','MeasurementIterations')
if($configuration.PerformanceValidation.ValidationProfile -in $profileValues.Keys){$expected=$profileValues[$configuration.PerformanceValidation.ValidationProfile];for($i=0;$i -lt $numericNames.Count;$i++){if($configuration.PerformanceValidation.($numericNames[$i]) -isnot [int] -or $configuration.PerformanceValidation.($numericNames[$i]) -ne $expected[$i]){Add-Diagnostic "Schema violation: PerformanceValidation profile values must match the approved $($configuration.PerformanceValidation.ValidationProfile) profile."}}}
if($configuration.PerformanceValidation.LivePerformanceValidationEnabled -or $configuration.PerformanceValidation.QueryPlanValidationEnabled){Add-Diagnostic 'Schema violation: live performance and query-plan validation require separate authorization and must be false.'}

$historyProperties=@('HistoryValidationEnabled','ProjectionValidationEnabled','QueryValidationEnabled','RetentionValidationEnabled','ExpectedHistorySchemaVersion','RetentionPolicyProfile','RetentionBatchSize','RetentionDryRunEnabled')
Test-AllowedProperties $configuration.HistoryValidation 'HistoryValidation' $historyProperties
foreach($name in @('HistoryValidationEnabled','ProjectionValidationEnabled','QueryValidationEnabled','RetentionValidationEnabled','RetentionDryRunEnabled')){Test-Boolean $configuration.HistoryValidation 'HistoryValidation' $name}
if($configuration.HistoryValidation.ExpectedHistorySchemaVersion -isnot [int] -or $configuration.HistoryValidation.ExpectedHistorySchemaVersion -ne 1){Add-Diagnostic 'Schema violation: HistoryValidation.ExpectedHistorySchemaVersion must be 1.'}
Test-RequiredString $configuration.HistoryValidation 'HistoryValidation' 'RetentionPolicyProfile' '^ExecutionHistoryV1$'
if($configuration.HistoryValidation.RetentionBatchSize -isnot [int] -or $configuration.HistoryValidation.RetentionBatchSize -ne 500){Add-Diagnostic 'Schema violation: HistoryValidation.RetentionBatchSize must match ExecutionHistoryV1 value 500.'}
if($configuration.HistoryValidation.RetentionDryRunEnabled){Add-Diagnostic 'Schema violation: HistoryValidation.RetentionDryRunEnabled must be false because no dry-run contract exists.'}

if ($null -eq $configuration.PSObject.Properties['SqlTargets'] -or
    $configuration.SqlTargets -isnot [array] -or $configuration.SqlTargets.Count -eq 0) {
    Add-Diagnostic 'Schema violation: SqlTargets must be a non-empty array.'
} else {
    $sqlTargetNames=@{};$sqlTargetEndpoints=@{}
    $allowedSqlTargetProperties=@('Name','Server','Instance','Port','ExpectedRole','ExpectedVersion',
        'ExpectedEdition','Encrypt','TrustServerCertificate','DatabasesToValidate',
        'RequiredPermissionsProfile','ValidationEnabled')
    for($index=0;$index -lt $configuration.SqlTargets.Count;$index++){
        $target=$configuration.SqlTargets[$index];$path="SqlTargets[$index]"
        if($target -isnot [pscustomobject]){Add-Diagnostic "Schema violation: $path must be an object.";continue}
        Test-AllowedProperties $target $path $allowedSqlTargetProperties
        Test-RequiredString $target $path 'Name' '^[A-Za-z0-9][A-Za-z0-9._-]*$'
        Test-RequiredString $target $path 'Server' '^[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?$'
        Test-RequiredString $target $path 'Instance' '^[A-Za-z0-9_.$-]+$'
        foreach($name in @('ExpectedRole','RequiredPermissionsProfile')){Test-RequiredString $target $path $name}
        Test-Port $target.Port "$path.Port"
        foreach($name in @('Encrypt','TrustServerCertificate','ValidationEnabled')){Test-Boolean $target $path $name}
        if($target.Encrypt -is [bool] -and -not $target.Encrypt){Add-Diagnostic "Schema violation: $path.Encrypt must be true."}
        if($target.TrustServerCertificate -is [bool] -and $target.TrustServerCertificate){Add-Diagnostic "Schema violation: $path.TrustServerCertificate must be false."}
        if($target.ExpectedRole -notin @('ManagedSqlTarget','OperationsDatabase')){Add-Diagnostic "Schema violation: $path.ExpectedRole is not allowed."}
        if($target.RequiredPermissionsProfile -notin @('SqlCollectorMetadataV1','OperationsDatabaseValidationV1')){Add-Diagnostic "Schema violation: $path.RequiredPermissionsProfile is not allowed."}
        if($null -eq $target.PSObject.Properties['DatabasesToValidate'] -or $target.DatabasesToValidate -isnot [array]){Add-Diagnostic "Schema violation: $path.DatabasesToValidate must be an array."}
        else{$databaseKeys=@{};foreach($database in $target.DatabasesToValidate){if($database -isnot [string] -or $database -notmatch '^[A-Za-z0-9_.-]+$'){Add-Diagnostic "Schema violation: $path.DatabasesToValidate contains an invalid name."}elseif($databaseKeys.ContainsKey($database.ToUpperInvariant())){Add-Diagnostic "Duplicate database is not allowed in $path."}else{$databaseKeys[$database.ToUpperInvariant()]=$true}}}
        if($target.PSObject.Properties['ExpectedVersion'] -and ($target.ExpectedVersion -isnot [string] -or $target.ExpectedVersion -notmatch '^[0-9]+(?:\.[0-9]+){0,3}$')){Add-Diagnostic "Schema violation: $path.ExpectedVersion is invalid."}
        if($target.PSObject.Properties['ExpectedEdition'] -and ($target.ExpectedEdition -isnot [string] -or [string]::IsNullOrWhiteSpace($target.ExpectedEdition))){Add-Diagnostic "Schema violation: $path.ExpectedEdition is invalid."}
        if($target.Name -is [string]){$key=$target.Name.ToUpperInvariant();if($sqlTargetNames.ContainsKey($key)){Add-Diagnostic "Duplicate SQL target name is not allowed: $($target.Name)."}else{$sqlTargetNames[$key]=$true}}
        if($target.Server -is [string] -and $target.Instance -is [string] -and $target.Port -is [int]){$key="$($target.Server)|$($target.Instance)|$($target.Port)".ToUpperInvariant();if($sqlTargetEndpoints.ContainsKey($key)){Add-Diagnostic "Duplicate SQL target endpoint is not allowed: $path."}else{$sqlTargetEndpoints[$key]=$true}}
        if($target.ExpectedRole -eq 'ManagedSqlTarget' -and $target.RequiredPermissionsProfile -ne 'SqlCollectorMetadataV1'){Add-Diagnostic "Schema violation: $path managed target requires SqlCollectorMetadataV1."}
        if($target.ExpectedRole -eq 'OperationsDatabase'){
            if($target.RequiredPermissionsProfile -ne 'OperationsDatabaseValidationV1'){Add-Diagnostic "Schema violation: $path Operations database requires OperationsDatabaseValidationV1."}
            if($target.Server -ne $configuration.SqlServer.Server -or $target.Instance -ne $configuration.SqlServer.Instance -or $target.Port -ne $configuration.SqlServer.Port -or @($target.DatabasesToValidate).Count -ne 1 -or $target.DatabasesToValidate[0] -ne $configuration.SqlServer.Database){Add-Diagnostic "Schema violation: $path must align with the SqlServer Operations database endpoint."}
        }
    }
}

Test-AllowedProperties $configuration.Security 'Security' @(
    'WindowsAuthentication','KerberosOnly','WinRMPort','IncludePortInSPN','UseTLS')
foreach ($name in @('WindowsAuthentication','KerberosOnly','IncludePortInSPN','UseTLS')) {
    Test-Boolean $configuration.Security 'Security' $name
}
if ($configuration.Security.WindowsAuthentication -is [bool] -and
    -not $configuration.Security.WindowsAuthentication) {
    Add-Diagnostic 'Schema violation: Security.WindowsAuthentication must be true.'
}
if ($configuration.Security.KerberosOnly -is [bool] -and
    -not $configuration.Security.KerberosOnly) {
    Add-Diagnostic 'Schema violation: Security.KerberosOnly must be true for IIS target validation.'
}
if ($configuration.Security.IncludePortInSPN -is [bool] -and
    -not $configuration.Security.IncludePortInSPN) {
    Add-Diagnostic 'Schema violation: Security.IncludePortInSPN must be true for IIS target validation.'
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
