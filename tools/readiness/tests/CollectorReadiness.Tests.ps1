$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
@(
    'Readiness.Common.ps1', 'CollectorHostValidation.ps1',
    'DotNetValidation.ps1', 'CollectorFilesValidation.ps1',
    'ConfigurationValidation.ps1', 'ServiceValidation.ps1',
    'GmsaValidation.ps1', 'NetworkValidation.ps1',
    'WinRmValidation.ps1', 'SqlValidation.ps1'
) | ForEach-Object { . (Join-Path $root $_) }

function New-TestCheck {
    param([string]$Id,[string]$Status,[bool]$Mandatory=$true,[bool]$Blocking=$true)
    New-ReadinessCheck -CheckId $Id -Category Runtime -Name $Id -Status $Status `
        -Severity INFO -Summary 'test' -Evidence 'safe' -Recommendation $null `
        -IsBlocking $Blocking -IsMandatory $Mandatory -DurationMilliseconds 1
}

function New-TestParameters {
    @{
        Mode='SmokeTest'; CollectorInstallPath='C:\PSM\WindowsCollector'
        CollectorServiceName='PSMWindowsCollector'; TargetFqdn='target.example.test'
        TransportPolicy='Auto'; WinRmHttpsPort=5986; WinRmHttpPort=5985
        SqlServer='sql.example.test'; SqlPort=1433; DatabaseName='PSM_Smoke'
        ExpectedServiceAccount='EXAMPLE\gmsaCollector$'
        SkipSqlAuthenticationTest=$false; SkipWinRmAuthenticationTest=$false
    }
}

function New-TestTable {
    param([hashtable]$Values)
    $table = New-Object System.Data.DataTable
    foreach ($key in $Values.Keys) { $null = $table.Columns.Add($key) }
    $row = $table.NewRow()
    foreach ($key in $Values.Keys) { $row[$key] = $Values[$key] }
    $table.Rows.Add($row)
    $table
}

function New-TestManifestContext {
    @{
        CollectorVersion=$null; CollectorServiceName='svc'
        CollectorInstallPath='c:\x'; TargetFqdn=$null
        TransportPolicy=$null; SqlServer=$null; DatabaseName=$null
    }
}

function New-TestHostOperations {
    param([int]$BuildNumber, [string]$Caption)
    $operatingSystem = @{
        Version="10.0.$BuildNumber"; BuildNumber=[string]$BuildNumber
        OSArchitecture='64-bit'; Caption=$Caption
    }
    @{
        GetComputerSystem = {
            @{
                Name='COLLECTOR01'; Domain='example.test'; PartOfDomain=$true
                TotalPhysicalMemory=8589934592; NumberOfLogicalProcessors=4
            }
        }
        GetOperatingSystem = { $operatingSystem }.GetNewClosure()
        GetTimeZone = { @{Id='Turkey Standard Time'} }
        GetTimeService = { @{Status='Running'} }
        GetVolume = { param($path) @{FreeSpace=10737418240;Size=21474836480} }
    }
}

Describe 'Core result model and aggregation' {
    It 'accepts all allowed status values' {
        foreach ($status in @('PASS','WARNING','FAIL','SKIPPED','NOT_APPLICABLE')) {
            $id = 'TEST.STATUS.' + $status.Replace('_','')
            (New-TestCheck $id $status $false $false).Status | Should Be $status
        }
    }
    It 'rejects an unknown status' {
        { New-ReadinessCheck -CheckId TEST.STATUS.INVALID -Category Runtime -Name test `
            -Status BROKEN -Severity INFO -Summary test -Evidence safe -Recommendation $null `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0 } | Should Throw
    }
    It 'accepts all allowed severity values' {
        foreach ($severity in @('INFO','LOW','MEDIUM','HIGH','CRITICAL')) {
            (New-ReadinessCheck -CheckId "TEST.SEVERITY.$severity" -Category Runtime -Name test `
                -Status PASS -Severity $severity -Summary test -Evidence safe -Recommendation $null `
                -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0).Severity | Should Be $severity
        }
    }
    It 'calculates READY and exit code 0' {
        $status = Get-ReadinessStatus @(New-TestCheck TEST.READY PASS)
        $status | Should Be READY
        (Get-ReadinessExitCode $status) | Should Be 0
    }
    It 'calculates WARNING and exit code 1' {
        $status = Get-ReadinessStatus @((New-TestCheck TEST.PASS PASS),(New-TestCheck TEST.WARNING WARNING $false $false))
        $status | Should Be WARNING
        (Get-ReadinessExitCode $status) | Should Be 1
    }
    It 'calculates NOT_READY and exit code 2 for any fail' {
        $status = Get-ReadinessStatus @((New-TestCheck TEST.PASS PASS),(New-TestCheck TEST.FAIL FAIL $false $false))
        $status | Should Be NOT_READY
        (Get-ReadinessExitCode $status) | Should Be 2
    }
    It 'calculates NOT_READY for mandatory skipped' {
        Get-ReadinessStatus @((New-TestCheck TEST.SKIPPED SKIPPED $true $true)) | Should Be NOT_READY
    }
    It 'does not downgrade for optional skipped' {
        Get-ReadinessStatus @((New-TestCheck TEST.PASS PASS),(New-TestCheck TEST.SKIPPED SKIPPED $false $false)) | Should Be READY
    }
    It 'orders checks deterministically' {
        $context = New-TestManifestContext
        $manifest = New-ReadinessManifest CollectorHost $context @((New-TestCheck TEST.Z PASS),(New-TestCheck TEST.A PASS)) ([datetime]'2026-07-27T12:00:00+03:00')
        $manifest.Checks[0].CheckId | Should Be TEST.A
    }
    It 'aggregates category status deterministically' {
        $categories = Get-ReadinessCategories @((New-TestCheck TEST.CATEGORY.WARNING WARNING $false $false))
        ($categories | Where-Object Name -eq Runtime).Status | Should Be WARNING
        ($categories | Where-Object Name -eq SQL).Status | Should Be NOT_APPLICABLE
    }
    It 'normalizes unexpected errors to NOT_READY' {
        $check = New-InternalErrorCheck FRAMEWORK.INTERNAL.ERROR CollectorHost framework
        (Get-ReadinessExitCode (Get-ReadinessStatus @($check))) | Should Be 2
    }
}

