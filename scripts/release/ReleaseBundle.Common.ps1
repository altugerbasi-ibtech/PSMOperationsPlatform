#requires -Version 5.1
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'PSMOperationsSqlPackage.Common.ps1')

function Write-PSMReleaseText {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Value)
    [IO.File]::WriteAllText($Path,$Value,[Text.UTF8Encoding]::new($false))
}

function Get-PSMReleaseRelativePath {
    param([Parameter(Mandatory)][string]$Root,[Parameter(Mandatory)][string]$Path)
    $rootUri=[Uri]([IO.Path]::GetFullPath($Root).TrimEnd('\')+'\')
    $pathUri=[Uri]([IO.Path]::GetFullPath($Path))
    [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString())
}

function Assert-PSMReleaseChildPath {
    param([Parameter(Mandatory)][string]$ReleaseRoot,[Parameter(Mandatory)][string]$Path)
    $release=[IO.Path]::GetFullPath($ReleaseRoot).TrimEnd('\')+'\'
    $candidate=[IO.Path]::GetFullPath($Path)
    if(-not $candidate.StartsWith($release,[StringComparison]::OrdinalIgnoreCase) -or
       [IO.Path]::GetDirectoryName($candidate).TrimEnd('\') -ne $release.TrimEnd('\')){
        throw 'Generated output path is not a direct child of the Release directory.'
    }
}

function Invoke-PSMReleaseNativeCommand {
    param([Parameter(Mandatory)][string]$FilePath,[Parameter(Mandatory)][object[]]$Arguments)
    $output=@(& $FilePath @Arguments 2>&1)
    [pscustomobject]@{ExitCode=$LASTEXITCODE;Output=@($output|ForEach-Object{[string]$_})}
}

function Invoke-PSMOperationsReleaseBundleBuild {
    param(
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [hashtable]$Operations
    )
    if(-not (Test-PSMOperationsReleaseVersion $Version)){throw 'Release version is invalid.'}
    $root=[IO.Path]::GetFullPath($RepositoryRoot)
    if(-not (Test-Path -LiteralPath $root -PathType Container)){throw 'Repository root does not exist.'}
    $releaseRoot=Join-Path $root 'Release'
    $databaseRoot=Join-Path $releaseRoot 'Database'
    $collectorRoot=Join-Path $releaseRoot 'Collector'
    $portalRoot=Join-Path $releaseRoot 'Portal'
    $documentationRoot=Join-Path $releaseRoot 'Documentation'

    $requiredSources=@(
        'Release\Database\DeploymentGuide.md',
        'Release\Database\SchemaValidation.sql',
        'Release\Database\SchemaValidation.md',
        'Release\Database\PermissionValidation.sql',
        'Release\Database\PermissionValidation.md',
        'Release\Verification\Verify-WinRM.ps1',
        'Release\Verification\Verify-SPN.ps1',
        'Release\Verification\Verify-gMSA.ps1',
        'Release\Verification\Verify-Network.ps1',
        'Release\Verification\Verify-SQL.ps1',
        'Release\Verification\Verification.Common.ps1',
        'Release\Verification\VerificationGuide.md',
        'Release\Acceptance\RAT.Common.ps1',
        'Release\Acceptance\Invoke-ReleaseAcceptanceTest.ps1',
        'Release\Acceptance\RATGuide.md',
        'src\PSMOperationsPlatform.WindowsCollector\PSMOperationsPlatform.WindowsCollector.csproj',
        'src\PSMOperationsPlatform.Web\PSMOperationsPlatform.Web.csproj',
        'Release\ReleaseGuide.md',
        'RELEASE.md'
    )
    foreach($relative in $requiredSources){
        if(-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)){
            throw "Required release source is missing: $relative"
        }
    }

    if(-not $Operations){
        $Operations=@{
            InvokeNative={param($file,$arguments)Invoke-PSMReleaseNativeCommand $file $arguments}
            BuildSql={
                param($bundleVersion,$bundleRoot,$bundleOutput)
                Invoke-PSMOperationsSqlPackageBuild $bundleVersion $bundleRoot $bundleOutput
            }
        }
    }

    foreach($output in @($collectorRoot,$portalRoot,$documentationRoot)){
        Assert-PSMReleaseChildPath $releaseRoot $output
        if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output -Recurse -Force}
        $null=New-Item -ItemType Directory -Path $output
    }
    foreach($rootArtifact in @('Manifest.json','Checksums.sha256','Version.txt')){
        $path=Join-Path $releaseRoot $rootArtifact
        if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}
    }

    $sqlResult=& $Operations.BuildSql $Version $root $databaseRoot
    if($null -eq $sqlResult){throw 'Database release package generation failed.'}

    $collectorProject=Join-Path $root 'src\PSMOperationsPlatform.WindowsCollector\PSMOperationsPlatform.WindowsCollector.csproj'
    $portalProject=Join-Path $root 'src\PSMOperationsPlatform.Web\PSMOperationsPlatform.Web.csproj'
    $collectorPublish=& $Operations.InvokeNative 'dotnet' @(
        'publish',$collectorProject,'--configuration','Release','--output',$collectorRoot)
    if([int]$collectorPublish.ExitCode -ne 0){throw 'Collector publish failed.'}
    $portalPublish=& $Operations.InvokeNative 'dotnet' @(
        'publish',$portalProject,'--configuration','Release','--output',$portalRoot)
    if([int]$portalPublish.ExitCode -ne 0){throw 'Portal publish failed.'}

    $documentationMap=[ordered]@{
        'RELEASE.md'='ReleaseStatus.md'
        'Release\Database\DeploymentGuide.md'='DatabaseDeploymentGuide.md'
        'Release\Database\SchemaValidation.md'='SchemaValidation.md'
        'Release\Database\PermissionValidation.md'='PermissionValidation.md'
        'Release\Verification\VerificationGuide.md'='VerificationGuide.md'
        'Release\Acceptance\RATGuide.md'='RATGuide.md'
        'Release\ReleaseGuide.md'='ReleaseGuide.md'
    }
    foreach($entry in $documentationMap.GetEnumerator()){
        $source=Join-Path $root $entry.Key
        if(-not (Test-Path -LiteralPath $source -PathType Leaf)){
            throw "Required documentation is missing: $($entry.Key)"
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $documentationRoot $entry.Value)
    }

    $commitResult=& $Operations.InvokeNative 'git' @('-C',$root,'rev-parse','HEAD')
    if([int]$commitResult.ExitCode -ne 0 -or [string]$commitResult.Output[0] -notmatch '^[0-9a-fA-F]{40}$'){
        throw 'Unable to resolve release Git commit.'
    }
    $gitCommit=[string]$commitResult.Output[0]
    $dateResult=& $Operations.InvokeNative 'git' @('-C',$root,'show','-s','--format=%cI',$gitCommit)
    $parsedDate=[DateTimeOffset]::MinValue
    if([int]$dateResult.ExitCode -ne 0 -or -not [DateTimeOffset]::TryParse(
        [string]$dateResult.Output[0],[Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind,[ref]$parsedDate)){
        throw 'Unable to resolve release build date.'
    }
    $buildDate=$parsedDate.UtcDateTime.ToString(
        'yyyy-MM-ddTHH:mm:ssZ',[Globalization.CultureInfo]::InvariantCulture)

    Write-PSMReleaseText (Join-Path $releaseRoot 'Version.txt') "$Version`r`n"
    $summary=@"
# Deployment Summary

- Product version: $Version
- Git commit: $gitCommit
- Build date: $buildDate
- Database package: included
- Collector publish output: included
- Portal publish output: included
- Verification package: included
- Deployment documentation: included
- Live deployment and verification: not executed

Deployment order: verify checksums, review manifest, deploy the database under
DBA change control, deploy Collector and Portal through approved procedures,
then execute the Verification Guide. Human release approval remains external.
"@
    Write-PSMReleaseText (Join-Path $documentationRoot 'DeploymentSummary.md') `
        (($summary -replace "`r?`n","`r`n").TrimEnd()+"`r`n")

    $requiredOutputs=@(
        "Database\PSMOperations-v$Version.sql",
        'Database\Manifest.json',
        'Database\Checksums.sha256',
        'Collector\PSMOperationsPlatform.WindowsCollector.dll',
        'Portal\PSMOperationsPlatform.Web.dll',
        'Verification\Verify-WinRM.ps1',
        'Verification\Verify-SPN.ps1',
        'Verification\Verify-gMSA.ps1',
        'Verification\Verify-Network.ps1',
        'Verification\Verify-SQL.ps1',
        'Acceptance\RAT.Common.ps1',
        'Acceptance\Invoke-ReleaseAcceptanceTest.ps1',
        'Acceptance\RATGuide.md',
        'Documentation\DeploymentSummary.md',
        'Version.txt'
    )
    foreach($relative in $requiredOutputs){
        if(-not (Test-Path -LiteralPath (Join-Path $releaseRoot $relative) -PathType Leaf)){
            throw "Required release artifact is missing: $relative"
        }
    }

    $rootManifest=Join-Path $releaseRoot 'Manifest.json'
    $rootChecksums=Join-Path $releaseRoot 'Checksums.sha256'
    $payloadFiles=@(Get-ChildItem -LiteralPath $releaseRoot -Recurse -File|
        Where-Object{$_.FullName -notin @($rootManifest,$rootChecksums)}|
        Sort-Object{Get-PSMReleaseRelativePath $releaseRoot $_.FullName})
    $artifacts=@($payloadFiles|ForEach-Object{
        [pscustomobject][ordered]@{
            Path=Get-PSMReleaseRelativePath $releaseRoot $_.FullName
            Size=$_.Length
            SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        }
    })
    $manifest=[pscustomobject][ordered]@{
        Product='PSM Operations Platform'
        ProductVersion=$Version
        BuildDate=$buildDate
        GitCommit=$gitCommit
        Artifacts=$artifacts
    }
    Write-PSMReleaseText $rootManifest (($manifest|ConvertTo-Json -Depth 6)+"`r`n")

    $checksumFiles=@(Get-ChildItem -LiteralPath $releaseRoot -Recurse -File|
        Where-Object{$_.FullName -ne $rootChecksums}|
        Sort-Object{Get-PSMReleaseRelativePath $releaseRoot $_.FullName})
    $checksumLines=@($checksumFiles|ForEach-Object{
        $hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        "$hash *$(Get-PSMReleaseRelativePath $releaseRoot $_.FullName)"
    })
    Write-PSMReleaseText $rootChecksums (($checksumLines -join "`r`n")+"`r`n")

    [pscustomobject][ordered]@{
        Version=$Version
        GitCommit=$gitCommit
        BuildDate=$buildDate
        ReleaseRoot=$releaseRoot
        ManifestPath=$rootManifest
        ChecksumsPath=$rootChecksums
        ArtifactCount=$artifacts.Count
    }
}
