$root=Split-Path -Parent $PSScriptRoot
$preflightPath=Join-Path $root 'Invoke-WP0062Preflight.ps1'
$sqlPath=Join-Path $root 'Get-WP0062SqlEvidence.ps1'
$hostPath=Join-Path $root 'Get-WP0062HostEvidence.ps1'
$evidencePath=Join-Path $root 'Test-WP0062Evidence.ps1'
$repoRoot=(Resolve-Path (Join-Path $root '..\..')).Path

$commonPreflight=@{
    CollectorHost='collector';CollectorInstallPath='C:\lab';CollectorServiceName='svc'
    ExpectedServiceAccount='EXAMPLE\gmsaCollector$';RepositoryRoot='C:\repo'
    ExpectedCommit=('a'*40);CollectorExecutablePath='C:\lab\collector.exe'
    SqlServer='sql.example.test';DatabaseName='PSM_Lab'
    ManagedServerId=[guid]'11111111-1111-1111-1111-111111111111'
    TargetFqdn='target.example.test';ExpectedTransportPolicy='Auto'
    ReadinessJsonPath='C:\evidence\readiness.json';EvidenceRoot='C:\evidence'
    ObservationMinutes=5;LoggingConfigurationPath='C:\lab\appsettings.json'
    ExpectedMigrationId='20260727230000_AddWindowsInventoryCurrentState'
    AllowedCollectorHostWarning=$false;ApprovedHttpFallback=$false
    OperatorName='Operator';ApproverName='Approver';RollbackOwner='Rollback'
    ChangeReference='CHG-TEST'
}
. $preflightPath @commonPreflight

$commonSql=@{
    SqlServer='sql.example.test';DatabaseName='PSM_Lab'
    ManagedServerId=[guid]'11111111-1111-1111-1111-111111111111'
    TargetFqdn='target.example.test';Phase='Baseline'
    ExpectedMigrationId='20260727230000_AddWindowsInventoryCurrentState'
    EvidenceRoot='C:\evidence'
}
. $sqlPath @commonSql

$commonHost=@{
    CollectorHost='collector';CollectorServiceName='svc'
    CollectorExecutablePath='C:\lab\collector.exe'
    LoggingConfigurationPath='C:\lab\appsettings.json'
    Snapshot='PreStart';EvidenceRoot='C:\evidence'
}
. $hostPath @commonHost
. $evidencePath -ManifestPath 'C:\evidence\manifest.json' -OutputRoot 'C:\evidence'

function New-PreflightState {
    @{
        Readiness=[pscustomobject]@{OverallStatus='READY';ExitCode=0;OperatingSystem='Windows Server 2022'}
        Writable=$true
        ServicePath='C:\lab\collector.exe'
        ServiceAccount='EXAMPLE\gmsaCollector$'
        ArtifactHash=('b'*64)
        Commit=('a'*40)
    }
}

function New-PreflightOps {
    param($State)
    @{
        PathExists={param($path)$true}
        GetHash={param($path)$State.ArtifactHash}
        GetCommit={param($root)$State.Commit}
        GetServices={
            [pscustomobject]@{Name='svc';StartName=$State.ServiceAccount
                PathName=('"{0}"' -f $State.ServicePath);State='Stopped';ProcessId=0}
        }
        ReadJson={param($path)$State.Readiness}
        GetEnvironmentValue={param($name)'present-but-redacted'}
        IsElevated={$true}
        TestWritable={param($path)$State.Writable}
        WriteText={param($path,$content)}
        MachineName={'collector'}
    }
}

function Invoke-TestPreflight {
    param($State,[bool]$AllowWarning=$false)
    $values=@{}+$commonPreflight
    $values.AllowedCollectorHostWarning=$AllowWarning
    Invoke-WP0062PreflightValidation $values (New-PreflightOps $State)
}

Describe 'WP-006.2 preflight' {
    It 'returns READY for approved local inputs without infrastructure' {
        (Invoke-TestPreflight (New-PreflightState)).OverallStatus | Should Be READY
    }
    It 'accepts an explicitly approved WARNING' {
        $state=New-PreflightState
        $state.Readiness=[pscustomobject]@{OverallStatus='WARNING';ExitCode=1;OperatingSystem='Windows Server 2019'}
        $result=Invoke-TestPreflight $state $true
        $result.OverallStatus | Should Be WARNING
        $result.ExitCode | Should Be 1
    }
    It 'rejects an unapproved WARNING and NOT_READY' {
        $state=New-PreflightState
        $state.Readiness=[pscustomobject]@{OverallStatus='WARNING';ExitCode=1;OperatingSystem='Windows Server 2019'}
        (Invoke-TestPreflight $state $false).OverallStatus | Should Be NOT_READY
        $state.Readiness=[pscustomobject]@{OverallStatus='NOT_READY';ExitCode=2;OperatingSystem='Windows Server 2022'}
        (Invoke-TestPreflight $state $true).OverallStatus | Should Be NOT_READY
    }
    It 'rejects service path and account mismatches' {
        $state=New-PreflightState;$state.ServicePath='C:\wrong\collector.exe'
        (Invoke-TestPreflight $state).OverallStatus | Should Be NOT_READY
        $state=New-PreflightState;$state.ServiceAccount='EXAMPLE\wrong$'
        (Invoke-TestPreflight $state).OverallStatus | Should Be NOT_READY
    }
    It 'rejects artifact/commit and evidence failures' {
        $state=New-PreflightState;$state.ArtifactHash=$null
        (Invoke-TestPreflight $state).OverallStatus | Should Be NOT_READY
        $state=New-PreflightState;$state.Commit=('c'*40)
        (Invoke-TestPreflight $state).OverallStatus | Should Be NOT_READY
        $state=New-PreflightState;$state.Writable=$false
        (Invoke-TestPreflight $state).OverallStatus | Should Be NOT_READY
    }
    It 'does not reveal the environment variable value' {
        ((Invoke-TestPreflight (New-PreflightState)) | ConvertTo-Json -Depth 8) |
            Should Not Match 'present-but-redacted'
    }
}

