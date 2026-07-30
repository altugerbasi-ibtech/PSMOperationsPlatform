#requires -Version 5.1
Set-StrictMode -Version Latest

$script:InfrastructureProject='src\PSMOperationsPlatform.Infrastructure\PSMOperationsPlatform.Infrastructure.csproj'
$script:StartupProject='src\PSMOperationsPlatform.WindowsCollector\PSMOperationsPlatform.WindowsCollector.csproj'
$script:DbContext='OperationsDbContext'

function Test-PSMOperationsReleaseVersion {
    param([Parameter(Mandatory)][string]$Version)
    $Version -match '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$'
}

function Get-PSMOperationsSqlPackageNames {
    param([Parameter(Mandatory)][string]$Version)
    if(-not (Test-PSMOperationsReleaseVersion $Version)){throw 'Release version is invalid.'}
    $baseName="PSMOperations-v$Version"
    [pscustomobject][ordered]@{
        SqlFile="$baseName.sql"
        ChecksumFile='Checksums.sha256'
        ManifestFile='Manifest.json'
    }
}

function Invoke-PSMOperationsNativeCommand {
    param([Parameter(Mandatory)][string]$FilePath,[Parameter(Mandatory)][object[]]$Arguments)
    $output=@(& $FilePath @Arguments 2>&1)
    [pscustomobject]@{ExitCode=$LASTEXITCODE;Output=@($output|ForEach-Object{[string]$_})}
}

function Assert-PSMOperationsNativeSuccess {
    param($Result,[Parameter(Mandatory)][string]$FailureMessage)
    if($null -eq $Result -or [int]$Result.ExitCode -ne 0){throw $FailureMessage}
}

