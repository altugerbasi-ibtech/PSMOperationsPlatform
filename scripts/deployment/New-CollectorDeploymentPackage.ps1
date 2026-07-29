#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputDirectory,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$')][string]$PackageId,
    [switch]$CreateZip,
    [switch]$Force
)

. (Join-Path $PSScriptRoot 'CollectorDeployment.Common.ps1')

$exitCodes = [ordered]@{Success=0;Warning=1;Validation=2;Build=3;Manifest=4;Archive=5;InvalidParameters=6;Unexpected=12}
$exitCode = $exitCodes.Unexpected
$packageRoot = $null
try {
    if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
        throw [ArgumentException]::new('RepositoryRoot does not exist.')
    }
    $projectPath = Join-Path $RepositoryRoot 'src\PSMOperationsPlatform.WindowsCollector\PSMOperationsPlatform.WindowsCollector.csproj'
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        $exitCode = $exitCodes.Validation
        throw 'Windows Collector project does not exist.'
    }
    if ([string]::IsNullOrWhiteSpace($PackageId)) {
        $PackageId = 'PSMWindowsCollector-{0}' -f [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
    } elseif (-not $PackageId.StartsWith('PSMWindowsCollector-',[StringComparison]::OrdinalIgnoreCase)) {
        $PackageId = "PSMWindowsCollector-$PackageId"
    }
    $packageRoot = Join-Path $OutputDirectory $PackageId
    $runtimePath = Join-Path $packageRoot 'package'
    $zipPath = "$packageRoot.zip"
    if ((Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $zipPath)) {
        if (-not $Force) { throw [ArgumentException]::new('Package output already exists. Use -Force with an explicit PackageId to replace it.') }
    }
    if ($WhatIfPreference) {
        $null = $PSCmdlet.ShouldProcess($packageRoot, 'Restore, build, publish, validate, manifest, and package Windows Collector')
        $exitCode = $exitCodes.Success
        [pscustomobject]@{Status='WHATIF';ExitCode=0;PackageId=$PackageId;PackagePath=$packageRoot;ArchivePath=$(if($CreateZip){$zipPath}else{$null})}
        return
    }
    if (-not $PSCmdlet.ShouldProcess($packageRoot, 'Create validated Windows Collector deployment package')) {
        $exitCode = $exitCodes.Success
        [pscustomobject]@{Status='CANCELLED';ExitCode=0;PackageId=$PackageId;PackagePath=$packageRoot;ArchivePath=$null}
        return
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        $exitCode = $exitCodes.Validation; throw 'dotnet executable is unavailable.'
    }
    if ($Force) {
        if (Test-Path -LiteralPath $packageRoot) { Remove-Item -LiteralPath $packageRoot -Recurse -Force }
        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    }
    $null = New-Item -ItemType Directory -Path $runtimePath -Force
    foreach ($operation in @('Restore','Build','Publish')) {
        $arguments = New-CollectorDotNetArguments $operation $projectPath $Configuration $runtimePath
        $native = Invoke-CollectorNativeProcess dotnet $arguments
        if ($native.ExitCode -ne 0) { $exitCode = $exitCodes.Build; throw "dotnet $($operation.ToLowerInvariant()) failed." }
    }
    Get-ChildItem -LiteralPath $runtimePath -File -Filter 'appsettings.Development.json' -ErrorAction SilentlyContinue |
        Remove-Item -Force
    try {
        Test-CollectorPackageRuntime $runtimePath
    } catch { $exitCode = $exitCodes.Validation; throw }
    $commit = (& git -C $RepositoryRoot rev-parse HEAD 2>$null | Select-Object -First 1)
    $branch = (& git -C $RepositoryRoot branch --show-current 2>$null | Select-Object -First 1)
    $dirty = @(& git -C $RepositoryRoot status --porcelain 2>$null).Count -gt 0
    try {
        $manifest = New-CollectorPackageManifest $runtimePath $Configuration ([string]$commit) ([string]$branch) $dirty {
            param($path) (Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash
        }
        $manifest.PackageId = $PackageId
        $manifest | Add-Member -NotePropertyName FileOwnership -NotePropertyValue ([ordered]@{
            ProductOwned=@('package/**','appsettings.json')
            TargetPreserved=@('appsettings.Production.json','appsettings.Local.json','appsettings.*.local.json','logging*.local.json','service*.local.json','environment variables','certificates','logs','diagnostics','dumps','operator-created files')
            Excluded=@('source','.git','bin or obj outside publish output','test output','secrets','appsettings.Development.json','logs','dumps','diagnostics','previous-version directories')
        })
        $manifestPath = Join-Path $packageRoot 'deployment-manifest.json'
        [IO.File]::WriteAllText($manifestPath,($manifest|ConvertTo-Json -Depth 10),(New-Object Text.UTF8Encoding($false)))
    } catch { $exitCode = $exitCodes.Manifest; throw }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-CollectorPackage.ps1') -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CollectorDeployment.Common.ps1') -Destination $packageRoot
    @'
# PSM Windows Collector deployment package

Transfer this complete directory or its ZIP using an approved enterprise mechanism.
Run Install-CollectorPackage.ps1 locally in an elevated Windows PowerShell session.
The installer upgrades an existing service only; fresh installation is out of scope.
Target-owned configuration and arbitrary installation-directory content are preserved.
Rollback uses a sibling timestamped previous-version directory; no backup manifest is created.
'@ | Set-Content -LiteralPath (Join-Path $packageRoot 'README.md') -Encoding UTF8
    if ($CreateZip) {
        try {
            Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
            $verificationPath = Join-Path ([IO.Path]::GetTempPath()) ("PSMCollectorArchiveVerify-" + [guid]::NewGuid().ToString('N'))
            try {
                Expand-Archive -LiteralPath $zipPath -DestinationPath $verificationPath
                $verifiedManifest = Get-ChildItem -LiteralPath $verificationPath -Filter deployment-manifest.json -Recurse | Select-Object -First 1
                if ($null -eq $verifiedManifest) { throw 'Archive does not contain deployment-manifest.json.' }
                $null = Get-Content -LiteralPath $verifiedManifest.FullName -Raw | ConvertFrom-Json
            } finally {
                if (Test-Path -LiteralPath $verificationPath) { Remove-Item -LiteralPath $verificationPath -Recurse -Force }
            }
        } catch { $exitCode = $exitCodes.Archive; throw }
    }
    $exitCode = if ($dirty) { $exitCodes.Warning } else { $exitCodes.Success }
    [pscustomobject]@{Status=$(if($dirty){'SUCCESS_WITH_WARNING'}else{'SUCCESS'});ExitCode=$exitCode
        PackageId=$PackageId;PackagePath=$packageRoot;ArchivePath=$(if($CreateZip){$zipPath}else{$null})
        DirtyWorktree=$dirty;FileCount=$manifest.FileCount;TotalSizeBytes=$manifest.TotalSizeBytes}
} catch {
    if ($exitCode -eq $exitCodes.Unexpected -and $_.Exception -is [ArgumentException]) { $exitCode = $exitCodes.InvalidParameters }
    Write-Error $_.Exception.Message
} finally {
    if ($MyInvocation.InvocationName -ne '.') { exit $exitCode }
}