Describe 'Readiness check collection normalization' {
    It 'normalizes zero checks' {
        [object[]]$result = @(ConvertTo-ReadinessCheckArray @())
        $result.Count | Should Be 0
    }
    It 'normalizes one check' {
        [object[]]$result = @(ConvertTo-ReadinessCheckArray (New-TestCheck TEST.ONE PASS))
        $result.Count | Should Be 1
        $result[0].CheckId | Should Be TEST.ONE
    }
    It 'normalizes multiple checks and preserves their order' {
        [object[]]$result = @(ConvertTo-ReadinessCheckArray @(
            (New-TestCheck TEST.Z PASS), (New-TestCheck TEST.A PASS)))
        ($result.CheckId -join ',') | Should Be 'TEST.Z,TEST.A'
    }
    It 'normalizes a generic List object without fragile array conversion' {
        $list = New-Object System.Collections.Generic.List[object]
        $list.Add((New-TestCheck TEST.LIST.Z PASS))
        $list.Add((New-TestCheck TEST.LIST.A PASS))
        $manifest = New-ReadinessManifest CollectorHost (New-TestManifestContext) `
            $list ([datetime]'2026-07-27T12:00:00+03:00')
        ($manifest.Checks.CheckId -join ',') | Should Be 'TEST.LIST.A,TEST.LIST.Z'
    }
    It 'normalizes an object array' {
        [object[]]$checks = @((New-TestCheck TEST.ARRAY PASS))
        (New-ReadinessManifest CollectorHost (New-TestManifestContext) `
            $checks ([datetime]'2026-07-27T12:00:00+03:00')).Checks.Count | Should Be 1
    }
    It 'normalizes an ArrayList' {
        $checks = New-Object System.Collections.ArrayList
        $null = $checks.Add((New-TestCheck TEST.ARRAYLIST PASS))
        (New-ReadinessManifest CollectorHost (New-TestManifestContext) `
            $checks ([datetime]'2026-07-27T12:00:00+03:00')).Checks.Count | Should Be 1
    }
    It 'creates a NOT_READY manifest for zero checks without changing the schema' {
        $manifest = New-ReadinessManifest -Mode CollectorHost `
            -Context (New-TestManifestContext) -Checks @() `
            -GeneratedAt ([datetime]'2026-07-27T12:00:00+03:00')
        $manifest.OverallStatus | Should Be NOT_READY
        $manifest.ExitCode | Should Be 2
        ($manifest.psobject.Properties.Name -join ',') | Should Be `
            'SchemaVersion,FrameworkName,FrameworkVersion,GeneratedAt,GeneratedOnMachine,ExecutingIdentity,PowerShellVersion,OperatingSystem,Mode,CollectorVersion,CollectorServiceName,CollectorInstallPath,TargetFqdn,TransportPolicy,SqlServer,DatabaseName,Categories,OverallStatus,ExitCode,Checks'
    }
}

