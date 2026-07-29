#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$PackagePath,
    [ValidateNotNullOrEmpty()][string]$ServiceName = 'PSM Collector',
    [int]$KeepPreviousCount = 2,
    [int]$StopTimeoutSeconds = 60,
    [int]$StartTimeoutSeconds = 60,
    [int]$StabilitySeconds = 10,
    [int]$StaleLockHours = 4,
    [switch]$Force
)

. (Join-Path $PSScriptRoot 'CollectorDeployment.Common.ps1')

if ($Force) { $ConfirmPreference = 'None' }
$result = New-CollectorDeploymentResult ([guid]::NewGuid().ToString('N')) $ServiceName
$exitCodes = Get-CollectorDeploymentExitCodes
$lockStream = $null
$replacementStarted = $false
$previousCreated = $false
$expandedPath = $null
$service = $null
$originalPathName = $null

function Wait-CollectorServiceState {
    param([string]$Name, [string]$State, [int]$TimeoutSeconds)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $current = Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $Name.Replace("'","''")) -ErrorAction Stop
        if ([string]::Equals([string]$current.State, $State, [StringComparison]::OrdinalIgnoreCase)) {
            return $current
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Service '$Name' did not reach $State within $TimeoutSeconds seconds."
}

function Move-CollectorManagedFiles {
    param([string]$SourceRoot, [string]$DestinationRoot, [string[]]$RelativePaths)
    foreach ($relative in @($RelativePaths | Sort-Object { $_.Length } -Descending)) {
        $source = Join-Path $SourceRoot $relative
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        $destination = Join-Path $DestinationRoot $relative
        $parent = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $parent)) {
            $null = New-Item -ItemType Directory -Path $parent -Force
        }
        Move-Item -LiteralPath $source -Destination $destination -Force -ErrorAction Stop
    }
}

function Copy-CollectorPreservedConfiguration {
    param([string]$InstallRoot, [string]$StageRoot, $ConfigurationPlan)
    foreach ($source in @($ConfigurationPlan.PreservedFiles)) {
        $relative = $source.Substring(([IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')).Length).TrimStart('\')
        $destination = Join-Path $StageRoot $relative
        $parent = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $parent)) { $null = New-Item -ItemType Directory -Path $parent -Force }
        Copy-Item -LiteralPath $source -Destination $destination -Force -ErrorAction Stop
    }
}

function Test-CollectorFilesAgainstManifest {
    param([string]$Root, $Manifest)
    foreach ($entry in @($Manifest.Files)) {
        $relative = ([string]$entry.Path).Replace('/','\')
        if (Test-CollectorPreservedConfigurationPath $relative) { continue }
        $candidate = Join-Path $Root $relative
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "Deployed file is missing: $relative."
        }
        $actual = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256 -ErrorAction Stop).Hash
        if (-not [string]::Equals($actual, [string]$entry.Sha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Deployed hash mismatch for $relative."
        }
    }
}

function Stop-CollectorServiceGracefully {
    param($ServiceRecord, [int]$TimeoutSeconds)
    if ([string]::Equals([string]$ServiceRecord.State, 'Stopped', [StringComparison]::OrdinalIgnoreCase)) {
        return $ServiceRecord
    }
    Invoke-CimMethod -InputObject $ServiceRecord -MethodName StopService -ErrorAction Stop | Out-Null
    Wait-CollectorServiceState ([string]$ServiceRecord.Name) 'Stopped' $TimeoutSeconds
}

function Start-AndValidateCollectorService {
    param([string]$Name, [string]$ExpectedPathName, [string]$InstallRoot, $Manifest,
        [int]$TimeoutSeconds, [int]$StableSeconds, [datetimeoffset]$EventStart)
    $record = Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $Name.Replace("'","''")) -ErrorAction Stop
    Invoke-CimMethod -InputObject $record -MethodName StartService -ErrorAction Stop | Out-Null
    $running = Wait-CollectorServiceState $Name 'Running' $TimeoutSeconds
    if ([uint32]$running.ProcessId -eq 0) { throw 'Collector service is Running but has no process ID.' }
    $processId = [uint32]$running.ProcessId
    Start-Sleep -Seconds $StableSeconds
    $stable = Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $Name.Replace("'","''")) -ErrorAction Stop
    if ($stable.State -ne 'Running' -or [uint32]$stable.ProcessId -eq 0 -or [uint32]$stable.ProcessId -ne $processId) {
        throw 'Collector service did not remain stable.'
    }
    if (-not [string]::Equals([string]$stable.PathName, $ExpectedPathName, [StringComparison]::Ordinal)) {
        throw 'Collector service PathName changed during deployment.'
    }
    Test-CollectorFilesAgainstManifest $InstallRoot $Manifest
    $criticalEvents = @(Get-WinEvent -FilterHashtable @{
        LogName='Application'; StartTime=$EventStart.UtcDateTime; Level=@(1,2)
    } -ErrorAction SilentlyContinue | Where-Object {
        $_.ProviderName -match '(?i)PSM|Collector|\.NET Runtime|Application Error' -and
        $_.Message -match '(?i)PSMOperationsPlatform|WindowsCollector|PSM Collector'
    })
    if ($criticalEvents.Count -gt 0) { throw 'New critical Collector startup events were found in the Application log.' }
}

