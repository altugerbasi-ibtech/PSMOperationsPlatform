#requires -Version 5.1
Set-StrictMode -Version Latest

$script:CollectorDeploymentVersion = '2.0.0'
$script:CollectorExecutableName = 'PSMOperationsPlatform.WindowsCollector.exe'
$script:CollectorDllName = 'PSMOperationsPlatform.WindowsCollector.dll'
$script:CollectorTargetFramework = 'net10.0'
$script:CollectorExitCodes = [ordered]@{
    Success = 0
    Warning = 1
    PackageValidation = 2
    ServiceDiscovery = 3
    Configuration = 4
    Staging = 5
    ServiceStop = 6
    ActiveDeployment = 7
    ServiceHealth = 8
    RollbackSucceeded = 9
    RollbackFailed = 10
    Concurrency = 11
    InvalidParameters = 12
    Unexpected = 13
}

function Get-CollectorDeploymentExitCodes {
    [pscustomobject]$script:CollectorExitCodes
}

function Resolve-CollectorServiceExecutable {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PathName)

    $value = $PathName.Trim()
    if ($value.Length -eq 0) { throw 'Service PathName is empty.' }
    if ($value[0] -eq '"') {
        $closingQuote = $value.IndexOf('"', 1)
        if ($closingQuote -le 1) { throw 'Quoted service PathName is malformed.' }
        return $value.Substring(1, $closingQuote - 1)
    }
    $match = [regex]::Match($value, '(?i)^(.+?\.exe)(?:\s|$)')
    if (-not $match.Success) { throw 'Unquoted service PathName does not contain an executable path.' }
    $match.Groups[1].Value
}

function Find-CollectorService {
    [CmdletBinding()]
    param(
        [string]$ServiceName = 'PSM Collector',
        [Parameter(Mandatory)][scriptblock]$GetServices
    )

    $services = @(& $GetServices)
    $named = @($services | Where-Object {
        [string]::Equals([string]$_.Name, $ServiceName, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($named.Count -eq 1) { return $named[0] }
    if ($named.Count -gt 1) { throw "Multiple services matched the explicit service name '$ServiceName'." }

    $executableMatches = @()
    foreach ($service in $services) {
        try { $path = Resolve-CollectorServiceExecutable ([string]$service.PathName) }
        catch { continue }
        if ([string]::Equals([IO.Path]::GetFileName($path), $script:CollectorExecutableName,
                [StringComparison]::OrdinalIgnoreCase)) {
            $executableMatches += $service
        }
    }
    if ($executableMatches.Count -eq 1) { return $executableMatches[0] }
    if ($executableMatches.Count -eq 0) {
        throw "Collector service '$ServiceName' was not found and no exact executable-path fallback matched."
    }
    throw 'Multiple services use the Collector executable; specify an unambiguous -ServiceName.'
}

function New-CollectorDotNetArguments {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Restore','Build','Publish')][string]$Operation,
        [Parameter(Mandatory)][string]$ProjectPath,
        [Parameter(Mandatory)][string]$Configuration,
        [string]$OutputPath
    )
    switch ($Operation) {
        'Restore' { [object[]]@('restore', $ProjectPath) }
        'Build' { [object[]]@('build', $ProjectPath, '--configuration', $Configuration, '--no-restore') }
        'Publish' {
            if ([string]::IsNullOrWhiteSpace($OutputPath)) { throw 'OutputPath is required for publish.' }
            [object[]]@('publish', $ProjectPath, '--configuration', $Configuration,
                '--framework', $script:CollectorTargetFramework, '--no-build', '--output', $OutputPath)
        }
    }
}

function Invoke-CollectorNativeProcess {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$FilePath, [Parameter(Mandatory)][object[]]$Arguments)
    $started = [DateTimeOffset]::UtcNow
    $records = @(& $FilePath @Arguments 2>&1)
    $nativeExitCode = $LASTEXITCODE
    [pscustomobject][ordered]@{
        ExitCode = $nativeExitCode
        Output = @($records | ForEach-Object { [string]$_ })
        DurationMilliseconds = [int]([DateTimeOffset]::UtcNow - $started).TotalMilliseconds
    }
}

function Get-CollectorPackageFiles {
    param([Parameter(Mandatory)][string]$Path)
    @(Get-ChildItem -LiteralPath $Path -File -Recurse)
}

function New-CollectorPackageManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PackagePath,
        [Parameter(Mandatory)][string]$Configuration,
        [Parameter(Mandatory)][string]$RepositoryCommit,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][bool]$DirtyWorktree,
        [Parameter(Mandatory)][scriptblock]$GetHash,
        [datetimeoffset]$CreatedAt = [DateTimeOffset]::UtcNow
    )
    $root = [IO.Path]::GetFullPath($PackagePath).TrimEnd('\')
    $entries = @()
    foreach ($file in @(Get-CollectorPackageFiles $root | Sort-Object FullName)) {
        $entries += [pscustomobject][ordered]@{
            Path = $file.FullName.Substring($root.Length).TrimStart('\').Replace('\', '/')
            Length = [long]$file.Length
            Sha256 = [string](& $GetHash $file.FullName)
        }
    }
    $hashText = @($entries | ForEach-Object { "$($_.Path):$($_.Length):$($_.Sha256)" }) -join "`n"
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $summary = ([BitConverter]::ToString(
            $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($hashText)))).Replace('-','')
    } finally { $sha.Dispose() }
    [pscustomobject][ordered]@{
        SchemaVersion = '2.0'
        WorkPackage = 'WP-006.8'
        DeploymentScriptVersion = $script:CollectorDeploymentVersion
        PackageId = $null
        CreatedAtUtc = $CreatedAt.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
        RepositoryCommit = $RepositoryCommit
        Branch = $Branch
        DirtyWorktree = $DirtyWorktree
        Configuration = $Configuration
        TargetFramework = $script:CollectorTargetFramework
        PrimaryExecutable = $script:CollectorExecutableName
        FileCount = @($entries).Count
        TotalSizeBytes = [long](($entries | Measure-Object Length -Sum).Sum)
        PackageHashSummary = $summary
        Files = @($entries)
        ContainsSecrets = $false
    }
}