Describe 'Manifest, JSON, Markdown and redaction' {
    $context = @{CollectorVersion=$null;CollectorServiceName='svc';CollectorInstallPath='c:\x';TargetFqdn='target.example.test';TransportPolicy='Auto';SqlServer='sql.example.test';DatabaseName='db'}
    $checks = @((New-TestCheck TEST.FAIL FAIL))
    $manifest = New-ReadinessManifest SmokeTest $context $checks ([datetime]'2026-07-27T12:00:00+03:00')
    It 'uses schema and framework versions' {
        $manifest.SchemaVersion | Should Be '1.0'
        $manifest.FrameworkVersion | Should Be '1.0.0'
    }
    It 'keeps unavailable collector version null' { $manifest.CollectorVersion | Should Be $null }
    It 'produces valid JSON with required fields' {
        $roundTrip = $manifest | ConvertTo-Json -Depth 8 | ConvertFrom-Json
        $roundTrip.SchemaVersion | Should Be '1.0'
        $roundTrip.Checks[0].CheckId | Should Be TEST.FAIL
    }
    It 'redacts passwords and raw connection strings' {
        Protect-ReadinessText 'Password=hunter2' | Should Be '[REDACTED]'
        Protect-ReadinessText 'Server=x;Database=y;Integrated Security=True' | Should Be '[REDACTED]'
    }
    It 'does not over-redact server and database names' {
        Protect-ReadinessText 'Server=x; Database=y' | Should Be 'Server=x; Database=y'
    }
    It 'produces all required Markdown sections and recommendations' {
        $markdown = ConvertTo-ReadinessMarkdown $manifest
        foreach ($heading in @('## Manifest','## Overall Result','## Category Summary',
            '## Blocking Failures','## Warnings','## Detailed Checks',
            '## Required Manual Actions','## Security and Redaction Confirmation','## Execution Scope')) {
            $markdown | Should Match ([regex]::Escape($heading))
        }
    }
    It 'writes reports only to an existing directory' {
        $paths = Write-ReadinessReports $manifest $TestDrive $true $true
        Test-Path $paths.JsonPath | Should Be $true
        Test-Path $paths.MarkdownPath | Should Be $true
    }
    It 'rejects a missing output directory' {
        { Write-ReadinessReports $manifest (Join-Path $TestDrive missing) $true $true } | Should Throw
    }
    It 'writes JSON and Markdown from a generic List input' {
        $list = New-Object System.Collections.Generic.List[object]
        $list.Add((New-TestCheck TEST.REPORT PASS))
        $genericManifest = New-ReadinessManifest CollectorHost `
            (New-TestManifestContext) $list ([datetime]'2026-07-27T12:00:00+03:00')
        $paths = Write-ReadinessReports $genericManifest $TestDrive $true $true
        (Get-Content -Raw $paths.JsonPath | ConvertFrom-Json).Checks[0].CheckId |
            Should Be TEST.REPORT
        (Get-Content -Raw $paths.MarkdownPath) | Should Match 'TEST\.REPORT'
    }
    It 'does not pass a null manifest to report writing after construction failure' {
        Mock New-ReadinessManifest { throw 'Password=do-not-expose' }
        Mock Write-ReadinessReports { throw 'must not execute' }
        $result = Complete-ReadinessRun CollectorHost (New-TestManifestContext) `
            @() ([datetime]'2026-07-27T12:00:00+03:00') $TestDrive $true $true
        Assert-MockCalled Write-ReadinessReports 0
        $result.Manifest | Should Be $null
        $result.Checks.Count | Should Be 1
        $result.Checks[0].Status | Should Be FAIL
        $result.OverallStatus | Should Be NOT_READY
        $result.ExitCode | Should Be 2
        ($result | ConvertTo-Json -Depth 8) | Should Not Match 'do-not-expose|Password'
    }
}

Describe 'Runtime and collector files' {
    It 'passes when required runtime is present' {
        $ops = @{TestPath={param($p)$true};GetCommand={@{Source='dotnet.exe'}};GetRuntimes={ 'Microsoft.NETCore.App 10.0.1 [c:\dotnet]' };GetInfo={ 'Architecture: x64' }}
        (Test-DotNetReadiness (New-TestParameters) $ops | Where-Object CheckId -eq DOTNET.RUNTIME.REQUIRED).Status | Should Be PASS
    }
    It 'fails when required runtime is missing' {
        $ops = @{TestPath={param($p)$true};GetCommand={@{Source='dotnet.exe'}};GetRuntimes={ 'Microsoft.NETCore.App 9.0.1 [c:\dotnet]' };GetInfo={ 'Architecture: x64' }}
        (Test-DotNetReadiness (New-TestParameters) $ops | Where-Object CheckId -eq DOTNET.RUNTIME.REQUIRED).Status | Should Be FAIL
    }
    It 'fails wrong runtime architecture' {
        $ops = @{TestPath={param($p)$true};GetCommand={@{Source='dotnet.exe'}};GetRuntimes={ 'Microsoft.NETCore.App 10.0.1 [c:\dotnet]' };GetInfo={ 'Architecture: x86' }}
        (Test-DotNetReadiness (New-TestParameters) $ops | Where-Object CheckId -eq DOTNET.ARCHITECTURE).Status | Should Be FAIL
    }
    It 'passes self-contained deployment without dotnet' {
        $ops = @{TestPath={param($p)$false};GetCommand={$null};GetRuntimes={};GetInfo={}}
        (Test-DotNetReadiness (New-TestParameters) $ops).Status | Should Be PASS
    }
    It 'fails missing install path' {
        $ops = @{TestPath={param($p,$t)$false};GetVersion={param($p)$null}}
        (Test-CollectorFilesReadiness (New-TestParameters) $ops)[0].Status | Should Be FAIL
    }
    It 'warns when version is unavailable' {
        $ops = @{TestPath={param($p,$t)$true};GetVersion={param($p)$null}}
        (Test-CollectorFilesReadiness (New-TestParameters) $ops | Where-Object CheckId -eq FILES.VERSION).Status | Should Be WARNING
    }
}