function Get-PSMOperationsSha256 {
    param([Parameter(Mandatory)][string]$Path)
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function ConvertTo-PSMOperationsDeterministicSql {
    param([Parameter(Mandatory)][string]$Sql)
    $normalized=($Sql -replace "`r`n","`n" -replace "`r","`n").TrimEnd("`n")
    if([string]::IsNullOrWhiteSpace($normalized)){throw 'Generated SQL output is empty.'}
    ($normalized -replace "`n","`r`n")+"`r`n"
}

function New-PSMOperationsSqlPackageManifest {
    param(
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$GitCommit,
        [Parameter(Mandatory)][string]$BuildDate,
        [Parameter(Mandatory)]$Names,
        [Parameter(Mandatory)][string]$Sha256,
        [Parameter(Mandatory)]$Expectation
    )
    [pscustomobject][ordered]@{
        ProductVersion=$Version
        BuildDate=$BuildDate
        GitCommit=$GitCommit
        SQLScriptName=$Names.SqlFile
        Sha256=$Sha256
    }
}

function Invoke-PSMOperationsSqlPackageBuild {
    param(
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$OutputDirectory,
        [hashtable]$Operations
    )
    if(-not (Test-PSMOperationsReleaseVersion $Version)){throw 'Release version is invalid.'}
    if(-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)){throw 'Repository root does not exist.'}
    $repositoryRootPath=[IO.Path]::GetFullPath($RepositoryRoot)
    $expectationPath=Join-Path $repositoryRootPath 'tools\deployment\PSMOperationsDatabaseSchemaExpectation.json'
    if(-not (Test-Path -LiteralPath $expectationPath -PathType Leaf)){throw 'Migration expectation is missing.'}
    $expectation=Get-Content -Raw -LiteralPath $expectationPath|ConvertFrom-Json
    if(@($expectation.expectedMigrations).Count -eq 0 -or
        [string]$expectation.latestMigration -ne [string]$expectation.expectedMigrations[-1]){
        throw 'Migration expectation is invalid.'
    }
    if(-not $Operations){
        $Operations=@{
            InvokeNative={param($file,$arguments)Invoke-PSMOperationsNativeCommand $file $arguments}
            WriteText={param($path,$value)[IO.File]::WriteAllText($path,$value,[Text.UTF8Encoding]::new($false))}
            GetHash={param($path)Get-PSMOperationsSha256 $path}
        }
    }
    $statusResult=& $Operations.InvokeNative 'git' @('-C',$repositoryRootPath,'status','--porcelain')
    Assert-PSMOperationsNativeSuccess $statusResult 'Unable to inspect repository source state.'
    if(@($statusResult.Output|Where-Object{-not [string]::IsNullOrWhiteSpace($_)}).Count){
        throw 'Repository source state is dirty.'
    }
    $commitResult=& $Operations.InvokeNative 'git' @('-C',$repositoryRootPath,'rev-parse','HEAD')
    Assert-PSMOperationsNativeSuccess $commitResult 'Unable to resolve repository source commit.'
    $sourceCommit=[string]$commitResult.Output[0]
    if($sourceCommit -notmatch '^[0-9a-fA-F]{40}$'){throw 'Repository source commit is invalid.'}
    $dateResult=& $Operations.InvokeNative 'git' @(
        '-C',$repositoryRootPath,'show','-s','--format=%cI',$sourceCommit)
    Assert-PSMOperationsNativeSuccess $dateResult 'Unable to resolve repository source date.'
    $sourceDate=[string]$dateResult.Output[0]
    $parsedSourceDate=[DateTimeOffset]::MinValue
    if(-not [DateTimeOffset]::TryParse(
        $sourceDate,[Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind,[ref]$parsedSourceDate)){
        throw 'Repository source date is invalid.'
    }
    $buildDate=$parsedSourceDate.UtcDateTime.ToString(
        'yyyy-MM-ddTHH:mm:ssZ',[Globalization.CultureInfo]::InvariantCulture)

    $solution=Join-Path $repositoryRootPath 'PSMOperationsPlatform.sln'
    Assert-PSMOperationsNativeSuccess (& $Operations.InvokeNative 'dotnet' @('restore',$solution)) 'Release restore failed.'
    Assert-PSMOperationsNativeSuccess (& $Operations.InvokeNative 'dotnet' @(
        'build',$solution,'--configuration','Release','--no-restore')) 'Release build failed.'

    $project=Join-Path $repositoryRootPath $script:InfrastructureProject
    $startup=Join-Path $repositoryRootPath $script:StartupProject
    $efBase=@('--project',$project,'--startup-project',$startup,'--context',$script:DbContext,
        '--configuration','Release','--no-build')
    $pendingArguments=@('ef','migrations','has-pending-model-changes')+$efBase
    $pendingResult=& $Operations.InvokeNative 'dotnet' $pendingArguments
    Assert-PSMOperationsNativeSuccess $pendingResult 'Pending model changes or EF model validation failure.'

    if(-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)){
        $null=New-Item -ItemType Directory -Path $OutputDirectory
    }
    $outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
    $names=Get-PSMOperationsSqlPackageNames $Version
    $sqlPath=Join-Path $outputRoot $names.SqlFile
    $scriptArguments=@('ef','migrations','script','--idempotent','--output',$sqlPath)+$efBase
    $scriptResult=& $Operations.InvokeNative 'dotnet' $scriptArguments
    Assert-PSMOperationsNativeSuccess $scriptResult 'Idempotent migration script generation failed.'
    if(-not (Test-Path -LiteralPath $sqlPath -PathType Leaf)){throw 'Generated SQL output is missing.'}
    $sql=ConvertTo-PSMOperationsDeterministicSql (Get-Content -Raw -LiteralPath $sqlPath)
    & $Operations.WriteText $sqlPath $sql
    $sha=[string](& $Operations.GetHash $sqlPath)
    if($sha -notmatch '^[0-9A-F]{64}$'){throw 'SQL checksum generation failed.'}
    $manifest=New-PSMOperationsSqlPackageManifest `
        $Version $sourceCommit $buildDate $names $sha $expectation
    $manifestPath=Join-Path $outputRoot $names.ManifestFile
    & $Operations.WriteText $manifestPath (($manifest|ConvertTo-Json -Depth 5)+"`r`n")
    $manifestSha=[string](& $Operations.GetHash $manifestPath)
    if($manifestSha -notmatch '^[0-9A-F]{64}$'){throw 'Manifest checksum generation failed.'}
    $checksumPath=Join-Path $outputRoot $names.ChecksumFile
    $checksums="$sha *$($names.SqlFile)`r`n$manifestSha *$($names.ManifestFile)`r`n"
    & $Operations.WriteText $checksumPath $checksums
    [pscustomobject][ordered]@{
        Version=$Version;GitCommit=$sourceCommit;BuildDate=$buildDate;SqlPath=$sqlPath
        ChecksumPath=$checksumPath;ManifestPath=$manifestPath;Sha256=$sha
    }
}