function Test-CollectorPackageRuntime {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PackagePath)
    if (-not (Test-Path -LiteralPath $PackagePath -PathType Container)) {
        throw 'Package runtime directory does not exist.'
    }
    foreach ($required in @($script:CollectorExecutableName, $script:CollectorDllName)) {
        if (-not (Test-Path -LiteralPath (Join-Path $PackagePath $required) -PathType Leaf)) {
            throw "Deployment package is missing $required."
        }
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $PackagePath -File -Recurse)) {
        $relative = $file.FullName.Substring(([IO.Path]::GetFullPath($PackagePath).TrimEnd('\')).Length).TrimStart('\')
        $isProhibitedDirectory = $relative -match
            '(?i)(^|\\)(logs?|dumps?|diagnostics?|previous[^\\]*|bin|obj|\.git|\.vs|usersecrets)(\\|$)'
        $isProhibitedFile = $file.Name -match
            '(?i)(^secrets\.json$|^appsettings\.Development\.json$|\.pubxml\.user$|\.user$|\.pfx$|\.p12$|\.cer$|\.key$|\.cs$|\.fs$|\.vb$|\.csproj$|\.fsproj$|\.vbproj$|\.sln$|\.suo$)'
        if ($isProhibitedDirectory -or $isProhibitedFile) {
            throw "Deployment package contains prohibited content: $relative."
        }
        if ($file.Name -like 'appsettings*.json') {
            try { $configuration = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json }
            catch { throw "Configuration file is not valid JSON: $($file.Name)." }
            $property = $configuration.psobject.Properties['ConnectionStrings']
            if ($null -ne $property) {
                foreach ($entry in @($property.Value.psobject.Properties)) {
                    if (-not [string]::IsNullOrWhiteSpace([string]$entry.Value)) {
                        throw 'Deployment package contains an embedded connection string.'
                    }
                }
            }
        }
    }
}

function Test-CollectorPackageManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PackagePath,
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][scriptblock]$GetHash
    )
    Test-CollectorPackageRuntime $PackagePath
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw 'deployment-manifest.json is missing.'
    }
    try { $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json }
    catch { throw 'deployment-manifest.json is not valid JSON.' }
    if ([string]::IsNullOrWhiteSpace([string]$manifest.PackageId)) { throw 'PackageId is missing.' }
    $entries = @($manifest.Files)
    if ($entries.Count -eq 0 -or [int]$manifest.FileCount -ne $entries.Count) {
        throw 'Package manifest file inventory is invalid.'
    }
    $seen = @{}
    foreach ($entry in $entries) {
        $relative = ([string]$entry.Path).Replace('/','\')
        if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or
            $relative -match '(^|\\)\.\.(\\|$)') { throw 'Package manifest contains an unsafe path.' }
        $key = $relative.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { throw "Package manifest contains a duplicate path: $relative." }
        $seen[$key] = $true
        $candidate = Join-Path $PackagePath $relative
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "Package file is missing: $relative."
        }
        $actual = [string](& $GetHash $candidate)
        if (-not [string]::Equals($actual, [string]$entry.Sha256,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Package hash validation failed for $relative."
        }
    }
    $manifest
}

