$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$releaseRoot=Join-Path $repoRoot 'scripts\release'
. (Join-Path $releaseRoot 'PSMOperationsSqlPackage.Common.ps1')

function New-SqlPackageFixture {
    $root=Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
    $expectationDirectory=Join-Path $root 'tools\deployment'
    $null=New-Item -ItemType Directory -Path $expectationDirectory
    Copy-Item -LiteralPath (Join-Path $repoRoot 'tools\deployment\PSMOperationsDatabaseSchemaExpectation.json') `
        -Destination $expectationDirectory
    [pscustomobject]@{
        Root=$root
        Output=Join-Path $root 'Release\Database'
        Calls=New-Object System.Collections.Generic.List[object]
    }
}

function New-SqlPackageOperations {
    param(
        [Parameter(Mandatory)]$Fixture,
        [string]$FailureCommand,
        [switch]$EmptySql
    )
    $fixtureState=$Fixture
    $failurePattern=$FailureCommand
    $writeEmpty=[bool]$EmptySql
    $invokeNative={
            param($file,$arguments)
            $argumentList=@($arguments|ForEach-Object{[string]$_})
            $fixtureState.Calls.Add([pscustomobject]@{File=$file;Arguments=$argumentList})
            $command="$file $($argumentList -join ' ')"
            if($failurePattern -and $command -match $failurePattern){
                return [pscustomobject]@{ExitCode=1;Output=@('safe failure')}
            }
            if($file -eq 'git' -and $argumentList -contains 'status'){
                return [pscustomobject]@{ExitCode=0;Output=@()}
            }
            if($file -eq 'git' -and $argumentList -contains 'rev-parse'){
                return [pscustomobject]@{ExitCode=0;Output=@('0123456789abcdef0123456789abcdef01234567')}
            }
            if($file -eq 'git' -and $argumentList -contains 'show'){
                return [pscustomobject]@{ExitCode=0;Output=@('2026-07-30T12:34:56+03:00')}
            }
            if($argumentList -contains 'script'){
                $outputIndex=[Array]::IndexOf($argumentList,'--output')
                $sqlPath=$argumentList[$outputIndex+1]
                $sqlValue=if($writeEmpty){" `r`n"}else{"SELECT 1;`nGO`n"}
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($sqlPath))|Out-Null
                [IO.File]::WriteAllText($sqlPath,$sqlValue)
            }
            [pscustomobject]@{ExitCode=0;Output=@()}
        }.GetNewClosure()
    @{
        InvokeNative=$invokeNative
        WriteText={
            param($path,$value)
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))|Out-Null
            [IO.File]::WriteAllText($path,$value,[Text.UTF8Encoding]::new($false))
        }
        GetHash={param($path)'A'*64}
    }
}

Describe 'WP-009.1 SQL release package' {
    It 'accepts semantic release versions and rejects unsafe values' {
        (Test-PSMOperationsReleaseVersion '1.2.3')|Should Be $true
        (Test-PSMOperationsReleaseVersion '1.2.3-rc.1')|Should Be $true
        (Test-PSMOperationsReleaseVersion '../1.2.3')|Should Be $false
        (Test-PSMOperationsReleaseVersion '1.2')|Should Be $false
    }

    It 'uses exact deterministic package filenames' {
        $names=Get-PSMOperationsSqlPackageNames '1.2.3-rc1'
        $names.SqlFile|Should Be 'PSMOperations-v1.2.3-rc1.sql'
        $names.ChecksumFile|Should Be 'Checksums.sha256'
        $names.ManifestFile|Should Be 'Manifest.json'
    }

    It 'normalizes SQL to CRLF with one terminal newline' {
        (ConvertTo-PSMOperationsDeterministicSql "SELECT 1;`nGO`n`n")|Should Be "SELECT 1;`r`nGO`r`n"
    }

    It 'creates SQL checksum and manifest without environment data' {
        $fixture=New-SqlPackageFixture
        $result=Invoke-PSMOperationsSqlPackageBuild '1.2.3' $fixture.Root $fixture.Output `
            (New-SqlPackageOperations $fixture)
        (Test-Path $result.SqlPath)|Should Be $true
        (Test-Path $result.ChecksumPath)|Should Be $true
        (Test-Path $result.ManifestPath)|Should Be $true
        (Get-Content -Raw $result.ChecksumPath)|Should Be (
            "A"*64+" *PSMOperations-v1.2.3.sql`r`n"+"A"*64+" *Manifest.json`r`n")
        $manifest=Get-Content -Raw $result.ManifestPath|ConvertFrom-Json
        $manifest.ProductVersion|Should Be '1.2.3'
        $manifest.BuildDate|Should Be '2026-07-30T09:34:56Z'
        $manifest.GitCommit|Should Be '0123456789abcdef0123456789abcdef01234567'
        $manifest.SQLScriptName|Should Be 'PSMOperations-v1.2.3.sql'
        $manifest.SHA256|Should Be ('A'*64)
        @($manifest.psobject.Properties.Name)|Should Be @(
            'ProductVersion','BuildDate','GitCommit','SQLScriptName','Sha256')
        (Get-Content -Raw $result.ManifestPath)|Should Not Match '(?i)testdrive|password|connectionstring'
    }

    It 'passes idempotent and offline EF arguments' {
        $fixture=New-SqlPackageFixture
        $null=Invoke-PSMOperationsSqlPackageBuild '1.2.3' $fixture.Root $fixture.Output `
            (New-SqlPackageOperations $fixture)
        $commands=@($fixture.Calls|ForEach-Object{"$($_.File) $($_.Arguments -join ' ')"})
        ($commands -join "`n")|Should Match 'dotnet ef migrations has-pending-model-changes'
        ($commands -join "`n")|Should Match 'dotnet ef migrations script --idempotent --output'
        ($commands -join "`n")|Should Not Match '(?i)database update|connection|string|migrate'
    }

    It 'fails when pending-model validation fails' {
        $fixture=New-SqlPackageFixture
        {Invoke-PSMOperationsSqlPackageBuild '1.2.3' $fixture.Root $fixture.Output `
            (New-SqlPackageOperations $fixture 'has-pending-model-changes')}|Should Throw
    }

    It 'fails when migration script generation fails' {
        $fixture=New-SqlPackageFixture
        {Invoke-PSMOperationsSqlPackageBuild '1.2.3' $fixture.Root $fixture.Output `
            (New-SqlPackageOperations $fixture 'migrations script')}|Should Throw
    }

    It 'fails when generated SQL is empty' {
        $fixture=New-SqlPackageFixture
        {Invoke-PSMOperationsSqlPackageBuild '1.2.3' $fixture.Root $fixture.Output `
            (New-SqlPackageOperations $fixture -EmptySql)}|Should Throw
    }

    It 'contains no automatic migration or database execution path' {
        $scripts=(Get-Content -Raw (Join-Path $releaseRoot 'PSMOperationsSqlPackage.Common.ps1'))+
            (Get-Content -Raw (Join-Path $releaseRoot 'Build-PSMOperationsSqlPackage.ps1'))+
            (Get-Content -Raw (Join-Path $releaseRoot 'Publish-PSMOperationsSqlPackage.ps1'))
        $scripts|Should Not Match 'Database\.Migrate\s*\('
        $scripts|Should Not Match 'EnsureCreated\s*\('
        $scripts|Should Not Match '(?i)\bdatabase\s+update\b'
        $scripts|Should Not Match '(?i)\bsqlcmd\b|Invoke-Sqlcmd|SqlConnection'
    }

    It 'publishes only the three package files in CI' {
        $workflow=Get-Content -Raw (Join-Path $repoRoot '.github\workflows\release-sql-package.yml')
        $workflow|Should Match 'actions/upload-artifact@v4'
        $workflow|Should Match 'PSMOperations-v\$\{\{ steps\.version\.outputs\.value \}\}\.sql'
        $workflow|Should Match 'Release/Database/Checksums\.sha256'
        $workflow|Should Match 'Release/Database/Manifest\.json'
        $workflow|Should Match 'if-no-files-found: error'
        $workflow|Should Not Match '(?i)sqlcmd|Invoke-Sqlcmd|database update'
    }
}
