$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$commonPath = Join-Path $repoRoot 'scripts\deployment\CollectorDeployment.Common.ps1'
$builderPath = Join-Path $repoRoot 'scripts\deployment\New-CollectorDeploymentPackage.ps1'
$installerPath = Join-Path $repoRoot 'scripts\deployment\Install-CollectorPackage.ps1'
$retiredPath = Join-Path $repoRoot 'scripts\deployment\Deploy-Collector.ps1'
. $commonPath

function Get-ScriptContract {
    param([string]$Path)
    $tokens=$null; $errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile($Path,[ref]$tokens,[ref]$errors)
    [pscustomobject]@{
        Errors=@($errors)
        Source=(Get-Content -LiteralPath $Path -Raw)
        Parameters=if ($null -ne $ast.ParamBlock) {
            @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
        } else { @() }
    }
}

function New-TestPackage {
    param([string]$Root)
    $runtime = Join-Path $Root 'package'
    $null = New-Item -ItemType Directory -Path $runtime -Force
    Set-Content -LiteralPath (Join-Path $runtime $script:CollectorExecutableName) -Value 'exe'
    Set-Content -LiteralPath (Join-Path $runtime $script:CollectorDllName) -Value 'dll'
    $manifest = New-CollectorPackageManifest $runtime Release ('a' * 40) main $false {
        param($path) (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    } ([DateTimeOffset]'2026-07-28T00:00:00Z')
    $manifest.PackageId = 'PSMWindowsCollector-test'
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $Root 'deployment-manifest.json')
    [pscustomobject]@{Runtime=$runtime;ManifestPath=(Join-Path $Root 'deployment-manifest.json');Manifest=$manifest}
}

