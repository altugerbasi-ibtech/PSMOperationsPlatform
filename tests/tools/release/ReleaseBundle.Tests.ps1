$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$releaseScripts=Join-Path $repoRoot 'scripts\release'
. (Join-Path $releaseScripts 'ReleaseBundle.Common.ps1')

function New-ReleaseBundleFixture {
    $root=Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
    $required=@(
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
        'src\PSMOperationsPlatform.WindowsCollector\PSMOperationsPlatform.WindowsCollector.csproj',
        'src\PSMOperationsPlatform.Web\PSMOperationsPlatform.Web.csproj',
        'Release\ReleaseGuide.md',
        'RELEASE.md'
    )
    foreach($relative in $required){
        $source=Join-Path $repoRoot $relative
        $destination=Join-Path $root $relative
        $null=New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force
        Copy-Item -LiteralPath $source -Destination $destination
    }
    $root
}

function New-ReleaseBundleOperations {
    param([Parameter(Mandatory)][string]$FixtureRoot)
    $rootState=$FixtureRoot
    $invoke={
        param($file,$arguments)
        $argsList=@($arguments|ForEach-Object{[string]$_})
        if($file -eq 'git' -and $argsList -contains 'rev-parse'){
            return [pscustomobject]@{ExitCode=0;Output=@('0123456789abcdef0123456789abcdef01234567')}
        }
        if($file -eq 'git' -and $argsList -contains 'show'){
            return [pscustomobject]@{ExitCode=0;Output=@('2026-07-30T12:34:56+03:00')}
        }
        if($file -eq 'dotnet' -and $argsList -contains 'publish'){
            $outputIndex=[Array]::IndexOf($argsList,'--output')
            $output=$argsList[$outputIndex+1]
            $null=New-Item -ItemType Directory -Path $output -Force
            $name=if(($argsList -join ' ') -match 'WindowsCollector'){
                'PSMOperationsPlatform.WindowsCollector.dll'
            }else{'PSMOperationsPlatform.Web.dll'}
            [IO.File]::WriteAllText((Join-Path $output $name),'published')
            return [pscustomobject]@{ExitCode=0;Output=@()}
        }
        [pscustomobject]@{ExitCode=1;Output=@('unexpected command')}
    }.GetNewClosure()
    $buildSql={
        param($version,$root,$output)
        $null=New-Item -ItemType Directory -Path $output -Force
        [IO.File]::WriteAllText((Join-Path $output "PSMOperations-v$version.sql"),'SELECT 1;')
        [IO.File]::WriteAllText((Join-Path $output 'Manifest.json'),'{}')
        [IO.File]::WriteAllText((Join-Path $output 'Checksums.sha256'),'A')
        [pscustomobject]@{Version=$version}
    }.GetNewClosure()
    @{InvokeNative=$invoke;BuildSql=$buildSql}
}

Describe 'WP-009.6 release bundle generator' {
    It 'contains syntactically valid entry and common scripts' {
        foreach($path in @(
            (Join-Path $repoRoot 'Build-Release.ps1'),
            (Join-Path $releaseScripts 'ReleaseBundle.Common.ps1'))){
            $tokens=$null
            $errors=$null
            [void][Management.Automation.Language.Parser]::ParseFile(
                $path,[ref]$tokens,[ref]$errors)
            @($errors).Count|Should Be 0
        }
    }

    It 'builds the complete bundle manifest checksums version and summary' {
        $fixture=New-ReleaseBundleFixture
        $result=Invoke-PSMOperationsReleaseBundleBuild '1.2.3' $fixture `
            (New-ReleaseBundleOperations $fixture)
        foreach($relative in @(
            'Database','Verification','Collector','Portal','Documentation',
            'Manifest.json','Checksums.sha256','Version.txt',
            'Documentation\DeploymentSummary.md')){
            Test-Path -LiteralPath (Join-Path $fixture "Release\$relative")|Should Be $true
        }
        (Get-Content -Raw (Join-Path $fixture 'Release\Version.txt'))|Should Be "1.2.3`r`n"
        $manifest=Get-Content -Raw (Join-Path $fixture 'Release\Manifest.json')|ConvertFrom-Json
        $manifest.ProductVersion|Should Be '1.2.3'
        $manifest.BuildDate|Should Be '2026-07-30T09:34:56Z'
        $manifest.GitCommit|Should Be '0123456789abcdef0123456789abcdef01234567'
        @($manifest.Artifacts).Count|Should BeGreaterThan 10
        @($manifest.Artifacts|Where-Object{$_.Path -eq 'Collector/PSMOperationsPlatform.WindowsCollector.dll'}).Count|Should Be 1
        @($manifest.Artifacts|Where-Object{$_.Path -eq 'Portal/PSMOperationsPlatform.Web.dll'}).Count|Should Be 1
        $checksums=Get-Content -Raw (Join-Path $fixture 'Release\Checksums.sha256')
        $checksums|Should Match '\*Manifest\.json'
        $checksums|Should Match '\*Version\.txt'
        $checksums|Should Not Match '(?m)\*Checksums\.sha256$'
        $result.ArtifactCount|Should Be @($manifest.Artifacts).Count
    }

    It 'fails before generation when a required source is missing' {
        $fixture=New-ReleaseBundleFixture
        Remove-Item -LiteralPath (Join-Path $fixture 'Release\Verification\Verify-SQL.ps1')
        {Invoke-PSMOperationsReleaseBundleBuild '1.2.3' $fixture `
            (New-ReleaseBundleOperations $fixture)}|Should Throw
    }

    It 'contains required failure gates and bounded output cleanup' {
        $common=Get-Content -Raw (Join-Path $releaseScripts 'ReleaseBundle.Common.ps1')
        $common|Should Match 'Required release source is missing'
        $common|Should Match 'Required release artifact is missing'
        $common|Should Match 'Collector publish failed'
        $common|Should Match 'Portal publish failed'
        $common|Should Match 'Assert-PSMReleaseChildPath'
        $common|Should Match 'Get-FileHash'
        $common|Should Match 'Manifest\.json'
        $common|Should Match 'Checksums\.sha256'
    }

    It 'documents one-command generation layout failures and safety boundary' {
        $guide=Get-Content -Raw (Join-Path $repoRoot 'Release\ReleaseGuide.md')
        foreach($term in @(
            '.\Build-Release.ps1 -Version','Database/','Verification/',
            'Collector/','Portal/','Documentation/','Manifest.json',
            'Checksums.sha256','Version.txt','DeploymentSummary.md',
            'Failure Behavior','contacts\s+no\s+target\s+server')){
            if($term -eq 'contacts\s+no\s+target\s+server'){
                $guide|Should Match $term
            }else{
                $guide|Should Match ([regex]::Escape($term))
            }
        }
    }
}