Describe 'Configuration, service and identity' {
    It 'classifies missing optional configuration files separately from missing values' {
        $ops = @{
            TestPath={param($p)$false}; GetContent={throw 'must not execute'}
            GetEnvironment={param($n)$null}
        }
        $checks = Test-ConfigurationReadiness (New-TestParameters) $ops
        ($checks | Where-Object CheckId -eq CONFIG.FILE.BASE).Status |
            Should Be NOT_APPLICABLE
        ($checks | Where-Object CheckId -eq CONFIG.FILE.BASE).Evidence |
            Should Match 'Status: Not found'
        ($checks | Where-Object CheckId -eq CONFIG.OPERATIONSDATABASE).Status |
            Should Be FAIL
    }
    It 'classifies an unreadable configuration file without calling it invalid JSON' {
        $ops = @{
            TestPath={param($p) $p -like '*appsettings.json'}
            GetContent={param($p) throw [System.UnauthorizedAccessException]::new()}
            GetEnvironment={param($n)$null}
        }
        $check = Test-ConfigurationReadiness (New-TestParameters) $ops |
            Where-Object CheckId -eq CONFIG.FILE.BASE
        $check.Status | Should Be FAIL
        $check.Summary | Should Match 'cannot be opened'
        $check.Evidence | Should Match 'Status: Unreadable'
        $check.Summary | Should Not Match 'invalid JSON'
    }
    It 'classifies invalid JSON precisely' {
        $ops = @{
            TestPath={param($p) $p -like '*appsettings.json'}
            GetContent={param($p)'{'}
            GetEnvironment={param($n)$null}
        }
        $check = Test-ConfigurationReadiness (New-TestParameters) $ops |
            Where-Object CheckId -eq CONFIG.FILE.BASE
        $check.Status | Should Be FAIL
        $check.Summary | Should Match 'invalid JSON'
        $check.Evidence | Should Match 'Status: Invalid JSON'
    }
    It 'accepts valid JSON without an optional ConnectionStrings section' {
        $ops = @{
            TestPath={param($p) $p -like '*appsettings.json'}
            GetContent={param($p)'{"Logging":{"LogLevel":{"Default":"Information"}}}'}
            GetEnvironment={param($n)
                if($n -like 'PSM__*') {
                    'Server=s;Database=d;Integrated Security=True'
                } else {$null}
            }
        }
        $checks = Test-ConfigurationReadiness (New-TestParameters) $ops
        ($checks | Where-Object CheckId -eq CONFIG.FILE.BASE).Status |
            Should Be PASS
        @($checks | Where-Object {
            $_.Summary -match 'invalid JSON'
        }).Count | Should Be 0
        ($checks | Where-Object CheckId -eq CONFIG.OPERATIONSDATABASE).Status |
            Should Be PASS
    }
    It 'accepts environment-variable-only configuration and reports its source' {
        $secretValue = 'Server=sql;Database=db;Integrated Security=True;Application Name=private-marker'
        $ops = @{
            TestPath={param($p)$false}
            GetContent={throw 'must not execute'}
            GetEnvironment={param($n)$null}
            GetMachineEnvironment={param($n) $secretValue}
        }
        $checks = Test-ConfigurationReadiness (New-TestParameters) $ops
        $check = $checks | Where-Object CheckId -eq CONFIG.OPERATIONSDATABASE
        $check.Status | Should Be PASS
        $check.Summary | Should Match 'Environment Variable'
        $check.Evidence | Should Match 'Provider: Machine'
        $check.Evidence | Should Match 'PSM__ConnectionStrings__OperationsDatabase'
        ($checks | ConvertTo-Json -Depth 8) | Should Not Match 'private-marker'
    }
    It 'applies environment-variable precedence over JSON without reporting a conflict' {
        $ops = @{
            TestPath={param($p) $p -like '*appsettings.json'}
            GetContent={param($p)'{"ConnectionStrings":{"OperationsDatabase":"Server=json;Database=jsondb;Integrated Security=True"}}'}
            GetEnvironment={param($n)$null}
            GetMachineEnvironment={
                param($n)'Server=env;Database=envdb;Integrated Security=True'
            }
        }
        $checks = Test-ConfigurationReadiness (New-TestParameters) $ops
        $source = $checks | Where-Object CheckId -eq CONFIG.OPERATIONSDATABASE
        $source.Summary | Should Match 'Environment Variable'
        $source.Evidence | Should Match 'Provider: Machine'
        ($checks | Where-Object CheckId -eq CONFIG.SOURCE.PRECEDENCE).Status |
            Should Be PASS
        ($checks | Where-Object CheckId -eq CONFIG.SQL.AUTHENTICATION).Status |
            Should Be PASS
    }
    It 'applies environment-specific JSON precedence over base appsettings JSON' {
        $ops = @{
            TestPath={param($p)$true}
            GetContent={param($p)
                if($p -like '*appsettings.Production.json') {
                    '{"ConnectionStrings":{"OperationsDatabase":"Server=environmentjson;Database=db;Integrated Security=True"}}'
                } else {
                    '{"ConnectionStrings":{"OperationsDatabase":"Server=basejson;Database=db;Integrated Security=True"}}'
                }
            }
            GetEnvironment={param($n)$null}
        }
        $checks = Test-ConfigurationReadiness (New-TestParameters) $ops
        ($checks | Where-Object CheckId -eq CONFIG.OPERATIONSDATABASE).Summary |
            Should Match 'appsettings.Production.json'
        ($checks | Where-Object CheckId -eq CONFIG.SQL.AUTHENTICATION).Status |
            Should Be PASS
    }
    It 'fails missing OperationsDatabase' {
        $ops = @{TestPath={param($p)$false};GetContent={};GetEnvironment={param($n)$null}}
        (Test-ConfigurationReadiness (New-TestParameters) $ops | Where-Object CheckId -eq CONFIG.OPERATIONSDATABASE).Status | Should Be FAIL
    }
    It 'rejects SQL Authentication' {
        $ops = @{TestPath={param($p)$false};GetContent={};GetEnvironment={param($n) if($n -like 'PSM__*'){'Server=s;Database=d;User ID=u;Password=p'} else {$null}}}
        (Test-ConfigurationReadiness (New-TestParameters) $ops | Where-Object CheckId -eq CONFIG.SQL.AUTHENTICATION).Status | Should Be FAIL
    }
    It 'accepts Integrated Authentication without exposing the raw value' {
        $ops = @{TestPath={param($p)$false};GetContent={};GetEnvironment={param($n) if($n -like 'PSM__*'){'Server=s;Database=d;Integrated Security=True;Encrypt=True'} else {$null}}}
        $check = Test-ConfigurationReadiness (New-TestParameters) $ops | Where-Object CheckId -eq CONFIG.SQL.AUTHENTICATION
        $check.Status | Should Be PASS
        $check.Evidence | Should Not Match 'Integrated Security=True;Encrypt=True'
    }
    It 'rejects unsafe credential material in JSON' {
        $ops = @{TestPath={param($p) $p -like '*appsettings.json'};GetContent={param($p)'{"Password":"secret"}'};GetEnvironment={param($n)$null}}
        (Test-ConfigurationReadiness (New-TestParameters) $ops | Where-Object CheckId -eq CONFIG.SECRETS.UNSAFE).Status | Should Be FAIL
    }
    It 'fails when service is missing' {
        $ops = @{GetService={param($n)$null};GetProcessPath={}}
        (Test-ServiceReadiness (New-TestParameters) $ops)[0].Status | Should Be FAIL
    }
    It 'detects service account mismatch' {
        $svc = @{Name='PSMWindowsCollector';DisplayName='PSM';StartMode='Auto';State='Stopped';StartName='EXAMPLE\wrong$';PathName='"C:\PSM\WindowsCollector\PSMOperationsPlatform.WindowsCollector.exe"';ProcessId=0}
        $ops = @{GetService={param($n)$svc};GetProcessPath={}}
        (Test-ServiceReadiness (New-TestParameters) $ops | Where-Object CheckId -eq SERVICE.ACCOUNT).Status | Should Be FAIL
    }
    It 'detects service binary path mismatch' {
        $svc = @{Name='PSMWindowsCollector';DisplayName='PSM';StartMode='Auto';State='Stopped';StartName='EXAMPLE\gmsaCollector$';PathName='"C:\Other\collector.exe"';ProcessId=0}
        $ops = @{GetService={param($n)$svc};GetProcessPath={}}
        (Test-ServiceReadiness (New-TestParameters) $ops | Where-Object CheckId -eq SERVICE.BINARYPATH).Status | Should Be FAIL
    }
    It 'treats stopped service as warning without changing it' {
        $svc = @{Name='PSMWindowsCollector';DisplayName='PSM';StartMode='Auto';State='Stopped';StartName='EXAMPLE\gmsaCollector$';PathName='"C:\PSM\WindowsCollector\PSMOperationsPlatform.WindowsCollector.exe"';ProcessId=0}
        $ops = @{GetService={param($n)$svc};GetProcessPath={}}
        (Test-ServiceReadiness (New-TestParameters) $ops | Where-Object CheckId -eq SERVICE.STATE).Status | Should Be WARNING
    }
    It 'passes valid gMSA and marks non-gMSA not applicable' {
        $ops = @{GetAdCommand={@{Name='Test-ADServiceAccount'}};TestAdAccount={param($n)$true};GetCurrentIdentity={'EXAMPLE\gmsaCollector$'}}
        (Test-IdentityReadiness (New-TestParameters) $ops | Where-Object CheckId -eq IDENTITY.GMSA).Status | Should Be PASS
        $p = New-TestParameters; $p.ExpectedServiceAccount='EXAMPLE\service'
        (Test-IdentityReadiness $p $ops | Where-Object CheckId -eq IDENTITY.GMSA).Status | Should Be NOT_APPLICABLE
    }
    It 'warns when AD module is unavailable' {
        $ops = @{GetAdCommand={$null};TestAdAccount={};GetCurrentIdentity={'EXAMPLE\operator'}}
        (Test-IdentityReadiness (New-TestParameters) $ops | Where-Object CheckId -eq IDENTITY.GMSA).Status | Should Be WARNING
    }
    It 'fails when gMSA validation fails' {
        $ops = @{GetAdCommand={@{Name='Test-ADServiceAccount'}};TestAdAccount={param($n)$false};GetCurrentIdentity={'EXAMPLE\operator'}}
        (Test-IdentityReadiness (New-TestParameters) $ops | Where-Object CheckId -eq IDENTITY.GMSA).Status | Should Be FAIL
    }
}