Describe 'WP-006.2 SQL evidence' {
    It 'detects duplicate stable keys from a fixture' {
        $data=Get-Content -Raw (Join-Path $PSScriptRoot 'fixtures\Sql-Duplicates.json') |
            ConvertFrom-Json
        $map=@{};foreach($p in $data.PSObject.Properties){$map[$p.Name]=@($p.Value)}
        (Get-WP0062Integrity $map).StableKeyDuplicateGroups | Should Be 1
    }
    It 'detects IPv4 orphans from a fixture' {
        $data=Get-Content -Raw (Join-Path $PSScriptRoot 'fixtures\Sql-Orphans.json') |
            ConvertFrom-Json
        $map=@{};foreach($p in $data.PSObject.Properties){$map[$p.Name]=@($p.Value)}
        (Get-WP0062Integrity $map).Ipv4OrphanCount | Should Be 1
    }
    It 'uses actual schemas and only read-only SQL statements' {
        $queries=Get-WP0062ReadOnlyQueries $commonSql.ManagedServerId $commonSql.ExpectedMigrationId
        $text=$queries.Values -join "`n"
        $text | Should Match 'configuration\.ManagedServer'
        $text | Should Match 'inventory\.WindowsIpv4AddressInventory'
        $text | Should Not Match '(?i)\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|EXEC(?:UTE)?)\b'
    }
    It 'rejects unsafe SQL identifiers before any query' {
        { Assert-WP0062SqlInputs 'server;DROP' 'PSM_Lab' 'migration' } | Should Throw
        { Assert-WP0062SqlInputs 'server' 'db;DROP' 'migration' } | Should Throw
    }
}

Describe 'WP-006.2 host evidence' {
    It 'captures a local Running snapshot through read-only mocked operations' {
        $ops=@{
            MachineName={'collector'};PathExists={param($path)$true}
            GetHash={param($path)('b'*64)}
            GetService={param($name)[pscustomobject]@{Name=$name;DisplayName='Collector'
                State='Running';StartMode='Auto';StartName='EXAMPLE\gmsaCollector$'
                PathName='"C:\lab\collector.exe"';ProcessId=42}}
            GetOperatingSystem={[pscustomobject]@{Caption='Windows Server 2022'
                Version='10.0';BuildNumber='20348';OSArchitecture='64-bit'}}
            GetProcess={param($id)[pscustomobject]@{Id=$id;StartTime=[datetime]'2026-07-27T12:00:00'
                WorkingSet64=100;PrivateMemorySize64=90;HandleCount=10;Threads=@(1,2)}}
            GetTimeZone={[pscustomobject]@{Id='Turkey Standard Time';DisplayName='Türkiye'}}
            WriteText={param($path,$content)}
        }
        $values=@{}+$commonHost;$values.Snapshot='Running'
        $result=Get-WP0062HostSnapshot $values $ops
        $result.Service.State | Should Be Running
        $result.Process.ThreadCount | Should Be 2
        $result.Executable.Sha256 | Should Be ('b'*64)
    }
}

function Write-TestJson {
    param([string]$Path,$Value)
    $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding UTF8
}