try {
    if ($KeepPreviousCount -lt 1 -or $KeepPreviousCount -gt 100 -or
        $StopTimeoutSeconds -lt 1 -or $StopTimeoutSeconds -gt 3600 -or
        $StartTimeoutSeconds -lt 1 -or $StartTimeoutSeconds -gt 3600 -or
        $StabilitySeconds -lt 1 -or $StabilitySeconds -gt 600 -or
        $StaleLockHours -lt 1 -or $StaleLockHours -gt 24) {
        $result.ExitCode = $exitCodes.InvalidParameters
        throw [ArgumentException]::new('Retention and timeout parameters are outside their supported ranges.')
    }
    Add-CollectorPhase $result 'ParameterValidation' 'PASS' 'Parameters are valid.'

    $inputPath = [IO.Path]::GetFullPath($PackagePath)
    if ($inputPath.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
        if ($WhatIfPreference) { throw 'WhatIf requires an already expanded package so validation remains read-only.' }
        $expandedPath = Join-Path ([IO.Path]::GetTempPath()) ("PSMCollectorPackage-" + $result.DeploymentId)
        Expand-Archive -LiteralPath $inputPath -DestinationPath $expandedPath -ErrorAction Stop
        $packageRoot = @(Get-ChildItem -LiteralPath $expandedPath -Directory)[0].FullName
    } elseif ((Split-Path -Leaf $inputPath) -eq 'package') {
        $packageRoot = Split-Path -Parent $inputPath
    } else {
        $packageRoot = $inputPath
    }
    $runtimePath = Join-Path $packageRoot 'package'
    $manifestPath = Join-Path $packageRoot 'deployment-manifest.json'

    Add-CollectorPhase $result 'PackageValidation' 'START' 'Validating package inventory and hashes.'
    $manifest = Test-CollectorPackageManifest $runtimePath $manifestPath {
        param($path) (Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash
    }
    Add-CollectorPhase $result 'PackageValidation' 'PASS' "Validated $(@($manifest.Files).Count) package files."

    Add-CollectorPhase $result 'ServiceDiscovery' 'START' 'Discovering the existing Collector service.'
    $allServices = @(Get-CimInstance Win32_Service -ErrorAction Stop |
        Select-Object Name,DisplayName,State,StartMode,StartName,PathName,ProcessId)
    $service = Find-CollectorService $ServiceName { $allServices }
    $ServiceName = [string]$service.Name
    $result.ServiceName = $ServiceName
    $result.OriginalServiceState = [string]$service.State
    $originalPathName = [string]$service.PathName
    $executablePath = Resolve-CollectorServiceExecutable $originalPathName
    if (-not [string]::Equals([IO.Path]::GetFileName($executablePath), $script:CollectorExecutableName,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Discovered service does not use the expected Collector executable.'
    }
    $installPath = [IO.Path]::GetDirectoryName($executablePath)
    $result.InstallPath = $installPath
    if (-not (Test-Path -LiteralPath $installPath -PathType Container)) {
        throw 'Discovered installation directory does not exist.'
    }
    Add-CollectorPhase $result 'ServiceDiscovery' 'PASS' "Found service '$ServiceName' and its installation directory."

    Add-CollectorPhase $result 'ConfigurationPreservation' 'START' 'Classifying target-owned configuration.'
    $configurationPlan = Get-CollectorConfigurationPlan $installPath
    Add-CollectorPhase $result 'ConfigurationPreservation' 'PASS' "Preserving $(@($configurationPlan.PreservedFiles).Count) target-owned configuration file(s)."

    $paths = New-CollectorSiblingPaths $installPath $result.DeploymentId
    $result.StagingPath = $paths.StagingPath
    $result.PreviousPath = $paths.PreviousPath
    $result.FailedPath = $paths.FailedPath
    $plan = [pscustomobject][ordered]@{
        PackageId=$manifest.PackageId; ServiceName=$ServiceName; OriginalState=$service.State
        InstallPath=$installPath; StagingPath=$paths.StagingPath; PreviousPath=$paths.PreviousPath
        PreservedConfiguration=@($configurationPlan.PreservedFiles); KeepPreviousCount=$KeepPreviousCount
    }
    if ($WhatIfPreference) {
        $null = $PSCmdlet.ShouldProcess($installPath, 'Deploy validated Collector package')
        Add-CollectorPhase $result 'Complete' 'WHATIF' 'Read-only validation and deployment planning completed.'
        $result.Status = 'WHATIF'
        $result.ExitCode = $exitCodes.Success
        $plan
        $result
        return
    }
    if (-not $PSCmdlet.ShouldProcess($installPath, 'Deploy validated Collector package with automatic rollback')) {
        $result.Status = 'CANCELLED'; $result.ExitCode = $exitCodes.Success; $result; return
    }

    Add-CollectorPhase $result 'Lock' 'START' 'Acquiring local deployment lock.'
    $lockPath = Join-Path ([IO.Path]::GetTempPath()) ("PSMCollector-{0}.deployment.lock" -f
        ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($ServiceName))).TrimEnd('=').Replace('/','_').Replace('+','-'))
    if (Test-Path -LiteralPath $lockPath) {
        $age = [DateTimeOffset]::UtcNow - [DateTimeOffset](Get-Item -LiteralPath $lockPath).LastWriteTimeUtc
        if ($age.TotalHours -lt $StaleLockHours) {
            $result.ExitCode = $exitCodes.Concurrency
            throw 'Another local Collector deployment holds the deployment lock.'
        }
        Move-Item -LiteralPath $lockPath -Destination ($lockPath + '.stale.' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
    }
    $lockStream = [IO.File]::Open($lockPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $lockBytes = [Text.Encoding]::UTF8.GetBytes("DeploymentId=$($result.DeploymentId)`nServiceName=$ServiceName")
    $lockStream.Write($lockBytes, 0, $lockBytes.Length)
    Add-CollectorPhase $result 'Lock' 'PASS' 'Local deployment lock acquired.'

    Add-CollectorPhase $result 'TargetStaging' 'START' 'Creating and validating same-volume staging.'
    if (Test-Path -LiteralPath $paths.StagingPath) { throw 'Generated staging path already exists.' }
    $null = New-Item -ItemType Directory -Path $paths.StagingPath
    Copy-Item -Path (Join-Path $runtimePath '*') -Destination $paths.StagingPath -Recurse -Force
    Copy-CollectorPreservedConfiguration $installPath $paths.StagingPath $configurationPlan
    Test-CollectorFilesAgainstManifest $paths.StagingPath $manifest
    if (-not [string]::Equals([IO.Path]::GetPathRoot($paths.StagingPath), [IO.Path]::GetPathRoot($installPath),
            [StringComparison]::OrdinalIgnoreCase)) { throw 'Staging is not on the installation volume.' }
    Add-CollectorPhase $result 'TargetStaging' 'PASS' 'Staged runtime validated on the active volume.'

    Add-CollectorPhase $result 'ServiceStop' 'START' 'Stopping the Collector service gracefully.'
    Stop-CollectorServiceGracefully $service $StopTimeoutSeconds | Out-Null
    Add-CollectorPhase $result 'ServiceStop' 'PASS' 'Collector service is stopped.'

    Add-CollectorPhase $result 'PreviousVersionCreation' 'START' 'Moving product-managed runtime to the previous-version directory.'
    $null = New-Item -ItemType Directory -Path $paths.PreviousPath
    $previousManifestPath = Join-Path $installPath 'deployment-manifest.json'
    $oldPaths = @()
    if (Test-Path -LiteralPath $previousManifestPath -PathType Leaf) {
        $oldManifest = Get-Content -LiteralPath $previousManifestPath -Raw | ConvertFrom-Json
        $oldPaths = @(Get-CollectorProductManagedPaths $oldManifest)
    }
    foreach ($newPath in @(Get-CollectorProductManagedPaths $manifest)) {
        if ($oldPaths -notcontains $newPath) { $oldPaths += $newPath }
    }
    $oldPaths += 'deployment-manifest.json'
    Move-CollectorManagedFiles $installPath $paths.PreviousPath @($oldPaths | Select-Object -Unique)
    $previousCreated = $true
    Add-CollectorPhase $result 'PreviousVersionCreation' 'PASS' 'Previous product-managed runtime retained; arbitrary target content was not moved.'

    Add-CollectorPhase $result 'ActiveDeployment' 'START' 'Moving staged product-managed runtime into the active directory.'
    $replacementStarted = $true
    $newPaths = @(Get-CollectorProductManagedPaths $manifest)
    Move-CollectorManagedFiles $paths.StagingPath $installPath $newPaths
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $installPath 'deployment-manifest.json') -Force
    foreach ($preserved in @($configurationPlan.PreservedFiles)) {
        $relative = $preserved.Substring(([IO.Path]::GetFullPath($installPath).TrimEnd('\')).Length).TrimStart('\')
        $stagedConfig = Join-Path $paths.StagingPath $relative
        if (Test-Path -LiteralPath $stagedConfig) {
            Copy-Item -LiteralPath $stagedConfig -Destination (Join-Path $installPath $relative) -Force
        }
    }
    Add-CollectorPhase $result 'DeploymentVerification' 'START' 'Verifying active executable and package hashes.'
    Test-CollectorFilesAgainstManifest $installPath $manifest
    Add-CollectorPhase $result 'DeploymentVerification' 'PASS' 'Active package hashes match the package manifest.'

    Add-CollectorPhase $result 'ServiceStart' 'START' 'Starting the Collector service.'
    Add-CollectorPhase $result 'HealthValidation' 'START' 'Validating service stability, identity, executable hash, and startup events.'
    Start-AndValidateCollectorService $ServiceName $originalPathName $installPath $manifest `
        $StartTimeoutSeconds $StabilitySeconds ([DateTimeOffset]$result.StartedAtUtc)
    Add-CollectorPhase $result 'HealthValidation' 'PASS' 'Collector service health validation passed.'

    Add-CollectorPhase $result 'RetentionCleanup' 'START' 'Applying previous-version retention.'
    try {
        $parent = Split-Path -Parent $installPath
        $base = [IO.Path]::GetFileName($installPath)
        $pattern = '^' + [regex]::Escape($base) + '_Previous_\d{8}T\d{6}Z$'
        $previousDirectories = @(Get-ChildItem -LiteralPath $parent -Directory | Where-Object { $_.Name -match $pattern } |
            Sort-Object Name -Descending)
        $keep = [Math]::Max(1, $KeepPreviousCount)
        foreach ($directory in @($previousDirectories | Select-Object -Skip $keep)) {
            if ([string]::Equals($directory.FullName, $paths.PreviousPath, [StringComparison]::OrdinalIgnoreCase)) { continue }
            Remove-Item -LiteralPath $directory.FullName -Recurse -Force -ErrorAction Stop
        }
        Add-CollectorPhase $result 'RetentionCleanup' 'PASS' "Retained $keep previous version(s)."
    } catch {
        $result.Warnings = @($result.Warnings) + 'Previous-version retention cleanup failed; deployment remains successful.'
        Add-CollectorPhase $result 'RetentionCleanup' 'WARNING' 'Retention cleanup failed.'
    }

    if (Test-Path -LiteralPath $paths.StagingPath) {
        Remove-Item -LiteralPath $paths.StagingPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    Add-CollectorPhase $result 'Complete' 'PASS' 'Collector deployment completed.'
    $result.Status = if (@($result.Warnings).Count) { 'SUCCESS_WITH_WARNING' } else { 'SUCCESS' }
    $result.ExitCode = if (@($result.Warnings).Count) { $exitCodes.Warning } else { $exitCodes.Success }
} catch {
    $failure = Get-CollectorSafeError $_ $result.CurrentPhase
    $result.Errors = @($result.Errors) + $failure
    Add-CollectorPhase $result 'Failure' 'FAIL' $failure.Message
    if ($result.ExitCode -eq $exitCodes.Unexpected) {
        switch ($failure.CurrentPhase) {
            'PackageValidation' { $result.ExitCode = $exitCodes.PackageValidation }
            'ServiceDiscovery' { $result.ExitCode = $exitCodes.ServiceDiscovery }
            'ConfigurationPreservation' { $result.ExitCode = $exitCodes.Configuration }
            'TargetStaging' { $result.ExitCode = $exitCodes.Staging }
            'ServiceStop' { $result.ExitCode = $exitCodes.ServiceStop }
            'PreviousVersionCreation' { $result.ExitCode = $exitCodes.ActiveDeployment }
            'ActiveDeployment' { $result.ExitCode = $exitCodes.ActiveDeployment }
            'DeploymentVerification' { $result.ExitCode = $exitCodes.ActiveDeployment }
            'ServiceStart' { $result.ExitCode = $exitCodes.ServiceHealth }
            'HealthValidation' { $result.ExitCode = $exitCodes.ServiceHealth }
        }
    }
    if ($replacementStarted -and $previousCreated) {
        $result.RollbackAttempted = $true
        Add-CollectorPhase $result 'Rollback' 'START' 'Restoring previous product-managed runtime.'
        try {
            $currentManifest = if (Test-Path -LiteralPath (Join-Path $installPath 'deployment-manifest.json')) {
                Get-Content -LiteralPath (Join-Path $installPath 'deployment-manifest.json') -Raw | ConvertFrom-Json
            } else { $manifest }
            $currentService = Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $ServiceName.Replace("'","''")) -ErrorAction Stop
            Stop-CollectorServiceGracefully $currentService $StopTimeoutSeconds | Out-Null
            if (-not (Test-Path -LiteralPath $paths.FailedPath)) { $null = New-Item -ItemType Directory -Path $paths.FailedPath }
            Move-CollectorManagedFiles $installPath $paths.FailedPath @(Get-CollectorProductManagedPaths $currentManifest)
            if (Test-Path -LiteralPath (Join-Path $installPath 'deployment-manifest.json')) {
                Move-Item -LiteralPath (Join-Path $installPath 'deployment-manifest.json') -Destination $paths.FailedPath -Force
            }
            $restoreFiles = @(Get-ChildItem -LiteralPath $paths.PreviousPath -File -Recurse)
            foreach ($file in $restoreFiles) {
                $relative = $file.FullName.Substring($paths.PreviousPath.Length).TrimStart('\')
                $destination = Join-Path $installPath $relative
                $parent = Split-Path -Parent $destination
                if (-not (Test-Path -LiteralPath $parent)) { $null = New-Item -ItemType Directory -Path $parent -Force }
                Move-Item -LiteralPath $file.FullName -Destination $destination -Force
            }
            if (-not (Test-Path -LiteralPath (Join-Path $installPath $script:CollectorExecutableName))) {
                throw 'Previous Collector executable is missing after rollback.'
            }
            $restored = Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $ServiceName.Replace("'","''")) -ErrorAction Stop
            Invoke-CimMethod -InputObject $restored -MethodName StartService -ErrorAction Stop | Out-Null
            $null = Wait-CollectorServiceState $ServiceName 'Running' $StartTimeoutSeconds
            Start-Sleep -Seconds $StabilitySeconds
            $stable = Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $ServiceName.Replace("'","''")) -ErrorAction Stop
            if ($stable.State -ne 'Running' -or [uint32]$stable.ProcessId -eq 0) { throw 'Restored service is not stable.' }
            $result.RollbackStatus = 'SUCCESS'
            $result.Status = 'DEPLOYMENT_FAILED_ROLLBACK_SUCCEEDED'
            $result.ExitCode = $exitCodes.RollbackSucceeded
            Add-CollectorPhase $result 'Rollback' 'PASS' 'Previous runtime restored and service stability confirmed.'
        } catch {
            $result.RollbackStatus = 'FAILED'
            $result.Status = 'DEPLOYMENT_AND_ROLLBACK_FAILED'
            $result.ExitCode = $exitCodes.RollbackFailed
            $result.Errors = @($result.Errors) + (Get-CollectorSafeError $_ 'Rollback')
            Add-CollectorPhase $result 'Rollback' 'FAIL' "Manual recovery paths: previous='$($paths.PreviousPath)'; failed='$($paths.FailedPath)'; active='$installPath'."
        }
    }
} finally {
    if ($null -ne $lockStream) {
        $lockName = $lockStream.Name
        $lockStream.Dispose()
        Remove-Item -LiteralPath $lockName -Force -ErrorAction SilentlyContinue
    }
    if ($expandedPath -and (Test-Path -LiteralPath $expandedPath)) {
        Remove-Item -LiteralPath $expandedPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    $result.EndedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if (-not $WhatIfPreference) {
        try {
            $logRoot = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'PSMOperationsPlatform\DeploymentLogs'
            if (-not (Test-Path -LiteralPath $logRoot)) { $null = New-Item -ItemType Directory -Path $logRoot -Force }
            $result.LogPath = Join-Path $logRoot ("CollectorDeployment-{0}.json" -f $result.DeploymentId)
            [IO.File]::WriteAllText($result.LogPath, ($result | ConvertTo-Json -Depth 12),
                (New-Object Text.UTF8Encoding($false)))
        } catch {
            $result.Warnings = @($result.Warnings) + 'Deployment log could not be written.'
            if ($result.ExitCode -eq 0) { $result.ExitCode = 1; $result.Status = 'SUCCESS_WITH_WARNING' }
        }
    }
}

$result
if ($MyInvocation.InvocationName -ne '.') { exit $result.ExitCode }