Describe 'Supported production and controlled-lab platform policy' {
    It 'warns for Windows Server 2019 support but passes SmokeTest platform validation' {
        $parameters = New-TestParameters
        $checks = Test-CollectorHostReadiness $parameters `
            (New-TestHostOperations 17763 'Microsoft Windows Server 2019')
        ($checks | Where-Object CheckId -eq HOST.OS.SUPPORTED).Status |
            Should Be WARNING
        ($checks | Where-Object CheckId -eq HOST.OS.MODE).Status | Should Be PASS
        $status = Get-ReadinessStatus $checks
        $status | Should Be WARNING
        (Get-ReadinessExitCode $status) | Should Be 1
    }
    It 'passes Windows Server 2019 CollectorHost deployment validation with a production warning' {
        $parameters = New-TestParameters
        $parameters.Mode = 'CollectorHost'
        $checks = Test-CollectorHostReadiness $parameters `
            (New-TestHostOperations 17763 'Microsoft Windows Server 2019')
        $supportCheck = $checks | Where-Object CheckId -eq HOST.OS.SUPPORTED
        $modeCheck = $checks | Where-Object CheckId -eq HOST.OS.MODE
        $supportCheck.Status | Should Be WARNING
        $supportCheck.IsBlocking | Should Be $false
        $modeCheck.Status | Should Be PASS
        @($checks | Where-Object {
            $_.CheckId -like 'HOST.OS.*' -and $_.Status -eq 'FAIL'
        }).Count | Should Be 0
        $status = Get-ReadinessStatus $checks
        $status | Should Be WARNING
        (Get-ReadinessExitCode $status) | Should Be 1
        (Get-ReadinessCategories $checks |
            Where-Object Name -eq CollectorHost).Status | Should Be WARNING
    }
    It 'passes Windows Server 2022 production and smoke-test policies' {
        foreach ($mode in @('CollectorHost','SmokeTest')) {
            $parameters = New-TestParameters
            $parameters.Mode = $mode
            $checks = Test-CollectorHostReadiness $parameters `
                (New-TestHostOperations 20348 'Microsoft Windows Server 2022')
            ($checks | Where-Object CheckId -eq HOST.OS.SUPPORTED).Status |
                Should Be PASS
            ($checks | Where-Object CheckId -eq HOST.OS.MODE).Status |
                Should Be PASS
            Get-ReadinessStatus $checks | Should Be READY
        }
    }
    It 'passes Windows Server 2025 production and smoke-test policies' {
        foreach ($mode in @('CollectorHost','SmokeTest')) {
            $parameters = New-TestParameters
            $parameters.Mode = $mode
            $checks = Test-CollectorHostReadiness $parameters `
                (New-TestHostOperations 26100 'Microsoft Windows Server 2025')
            ($checks | Where-Object CheckId -eq HOST.OS.SUPPORTED).Status |
                Should Be PASS
            ($checks | Where-Object CheckId -eq HOST.OS.MODE).Status |
                Should Be PASS
            (Get-ReadinessExitCode (Get-ReadinessStatus $checks)) | Should Be 0
        }
    }
    It 'fails platforms older than Windows Server 2019 in every mode' {
        foreach ($mode in @('CollectorHost','SmokeTest')) {
            $parameters = New-TestParameters
            $parameters.Mode = $mode
            $checks = Test-CollectorHostReadiness $parameters `
                (New-TestHostOperations 14393 'Microsoft Windows Server 2016')
            ($checks | Where-Object CheckId -eq HOST.OS.SUPPORTED).Status |
                Should Be FAIL
            ($checks | Where-Object CheckId -eq HOST.OS.MODE).Status |
                Should Be FAIL
            Get-ReadinessStatus $checks | Should Be NOT_READY
        }
    }
    It 'fails a non-server Windows operating system' {
        $parameters = New-TestParameters
        $parameters.Mode = 'CollectorHost'
        $checks = Test-CollectorHostReadiness $parameters `
            (New-TestHostOperations 26100 'Microsoft Windows 11 Enterprise')
        ($checks | Where-Object CheckId -eq HOST.OS.SUPPORTED).Status |
            Should Be FAIL
        ($checks | Where-Object CheckId -eq HOST.OS.MODE).Status |
            Should Be FAIL
    }
    It 'does not let the production warning suppress an unrelated blocking failure' {
        $parameters = New-TestParameters
        $parameters.Mode = 'CollectorHost'
        [object[]]$checks = @(Test-CollectorHostReadiness $parameters `
            (New-TestHostOperations 17763 'Microsoft Windows Server 2019'))
        $checks += New-TestCheck TEST.UNRELATED.FAIL FAIL
        Get-ReadinessStatus $checks | Should Be NOT_READY
        (Get-ReadinessExitCode (Get-ReadinessStatus $checks)) | Should Be 2
    }
    It 'keeps platform checks deterministic and JSON schema-compatible' {
        $parameters = New-TestParameters
        $parameters.Mode = 'CollectorHost'
        $checks = Test-CollectorHostReadiness $parameters `
            (New-TestHostOperations 17763 'Microsoft Windows Server 2019')
        $manifest = New-ReadinessManifest -Mode CollectorHost `
            -Context (New-TestManifestContext) -Checks $checks `
            -GeneratedAt ([datetime]'2026-07-27T12:00:00+03:00')
        $platformIds = @($manifest.Checks | Where-Object {
            $_.CheckId -like 'HOST.OS.*'
        } | Select-Object -ExpandProperty CheckId)
        ($platformIds -join ',') | Should Be 'HOST.OS.MODE,HOST.OS.SUPPORTED'
        $roundTrip = $manifest | ConvertTo-Json -Depth 8 | ConvertFrom-Json
        $roundTrip.SchemaVersion | Should Be '1.0'
        $roundTrip.OverallStatus | Should Be WARNING
        $roundTrip.ExitCode | Should Be 1
    }
    It 'separates deployment readiness from production support in Markdown' {
        $parameters = New-TestParameters
        $parameters.Mode = 'CollectorHost'
        $checks = Test-CollectorHostReadiness $parameters `
            (New-TestHostOperations 17763 'Microsoft Windows Server 2019')
        $manifest = New-ReadinessManifest -Mode CollectorHost `
            -Context (New-TestManifestContext) -Checks $checks `
            -GeneratedAt ([datetime]'2026-07-27T12:00:00+03:00')
        $markdown = ConvertTo-ReadinessMarkdown $manifest
        $markdown | Should Match '## Platform Classification'
        $markdown | Should Match 'Deployment readiness \| PASS'
        $markdown | Should Match 'Production support \| WARNING'
        $markdown | Should Match 'controlled-lab platform'
        $markdown | Should Not Match 'Password=|private-marker'
    }
}

Describe 'Network and WinRM policy' {
    It 'reports DNS failure and skips dependent TCP' {
        $ops = @{Resolve={param($n) throw 'dns'};TestTcp={param($n,$p) throw 'must not run'}}
        $checks = Test-NetworkEndpoint NETWORK.TEST target.example.test 5986 $true $ops
        $checks[0].Status | Should Be FAIL
        $checks[1].Status | Should Be SKIPPED
    }
    It 'passes TCP regardless of ping because ping is not used' {
        $ops = @{Resolve={param($n) @('192.0.2.10')};TestTcp={param($n,$p) @{Success=$true;Duration=3}}}
        (Test-NetworkEndpoint NETWORK.TEST target.example.test 5986 $true $ops)[1].Status | Should Be PASS
    }
    It 'reports TCP failure' {
        $ops = @{Resolve={param($n) @('192.0.2.10')};TestTcp={param($n,$p) @{Success=$false;Duration=5}}}
        (Test-NetworkEndpoint NETWORK.TEST target.example.test 5986 $true $ops)[1].Status | Should Be FAIL
    }
    It 'reports optional TCP failure as warning' {
        $ops = @{Resolve={param($n) @('192.0.2.10')};TestTcp={param($n,$p) @{Success=$false;Duration=5}}}
        (Test-NetworkEndpoint NETWORK.TEST target.example.test 5986 $false $ops)[1].Status | Should Be WARNING
    }
    It 'validates port parameters at the entry point AST contract' {
        $text = Get-Content -Raw (Join-Path $root 'Invoke-CollectorReadiness.ps1')
        $text | Should Match 'ValidateRange\(1,65535\)'
    }
    It 'passes HTTPS and performs no fallback' {
        $ops = @{TestWsMan={param($n,$p,$ssl) @{ProductVersion='x'}}}
        $checks = Test-WinRmReadiness (New-TestParameters) $ops
        ($checks | Where-Object CheckId -eq WINRM.HTTPS).Status | Should Be PASS
        ($checks | Where-Object CheckId -eq WINRM.HTTP.FALLBACK) | Should Be $null
    }
    It 'HttpsOnly prevents fallback' {
        $p = New-TestParameters; $p.TransportPolicy='HttpsOnly'
        $ops = @{TestWsMan={param($n,$port,$ssl) throw [System.TimeoutException]::new()}}
        $checks = Test-WinRmReadiness $p $ops
        ($checks | Where-Object CheckId -eq WINRM.HTTP.FALLBACK) | Should Be $null
    }
    It 'Auto permits eligible timeout fallback' {
        $ops = @{TestWsMan={param($n,$port,$ssl) if($ssl){throw [System.TimeoutException]::new()} else {@{ok=$true}}}}
        $checks = Test-WinRmReadiness (New-TestParameters) $ops
        ($checks | Where-Object CheckId -eq WINRM.HTTPS).Status | Should Be WARNING
        ($checks | Where-Object CheckId -eq WINRM.HTTP.FALLBACK).Status | Should Be PASS
    }
    It 'authentication failure prevents fallback' {
        $ops = @{TestWsMan={param($n,$port,$ssl) throw [System.Security.Authentication.AuthenticationException]::new()}}
        (Test-WinRmReadiness (New-TestParameters) $ops | Where-Object CheckId -eq WINRM.HTTP.FALLBACK).Status | Should Be SKIPPED
    }
    It 'authorization failure prevents fallback' {
        $ops = @{TestWsMan={param($n,$port,$ssl) throw [System.UnauthorizedAccessException]::new()}}
        (Test-WinRmReadiness (New-TestParameters) $ops | Where-Object CheckId -eq WINRM.HTTP.FALLBACK).Status | Should Be SKIPPED
    }
    It 'DNS failure prevents fallback' {
        $ops = @{TestWsMan={param($n,$port,$ssl) throw [System.Net.Sockets.SocketException]::new()}}
        (Test-WinRmReadiness (New-TestParameters) $ops | Where-Object CheckId -eq WINRM.HTTP.FALLBACK).Status | Should Be SKIPPED
    }
    It 'cancellation prevents fallback' {
        $ops = @{TestWsMan={param($n,$port,$ssl) throw [System.OperationCanceledException]::new()}}
        (Test-WinRmReadiness (New-TestParameters) $ops | Where-Object CheckId -eq WINRM.HTTP.FALLBACK).Status | Should Be SKIPPED
    }
    It 'certificate failure is safely reported and permits Auto fallback' {
        $ops = @{TestWsMan={param($n,$port,$ssl) if($ssl){throw [System.Security.Authentication.AuthenticationException]::new('Certificate')} else {@{ok=$true}}}}
        $checks = Test-WinRmReadiness (New-TestParameters) $ops
        ($checks | Where-Object CheckId -eq WINRM.HTTPS).Evidence | Should Not Match 'Certificate'
        ($checks | Where-Object CheckId -eq WINRM.HTTP.FALLBACK).Status | Should Be PASS
    }
}

Describe 'SQL metadata-only validation' {
    It 'skips all SQL access when explicitly requested and remains mandatory' {
        $p = New-TestParameters; $p.SkipSqlAuthenticationTest=$true
        $check = (Test-SqlReadiness $p @{Query={throw 'must not execute'}})[0]
        $check.Status | Should Be SKIPPED
        $check.IsMandatory | Should Be $true
    }
    It 'passes authentication, migration, tables and permission metadata' {
        $ops = @{Query={param($connection,$query)
            if($query -match 'SUSER_SNAME'){New-TestTable @{DatabaseName='PSM_Smoke';IntegratedIdentity='EXAMPLE\gmsa$'}}
            elseif($query -match 'MigrationPresent'){New-TestTable @{MigrationPresent=1;TablesPresent=1}}
            else {New-TestTable @{CanSelect=1;CanInsert=1;CanUpdate=1;CanDelete=1}}
        }}
        $checks = Test-SqlReadiness (New-TestParameters) $ops
        @($checks | Where-Object Status -eq FAIL).Count | Should Be 0
    }
    It 'fails when migration or a required table is missing' {
        $ops = @{Query={param($connection,$query)
            if($query -match 'SUSER_SNAME'){New-TestTable @{DatabaseName='PSM_Smoke';IntegratedIdentity='EXAMPLE\gmsa$'}}
            elseif($query -match 'MigrationPresent'){New-TestTable @{MigrationPresent=0;TablesPresent=1}}
            else {New-TestTable @{CanSelect=1;CanInsert=1;CanUpdate=1;CanDelete=1}}
        }}
        (Test-SqlReadiness (New-TestParameters) $ops | Where-Object CheckId -eq SQL.SCHEMA).Status | Should Be FAIL
    }
    It 'fails missing read and write permission metadata' {
        $ops = @{Query={param($connection,$query)
            if($query -match 'SUSER_SNAME'){New-TestTable @{DatabaseName='PSM_Smoke';IntegratedIdentity='EXAMPLE\gmsa$'}}
            elseif($query -match 'MigrationPresent'){New-TestTable @{MigrationPresent=1;TablesPresent=1}}
            else {New-TestTable @{CanSelect=0;CanInsert=0;CanUpdate=0;CanDelete=0}}
        }}
        $checks = Test-SqlReadiness (New-TestParameters) $ops
        ($checks | Where-Object CheckId -eq SQL.PERMISSION.READ).Status | Should Be FAIL
        ($checks | Where-Object CheckId -eq SQL.PERMISSION.WRITE.METADATA).Status | Should Be FAIL
    }
}

Describe 'AST read-only enforcement' {
    $productScripts = Get-ChildItem $root -Filter *.ps1
    It 'contains no prohibited mutation or remote execution commands' {
        $prohibited = @(
            'Install-Module','Install-ADServiceAccount','Uninstall-Module',
            'Enable-PSRemoting','Disable-PSRemoting','Set-Item','New-Item',
            'Remove-Item','Start-Service','Stop-Service','Restart-Service',
            'Set-Service','Invoke-Command','Enter-PSSession','Restart-Computer',
            'New-PSSession','Set-ADServiceAccount','New-NetFirewallRule'
        )
        $found = @()
        foreach($file in $productScripts) {
            $tokens=$null;$errors=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
            $found += $ast.FindAll({param($node)
                $node -is [Management.Automation.Language.CommandAst] -and
                $node.GetCommandName() -in $prohibited
            },$true)
        }
        $found.Count | Should Be 0
    }
    It 'contains no SQL mutation statement in executable SQL literals' {
        $sql = Get-Content -Raw (Join-Path $root 'SqlValidation.ps1')
        $sql | Should Not Match '(?im)^\s*(INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|MERGE|TRUNCATE)\b'
    }
    It 'contains no WinRM certificate bypass or TrustedHosts mutation option' {
        $tokens=$null;$errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $root 'WinRmValidation.ps1'),[ref]$tokens,[ref]$errors)
        $bypassParameters = @($ast.FindAll({param($node)
            $node -is [Management.Automation.Language.CommandParameterAst] -and
            $node.ParameterName -in @('SkipCACheck','SkipCNCheck','SkipRevocationCheck')
        },$true))
        $bypassParameters.Count | Should Be 0
    }
    It 'defines console summary and fixed report names' {
        $entry = Get-Content -Raw (Join-Path $root 'Invoke-CollectorReadiness.ps1')
        $common = Get-Content -Raw (Join-Path $root 'Readiness.Common.ps1')
        $entry | Should Match 'Overall:'
        $entry | Should Match 'Exit Code:'
        $common | Should Match 'collector-readiness.json'
        $common | Should Match 'collector-readiness.md'
    }
    It 'parses every product PowerShell file without syntax errors' {
        foreach($file in (Get-ChildItem $root -Filter *.ps1)) {
            $tokens=$null;$errors=$null
            $null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
            $errors.Count | Should Be 0
        }
    }
}