Describe 'WP-006.2 offline evidence validation' {
    It 'reports missing manifest without infrastructure access' {
        $ops=Get-WP0062EvidenceOperations
        (Test-WP0062OfflineEvidence (Join-Path $TestDrive 'missing.json') $ops).Status |
            Should Be NOT_READY
    }
    It 'detects schema, duplicate, orphan, timestamp and missing evidence failures' {
        $dir=Join-Path $TestDrive 'evidence';$null=New-Item -ItemType Directory $dir
        $id='11111111-1111-1111-1111-111111111111'
        $ready=[pscustomobject]@{OverallStatus='READY';ExitCode=0}
        $pre=[pscustomobject]@{OverallStatus='READY'}
        $badSql=[pscustomobject]@{Phase='Verification';EligibleTargetCount=1
            Target=[pscustomobject]@{ManagedServerId=$id;TargetFqdn='target.example.test'}
            Integrity=[pscustomobject]@{SingularDuplicateGroups=1;StableKeyDuplicateGroups=1;Ipv4OrphanCount=1}}
        $base=$badSql | Select-Object *;$base.Phase='Baseline'
        $post=$badSql | Select-Object *;$post.Phase='PostRun'
        $hostPre=[pscustomobject]@{Snapshot='PreStart';Service=[pscustomobject]@{State='Stopped'}}
        $hostRun=[pscustomobject]@{Snapshot='Running';Service=[pscustomobject]@{State='Running'}}
        $hostPost=[pscustomobject]@{Snapshot='PostStop';Service=[pscustomobject]@{State='Stopped'}}
        $modules=[pscustomobject]@{ModuleOrder=@('Computer');ModuleOutcomes=@()}
        $restore=[pscustomobject]@{RestorationCompleted=$false}
        $objects=@{ready=$ready;pre=$pre;base=$base;post=$post;verify=$badSql
            hostpre=$hostPre;hostrun=$hostRun;hostpost=$hostPost;modules=$modules;restore=$restore}
        foreach($key in $objects.Keys){Write-TestJson (Join-Path $dir "$key.json") $objects[$key]}
        $manifest=[ordered]@{
            SchemaVersion='0';WorkPackage='WP-006.2';ExecutionId=[guid]::NewGuid().ToString()
            RepositoryCommit=('a'*40);CollectorArtifactHash=('b'*64);CollectorHost='collector'
            CollectorInstallPath='C:\lab';CollectorServiceName='svc';ExpectedServiceAccount='EXAMPLE\gmsa$'
            SqlServer='sql';DatabaseName='db';ManagedServerId=$id;TargetFqdn='target.example.test'
            TransportPolicy='Auto';EvidenceRoot=$dir;Operator='o';Approver='a';RollbackOwner='r'
            ChangeReference='c';ConfigurationSource='Environment';StartTime='2026-07-27T12:10:00+03:00'
            StopTime='2026-07-27T12:00:00+03:00';RestorationCompleted=$false
            LabOnlyException=[ordered]@{Applies=$false}
            LoggingProfile=[ordered]@{OriginalHash='x';RestoredHash='y'}
            EvidenceFiles=[ordered]@{ReadinessJson='ready.json';PreflightJson='pre.json'
                SqlBaselineJson='base.json';SqlPostRunJson='post.json';SqlVerificationJson='verify.json'
                HostPreStartJson='hostpre.json';HostRunningJson='hostrun.json';HostPostStopJson='hostpost.json'
                ModuleEvidenceJson='modules.json';RestorationEvidenceJson='restore.json'}
        }
        $manifestPath=Join-Path $dir 'manifest.json';Write-TestJson $manifestPath $manifest
        $result=Test-WP0062OfflineEvidence $manifestPath (Get-WP0062EvidenceOperations)
        $result.Status | Should Be NOT_READY
        ($result.Checks | Where-Object CheckId -eq SQL.STABLEKEY).Status | Should Be FAIL
        ($result.Checks | Where-Object CheckId -eq SQL.ORPHANS).Status | Should Be FAIL
        ($result.Checks | Where-Object CheckId -eq TIME.ORDER).Status | Should Be FAIL
    }
}

Describe 'Static safety and schema' {
    $scripts=@($preflightPath,$sqlPath,$hostPath,$evidencePath)
    It 'parses every script without syntax errors' {
        foreach($path in $scripts){
            $tokens=$null;$errors=$null
            $null=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
            $errors.Count | Should Be 0
        }
    }
    It 'contains no service, AD, WinRM, target-data, or logging mutation command' {
        $text=($scripts | ForEach-Object {Get-Content -Raw $_}) -join "`n"
        $text | Should Not Match '(?i)\b(Start-Service|Stop-Service|Restart-Service|Set-Service|New-Service|Remove-Service|Invoke-Command|New-PSSession|Enter-PSSession|Set-AD|Get-AD|Set-ItemProperty)\b'
    }
    It 'contains no mutating SQL' {
        $text=Get-Content -Raw $sqlPath
        $text | Should Not Match '(?i)\b(INSERT|UPDATE|DELETE|MERGE|CREATE\s+(TABLE|SCHEMA|PROCEDURE)|ALTER\s+(TABLE|ROLE)|DROP|TRUNCATE)\b'
    }
    It 'does not hard-code repository environment values or credential parameters' {
        $text=($scripts | ForEach-Object {Get-Content -Raw $_}) -join "`n"
        $text | Should Not Match 'mydb01\.ae\.local|gmsaSPWorker|PSMOperationsPlatform_SmokeTest'
        $text | Should Not Match '(?i)\\-(Password|Credential|SqlUser|SqlPassword)\b'
    }
    It 'keeps the manifest template valid and stable' {
        $template=Get-Content -Raw (Join-Path $repoRoot 'docs\testing\templates\WP-006.2-Execution-Manifest.template.json') |
            ConvertFrom-Json
        $template.SchemaVersion | Should Be '1.0'
        $template.WorkPackage | Should Be 'WP-006.2'
        $template.EvidenceFiles.SqlVerificationJson | Should Not BeNullOrEmpty
    }
}