function Test-CollectorPreservedConfigurationPath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RelativePath)
    $name = [IO.Path]::GetFileName($RelativePath)
    if ([string]::Equals($name, 'appsettings.Production.json', 'OrdinalIgnoreCase')) { return $true }
    if ([string]::Equals($name, 'appsettings.Local.json', 'OrdinalIgnoreCase')) { return $true }
    if ($name -match '(?i)^appsettings\..+\.local\.json$') { return $true }
    if ($name -match '(?i)^logging(\..+)?\.local\.json$') { return $true }
    if ($name -match '(?i)^service(\..+)?\.local\.json$') { return $true }
    $false
}

function Get-CollectorConfigurationPlan {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$InstallPath)
    $preserved = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $InstallPath -File -Filter '*.json' -ErrorAction SilentlyContinue)) {
        if (Test-CollectorPreservedConfigurationPath $file.Name) {
            $preserved += $file.FullName
        } elseif ($file.Name -match '(?i)^(appsettings|logging|service).+\.json$' -and
            -not [string]::Equals($file.Name, 'appsettings.json', 'OrdinalIgnoreCase')) {
            throw "Ambiguous configuration ownership: $($file.Name). Rename it to an approved *.local.json form or classify it explicitly."
        }
    }
    [pscustomobject][ordered]@{ PreservedFiles = @($preserved) }
}

function New-CollectorSiblingPaths {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$InstallPath,
        [Parameter(Mandatory)][string]$DeploymentId,
        [datetimeoffset]$Now = [DateTimeOffset]::UtcNow
    )
    $full = [IO.Path]::GetFullPath($InstallPath).TrimEnd('\')
    $parent = [IO.Path]::GetDirectoryName($full)
    $name = [IO.Path]::GetFileName($full)
    [pscustomobject][ordered]@{
        StagingPath = Join-Path $parent ("{0}_Staging_{1}" -f $name, $DeploymentId)
        PreviousPath = Join-Path $parent ("{0}_Previous_{1}" -f $name, $Now.UtcDateTime.ToString('yyyyMMddTHHmmssZ'))
        FailedPath = Join-Path $parent ("{0}_Failed_{1}" -f $name, $DeploymentId)
    }
}

function Get-CollectorProductManagedPaths {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Manifest)
    $paths = @()
    foreach ($entry in @($Manifest.Files)) {
        $relative = ([string]$entry.Path).Replace('/','\')
        if (-not (Test-CollectorPreservedConfigurationPath $relative)) { $paths += $relative }
    }
    @($paths)
}

function New-CollectorDeploymentResult {
    param([string]$DeploymentId, [string]$ServiceName)
    [pscustomobject][ordered]@{
        SchemaVersion = '2.0'
        WorkPackage = 'WP-006.8'
        DeploymentId = $DeploymentId
        ServiceName = $ServiceName
        Status = 'FAILED'
        ExitCode = $script:CollectorExitCodes.Unexpected
        CurrentPhase = 'ParameterValidation'
        StartedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        EndedAtUtc = $null
        OriginalServiceState = $null
        InstallPath = $null
        StagingPath = $null
        PreviousPath = $null
        FailedPath = $null
        RollbackAttempted = $false
        RollbackStatus = 'NOT_REQUIRED'
        LogPath = $null
        Phases = @()
        Warnings = @()
        Errors = @()
    }
}

function Add-CollectorPhase {
    param($Result, [string]$Phase, [string]$Status, [string]$Summary)
    $Result.CurrentPhase = $Phase
    $Result.Phases = @($Result.Phases) + [pscustomobject][ordered]@{
        TimestampUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Phase = $Phase
        Status = $Status
        Summary = $Summary
    }
}

function Get-CollectorSafeError {
    param([Parameter(Mandatory)][Management.Automation.ErrorRecord]$ErrorRecord, [string]$CurrentPhase)
    $invocation = $ErrorRecord.InvocationInfo
    [pscustomobject][ordered]@{
        CurrentPhase = $CurrentPhase
        ExceptionType = $ErrorRecord.Exception.GetType().FullName
        Message = $ErrorRecord.Exception.Message
        FullyQualifiedErrorId = $ErrorRecord.FullyQualifiedErrorId
        ScriptName = $invocation.ScriptName
        ScriptLineNumber = $invocation.ScriptLineNumber
        InvocationLine = $invocation.Line
        StackTrace = $ErrorRecord.ScriptStackTrace
        InnerException = if ($null -ne $ErrorRecord.Exception.InnerException) {
            $ErrorRecord.Exception.InnerException.Message
        } else { $null }
    }
}