Describe 'WP-006.8 package integrity' {
    It 'parses all deployment scripts in Windows PowerShell syntax' {
        foreach ($path in @($commonPath,$builderPath,$installerPath,$retiredPath)) {
            (Get-ScriptContract $path).Errors.Count | Should Be 0
        }
    }
    It 'validates a package manifest' {
        $package = New-TestPackage (Join-Path $TestDrive 'valid')
        $actual = Test-CollectorPackageManifest $package.Runtime $package.ManifestPath {
            param($path) (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
        $actual.PackageId | Should Be 'PSMWindowsCollector-test'
    }
    It 'rejects a package hash mismatch' {
        $package = New-TestPackage (Join-Path $TestDrive 'mismatch')
        Set-Content -LiteralPath (Join-Path $package.Runtime $script:CollectorDllName) -Value 'changed'
        { Test-CollectorPackageManifest $package.Runtime $package.ManifestPath {
            param($path) (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }} | Should Throw
    }
    It 'rejects unsafe manifest paths' {
        $package = New-TestPackage (Join-Path $TestDrive 'unsafe')
        $package.Manifest.Files[0].Path = '..\escape.exe'
        $package.Manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $package.ManifestPath
        { Test-CollectorPackageManifest $package.Runtime $package.ManifestPath { param($path) 'x' } } | Should Throw
    }
    It 'records hashes UTC source state configuration framework and PackageId' {
        $package = New-TestPackage (Join-Path $TestDrive 'fields')
        $manifest = $package.Manifest
        $manifest.CreatedAtUtc | Should Match 'Z$'
        $manifest.RepositoryCommit | Should Be ('a' * 40)
        $manifest.Configuration | Should Be Release
        $manifest.TargetFramework | Should Be net10.0
        $manifest.PackageId | Should Not BeNullOrEmpty
        @($manifest.Files)[0].Sha256 | Should Match '^[A-F0-9]{64}$'
    }
    It 'rejects embedded connection strings' {
        $root = Join-Path $TestDrive 'secret'; $package = New-TestPackage $root
        Set-Content -LiteralPath (Join-Path $package.Runtime 'appsettings.json') -Value '{"ConnectionStrings":{"OperationsDatabase":"secret"}}'
        { Test-CollectorPackageRuntime $package.Runtime } | Should Throw
    }
    It 'accepts the Microsoft UserSecrets runtime assembly' {
        $package = New-TestPackage (Join-Path $TestDrive 'usersecrets-assembly')
        Set-Content -LiteralPath (Join-Path $package.Runtime 'Microsoft.Extensions.Configuration.UserSecrets.dll') -Value 'runtime'
        { Test-CollectorPackageRuntime $package.Runtime } | Should Not Throw
    }
    It 'rejects secrets.json' {
        $package = New-TestPackage (Join-Path $TestDrive 'secrets-json')
        Set-Content -LiteralPath (Join-Path $package.Runtime 'secrets.json') -Value '{"secret":"value"}'
        { Test-CollectorPackageRuntime $package.Runtime } | Should Throw
    }
    It 'rejects appsettings.Development.json' {
        $package = New-TestPackage (Join-Path $TestDrive 'development-settings')
        Set-Content -LiteralPath (Join-Path $package.Runtime 'appsettings.Development.json') -Value '{}'
        { Test-CollectorPackageRuntime $package.Runtime } | Should Throw
    }
    It 'continues to accept ordinary runtime assemblies' {
        $package = New-TestPackage (Join-Path $TestDrive 'runtime-assemblies')
        Set-Content -LiteralPath (Join-Path $package.Runtime 'Microsoft.Extensions.Hosting.dll') -Value 'runtime'
        Set-Content -LiteralPath (Join-Path $package.Runtime 'PSMOperationsPlatform.Infrastructure.dll') -Value 'runtime'
        { Test-CollectorPackageRuntime $package.Runtime } | Should Not Throw
    }
    It 'excludes development settings in the builder' {
        (Get-ScriptContract $builderPath).Source | Should Match 'appsettings\.Development\.json'
    }
    It 'runs restore build and publish' {
        $source=(Get-ScriptContract $builderPath).Source
        $source | Should Match "'Restore','Build','Publish'"
    }
    It 'uses native argument arrays safely' {
        New-CollectorDotNetArguments Publish 'C:\repo path\x.csproj' Release 'C:\out path' |
            Should Be @('publish','C:\repo path\x.csproj','--configuration','Release','--framework','net10.0','--no-build','--output','C:\out path')
    }
    It 'supports optional ZIP creation and PackageId' {
        $parameters=(Get-ScriptContract $builderPath).Parameters
        $parameters -contains 'CreateZip' | Should Be $true
        $parameters -contains 'PackageId' | Should Be $true
    }
}

Describe 'WP-006.8 service and path discovery' {
    $named = [pscustomobject]@{Name='PSM Collector';PathName='"C:\PSM\PSMOperationsPlatform.WindowsCollector.exe"'}
    $fallback = [pscustomobject]@{Name='Different';PathName='C:\PSM\PSMOperationsPlatform.WindowsCollector.exe --service'}
    It 'uses an explicit service name containing spaces' {
        (Find-CollectorService 'PSM Collector' { @($named,$fallback) }).Name | Should Be 'PSM Collector'
    }
    It 'uses the default service name' {
        (Find-CollectorService -GetServices { @($named) }).Name | Should Be 'PSM Collector'
    }
    It 'falls back to exact executable filename matching' {
        (Find-CollectorService 'Missing' { @($fallback) }).Name | Should Be Different
    }
    It 'rejects zero service matches' {
        { Find-CollectorService 'Missing' { @([pscustomobject]@{Name='Other';PathName='C:\x\other.exe'}) } } | Should Throw
    }
    It 'rejects multiple executable matches' {
        { Find-CollectorService 'Missing' { @($fallback,[pscustomobject]@{Name='Two';PathName='"D:\x\PSMOperationsPlatform.WindowsCollector.exe"'}) } } | Should Throw
    }
    It 'parses a quoted PathName' {
        Resolve-CollectorServiceExecutable '"C:\Program Files\PSM\PSMOperationsPlatform.WindowsCollector.exe" --service' |
            Should Be 'C:\Program Files\PSM\PSMOperationsPlatform.WindowsCollector.exe'
    }
    It 'parses an unquoted PathName' {
        Resolve-CollectorServiceExecutable 'C:\Program Files\PSM\PSMOperationsPlatform.WindowsCollector.exe --service' |
            Should Be 'C:\Program Files\PSM\PSMOperationsPlatform.WindowsCollector.exe'
    }
    It 'discovers the installation directory from the executable' {
        [IO.Path]::GetDirectoryName((Resolve-CollectorServiceExecutable $named.PathName)) | Should Be 'C:\PSM'
    }
}

Describe 'WP-006.8 configuration and staging' {
    It 'preserves target-owned configuration allowlist entries' {
        Test-CollectorPreservedConfigurationPath appsettings.Production.json | Should Be $true
        Test-CollectorPreservedConfigurationPath appsettings.Local.json | Should Be $true
        Test-CollectorPreservedConfigurationPath appsettings.Site.local.json | Should Be $true
        Test-CollectorPreservedConfigurationPath logging.ops.local.json | Should Be $true
    }
    It 'does not classify primary appsettings as target-owned' {
        Test-CollectorPreservedConfigurationPath appsettings.json | Should Be $false
    }
    It 'rejects ambiguous configuration' {
        $root=Join-Path $TestDrive 'ambiguous';$null=New-Item -ItemType Directory $root
        Set-Content -LiteralPath (Join-Path $root 'appsettings.Custom.json') -Value '{}'
        { Get-CollectorConfigurationPlan $root } | Should Throw
    }
    It 'returns preserved configuration without scalar array collapse' {
        $root=Join-Path $TestDrive 'single';$null=New-Item -ItemType Directory $root
        Set-Content -LiteralPath (Join-Path $root 'appsettings.Local.json') -Value '{}'
        $plan=Get-CollectorConfigurationPlan $root
        @($plan.PreservedFiles).Count | Should Be 1
    }
    It 'generates same-volume sibling staging and previous paths' {
        $paths=New-CollectorSiblingPaths 'C:\Program Files\PSM' abc ([DateTimeOffset]'2026-07-28T06:17:51Z')
        $paths.StagingPath | Should Be 'C:\Program Files\PSM_Staging_abc'
        $paths.PreviousPath | Should Be 'C:\Program Files\PSM_Previous_20260728T061751Z'
        [IO.Path]::GetPathRoot($paths.StagingPath) | Should Be ([IO.Path]::GetPathRoot('C:\Program Files\PSM'))
    }
    It 'uses a filesystem-safe previous-directory name' {
        (New-CollectorSiblingPaths 'C:\PSM' abc ([DateTimeOffset]'2026-07-28T06:17:51Z')).PreviousPath |
            Should Match '_Previous_\d{8}T\d{6}Z$'
    }
}

Describe 'WP-006.8 replacement rollback and health contract' {
    BeforeAll { $source=(Get-ScriptContract $installerPath).Source }
    It 'handles an already stopped service' { $source | Should Match "ServiceRecord\.State, 'Stopped'" }
    It 'requests one graceful stop and polls state' {
        $source | Should Match 'MethodName StopService'
        $source | Should Match 'Wait-CollectorServiceState'
        $source | Should Not Match 'Stop-Process|taskkill'
    }
    It 'maps service stop timeout independently' { (Get-CollectorDeploymentExitCodes).ServiceStop | Should Be 6 }
    It 'stages before stopping the service' {
        $source.IndexOf("'TargetStaging'") | Should BeLessThan $source.IndexOf("'ServiceStop'")
    }
    It 'moves product-managed files and preserves arbitrary files' {
        $source | Should Match 'Get-CollectorProductManagedPaths'
        $source | Should Not Match 'Move-Item\s+-LiteralPath\s+\$installPath'
    }
    It 'marks replacement only when active replacement begins' {
        $source.IndexOf('$replacementStarted = $true') | Should BeGreaterThan $source.IndexOf("'ActiveDeployment'")
    }
    It 'implements start timeout and stability failure checks' {
        $source | Should Match 'StartTimeoutSeconds'
        $source | Should Match 'did not remain stable'
    }
    It 'verifies active hashes and executable existence' {
        $source | Should Match 'Test-CollectorFilesAgainstManifest'
        $source | Should Match 'CollectorExecutableName'
    }
    It 'inspects only local Application startup events' {
        $source | Should Match 'Get-WinEvent'
        $source | Should Match "LogName='Application'"
    }
    It 'implements successful rollback stability validation' {
        $source | Should Match "RollbackStatus = 'SUCCESS'"
        $source | Should Match 'Restored service is not stable'
    }
    It 'retains exact manual recovery paths on rollback failure' {
        $source | Should Match "Manual recovery paths:"
        $source | Should Match "RollbackStatus = 'FAILED'"
    }
    It 'applies strict previous-version retention after health validation' {
        $source.IndexOf("'RetentionCleanup'") | Should BeGreaterThan $source.IndexOf("'HealthValidation' 'PASS'")
        $source | Should Match '_Previous_\\d\{8\}T\\d\{6\}Z'
        $source | Should Match '\[Math\]::Max\(1, \$KeepPreviousCount\)'
    }
    It 'turns retention cleanup failure into a warning' {
        $source | Should Match 'deployment remains successful'
    }
    It 'implements active and stale deployment lock behavior' {
        $source | Should Match 'deployment\.lock'
        $source | Should Match 'TotalHours -lt \$StaleLockHours'
        (Get-CollectorDeploymentExitCodes).Concurrency | Should Be 11
    }
    It 'WhatIf returns before lock and mutation' {
        $source.IndexOf('if ($WhatIfPreference)') | Should BeLessThan $source.IndexOf("'Lock' 'START'")
    }
    It 'Force changes confirmation only' {
        $source.IndexOf("if (`$Force) { `$ConfirmPreference = 'None' }") | Should BeGreaterThan -1
    }
    It 'uses simple arrays without compatibility-sensitive generic collections' {
        $source | Should Not Match 'List\[|AddRange|System\.Collections\.Generic'
        (Get-Content -LiteralPath $commonPath -Raw) | Should Not Match 'List\[|AddRange|System\.Collections\.Generic'
    }
    It 'emits detailed safe failure diagnostics' {
        $common=Get-Content -LiteralPath $commonPath -Raw
        foreach ($field in @('CurrentPhase','ExceptionType','FullyQualifiedErrorId','ScriptLineNumber','InvocationLine','StackTrace','InnerException')) {
            $common | Should Match $field
        }
    }
    It 'does not log environment values or accept secrets' {
        $source | Should Not Match 'GetEnvironmentVariable|Credential|Password|ConnectionString'
    }
    It 'does not modify service account database AD SPN or use remoting' {
        $source | Should Not Match 'Set-Service|sc\.exe|New-Service|StartName\s*=|SqlConnection|Invoke-Sqlcmd|dotnet\s+ef|database\s+update|Set-AD|setspn|PSSession|Invoke-Command'
    }
    It 'does not contact SQL for health validation' {
        $source | Should Not Match 'ManagedServer|OperationsDb|SQL Server'
    }
    It 'has deterministic documented exit codes' {
        $codes=Get-CollectorDeploymentExitCodes
        $codes.Success|Should Be 0;$codes.Warning|Should Be 1;$codes.PackageValidation|Should Be 2
        $codes.ServiceDiscovery|Should Be 3;$codes.Configuration|Should Be 4;$codes.Staging|Should Be 5
        $codes.ServiceStop|Should Be 6;$codes.ActiveDeployment|Should Be 7;$codes.ServiceHealth|Should Be 8
        $codes.RollbackSucceeded|Should Be 9;$codes.RollbackFailed|Should Be 10
        $codes.Concurrency|Should Be 11;$codes.InvalidParameters|Should Be 12;$codes.Unexpected|Should Be 13
    }
}

Describe 'WP-006.8 simplified authority' {
    It 'contains no backup manifest or generic backup engine' {
        $all=(Get-Content $commonPath -Raw)+(Get-Content $installerPath -Raw)
        $all | Should Not Match 'backup-manifest|CreateBackup|PruneBackups|KeepBackup'
    }
    It 'retires the competing installer entry point' {
        (Get-Content $retiredPath -Raw) | Should Match 'has been retired'
    }
    It 'retires the legacy publish install update and remote orchestrator entry points' {
        foreach ($name in @('Publish-PSMWindowsCollector.ps1','Install-PSMWindowsCollector.ps1',
            'Update-PSMWindowsCollector.ps1','Invoke-PSMWindowsCollectorDeployment.ps1')) {
            (Get-Content (Join-Path $repoRoot "tools\deployment\$name") -Raw) | Should Match 'retired by WP-006\.8'
        }
    }
}
