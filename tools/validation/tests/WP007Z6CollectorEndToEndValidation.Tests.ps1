$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$collectorRoot=Join-Path $repoRoot 'tools\validation\collector'
. (Join-Path $repoRoot 'tools\validation\common\OperationalValidation.Common.ps1')
. (Join-Path $repoRoot 'tools\validation\common\OperationalValidation.Reporting.ps1')
. (Join-Path $collectorRoot 'CollectorEndToEnd.Checks.ps1')

function New-TestE2EResult {
    param([string]$Id,[string]$Category='Test',[string]$Target='target',[string]$Status='PASS',[bool]$Mandatory=$true,[object]$Actual=$true)
    New-OperationalObservation $Id $Category $Id $Id $Target $Status $(if($Status -eq 'FAIL'){'HIGH'}elseif($Status -in @('WARNING','SKIPPED')){'MEDIUM'}else{'INFO'}) $true $Actual 'synthetic mocked result' 'synthetic recommendation' $Id $Mandatory
}

function New-TestE2EOperations {
    param([string]$IisStatus='PASS',[string]$SqlStatus='PASS',[string]$HostStatus='PASS',[string]$CurrentIdentity='EXAMPLE\gmsa-collector$')
    $newResult=${function:New-TestE2EResult}
    $runHost=({param($configuration,$collector,$output,$skip)@(
        (& $newResult 'OS.VERSION' OperatingSystem $collector $HostStatus $true),
        (& $newResult 'IDENTITY.GMSA.FORMAT' ActiveDirectoryKerberos $collector PASS $true),
        (& $newResult 'SECURITY.NO.SECRETS' SecurityConfiguration $collector PASS $true))}).GetNewClosure()
    $runIis=({param($configuration,$target,$index)$prefix='T{0:D3}' -f $index;@(
        (& $newResult "$prefix.CONNECTIVITY.DNS.FORWARD" Connectivity $target $IisStatus $true),
        (& $newResult "$prefix.CONNECTIVITY.TCP.WINRM" Connectivity $target PASS $true),
        (& $newResult "$prefix.CONNECTIVITY.WSMAN" Connectivity $target PASS $true),
        (& $newResult "$prefix.CONNECTIVITY.KERBEROS.SESSION" Connectivity $target PASS $true),
        (& $newResult "$prefix.COMPATIBILITY.RESULT" CollectorCompatibility $target $IisStatus $true))}).GetNewClosure()
    $runSql=({param($configuration,$target,$index,$source)$prefix='T{0:D3}' -f $index;$items=@(
        (& $newResult "$prefix.NETWORK.DNS.FORWARD" DNSNetworkConnectivity $target.Name $SqlStatus $true),
        (& $newResult "$prefix.NETWORK.TCP" DNSNetworkConnectivity $target.Name PASS $true),
        (& $newResult "$prefix.CONNECTION.OPEN" SQLConnection $target.Name PASS $true),
        (& $newResult "$prefix.CONNECTION.ENDPOINT" SQLConnection $target.Name PASS $true),
        (& $newResult "$prefix.CONNECTION.WINDOWSAUTH" SQLConnection $target.Name PASS $true $configuration.SqlCollector.ServiceAccount),
        (& $newResult "$prefix.KERBEROS.AUTHSCHEME" KerberosSPN $target.Name PASS $true 'KERBEROS'),
        (& $newResult "$prefix.TLS.ENCRYPTED" EncryptionTLS $target.Name PASS $true 'TRUE'),
        (& $newResult "$prefix.COMPATIBILITY.RESULT" SQLCollectorCompatibility $target.Name $SqlStatus $true));if($target.ExpectedRole -eq 'OperationsDatabase'){$items+=@((& $newResult "$prefix.OPERATIONS.ONLINE" OperationsDatabaseCompatibility $target.Name PASS $true),(& $newResult "$prefix.OPERATIONS.SCHEMA" OperationsDatabaseCompatibility $target.Name PASS $true),(& $newResult "$prefix.OPERATIONS.PERMISSIONPROFILE" OperationsDatabaseCompatibility $target.Name PASS $true))};$items}).GetNewClosure()
    @{
        RunCollectorHost=$runHost;RunIisTarget=$runIis;RunSqlTarget=$runSql
        CurrentIdentity=({$CurrentIdentity}).GetNewClosure()
    }
}

Describe 'WP-007.Z.6 Collector end-to-end validation' {
    BeforeEach{$configuration=Get-Content -Raw (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json')|ConvertFrom-Json;$collector=$configuration.Collector.Server}

    It 'accepts the complete valid shared configuration' {Test-CollectorEndToEndConfiguration $configuration $collector|Should Be $true}
    It 'rejects a Collector outside configuration before operations' {{Test-CollectorEndToEndConfiguration $configuration 'other.example.invalid'}|Should Throw}
    It 'rejects missing or duplicate Operations database targets' {$configuration.SqlTargets|Where-Object ExpectedRole -eq OperationsDatabase|ForEach-Object{$_.ValidationEnabled=$false};{Test-CollectorEndToEndConfiguration $configuration $collector}|Should Throw}
    It 'produces COLLECTOR READY from passing child evidence' {$outcome=Invoke-CollectorEndToEndChecks $configuration 'configuration.json' $TestDrive $collector $false (New-TestE2EOperations);$outcome.ReadinessDecision|Should Be 'COLLECTOR READY';@($outcome.BlockedExecutionReasons).Count|Should Be 0;($outcome.Results|Where-Object CheckId -eq READINESS.DECISION).Status|Should Be PASS}
    It 'namespaces reused host IIS and SQL results without duplicates' {$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false (New-TestE2EOperations);@($outcome.Results|Where-Object CheckId -match '^HOST\.').Count -gt 0|Should Be $true;@($outcome.Results|Where-Object CheckId -match '^IIS\.T001\.').Count -gt 0|Should Be $true;@($outcome.Results|Where-Object CheckId -match '^SQL\.T001\.').Count -gt 0|Should Be $true;{Assert-OperationalResults $outcome.Results}|Should Not Throw}
    It 'validates every configured IIS and enabled SQL target' {$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false (New-TestE2EOperations);@($outcome.Results|Where-Object CheckId -match '^PREREQUISITE\.IIS\.T').Count|Should Be @($configuration.IisTargets).Count;@($outcome.Results|Where-Object CheckId -match '^PREREQUISITE\.SQL\.T').Count|Should Be @($configuration.SqlTargets|Where-Object ValidationEnabled).Count}
    It 'produces ready with warnings for a nonblocking child warning' {$ops=New-TestE2EOperations;$original=$ops.RunCollectorHost;$ops.RunCollectorHost={param($c,$t,$o,$s)@(& $original $c $t $o $s)+(New-TestE2EResult 'OPTIONAL.WARNING' Test $t WARNING $false)};$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false $ops;$outcome.ReadinessDecision|Should Be 'COLLECTOR READY WITH WARNINGS'}
    It 'blocks readiness on an IIS failure' {$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false (New-TestE2EOperations -IisStatus FAIL);$outcome.ReadinessDecision|Should Be 'COLLECTOR NOT READY';@($outcome.BlockedExecutionReasons).Count -gt 0|Should Be $true}
    It 'blocks readiness on a SQL failure' {$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false (New-TestE2EOperations -SqlStatus FAIL);$outcome.ReadinessDecision|Should Be 'COLLECTOR NOT READY'}
    It 'blocks readiness when current Collector identity is inconsistent' {$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false (New-TestE2EOperations -CurrentIdentity 'EXAMPLE\wrong$');$outcome.ReadinessDecision|Should Be 'COLLECTOR NOT READY'}
    It 'does not call IIS or SQL operations when remote checks are skipped' {$script:iisCalls=0;$script:sqlCalls=0;$ops=New-TestE2EOperations;$ops.RunIisTarget={param($a,$b,$c)$script:iisCalls++};$ops.RunSqlTarget={param($a,$b,$c,$d)$script:sqlCalls++};$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $true $ops;$script:iisCalls|Should Be 0;$script:sqlCalls|Should Be 0;$outcome.ReadinessDecision|Should Be 'COLLECTOR NOT READY';@($outcome.Results|Where-Object Status -eq SKIPPED).Count|Should Be (@($configuration.IisTargets).Count+@($configuration.SqlTargets|Where-Object ValidationEnabled).Count)}
    It 'creates every required matrix edge without lateral probes' {$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false (New-TestE2EOperations);@($outcome.Results|Where-Object CheckId -match '^MATRIX\.IIS\.').Count|Should Be @($configuration.IisTargets).Count;@($outcome.Results|Where-Object CheckId -match '^MATRIX\.SQL\.').Count|Should Be @($configuration.SqlTargets|Where-Object ValidationEnabled).Count;($outcome.Results|Where-Object CheckId -eq MATRIX.OPERATIONSDATABASE).Status|Should Be PASS;($outcome.Results|Where-Object CheckId -eq MATRIX.PORTAL.CONFIGURATION).Status|Should Be NOT_APPLICABLE}
    It 'fails a matrix edge when required constituent evidence is missing' {$ops=New-TestE2EOperations;$ops.RunIisTarget={param($c,$t,$i)@(New-TestE2EResult ('T{0:D3}.COMPATIBILITY.RESULT' -f $i) CollectorCompatibility $t PASS $true)};$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false $ops;($outcome.Results|Where-Object CheckId -eq MATRIX.IIS.T001).Status|Should Be FAIL;$outcome.ReadinessDecision|Should Be 'COLLECTOR NOT READY'}
    It 'adds readiness and blocked reasons to common JSON Markdown and log reports' {$outcome=Invoke-CollectorEndToEndChecks $configuration config $TestDrive $collector $false (New-TestE2EOperations);$report=New-OperationalReport $configuration ('A'*64) source $collector ([datetime]'2026-07-31') $outcome.Results 'Collector End-to-End Validation Report' 'CollectorEndToEndValidation';$report|Add-Member ReadinessDecision $outcome.ReadinessDecision;$report|Add-Member BlockedExecutionReasons @($outcome.BlockedExecutionReasons);$paths=Write-OperationalReports $report $TestDrive;Split-Path $paths.Json -Leaf|Should Be CollectorEndToEndValidationReport.json;Split-Path $paths.Markdown -Leaf|Should Be CollectorEndToEndValidationReport.md;Split-Path $paths.Log -Leaf|Should Be CollectorEndToEndValidation.log;(Get-Content -Raw $paths.Markdown)|Should Match 'Readiness decision: COLLECTOR READY';(Get-Content -Raw $paths.Log)|Should Match 'ReadinessDecision=COLLECTOR READY'}
    It 'defines the exact entry point parameters and decisions' {$text=Get-Content -Raw (Join-Path $collectorRoot 'Invoke-CollectorEndToEndValidation.ps1');foreach($term in @('ConfigurationPath','OutputPath','TargetCollector','SkipRemoteChecks','COLLECTOR READY','COLLECTOR READY WITH WARNINGS','COLLECTOR NOT READY','exit 3','exit 4')){$text|Should Match ([regex]::Escape($term))}}
    It 'reuses child functions and does not launch child validator processes' {$text=Get-Content -Raw (Join-Path $collectorRoot 'CollectorEndToEnd.Checks.ps1');foreach($term in @('Invoke-CollectorOperatingSystemChecks','Invoke-IisTargetChecks','Invoke-SqlTargetChecks')){$text|Should Match ([regex]::Escape($term))};$text|Should Not Match '(?i)Start-Process|powershell\.exe|Invoke-IISTargetValidation\.ps1|Invoke-SQLTargetValidation\.ps1'}
    It 'contains no runtime or environment mutation commands' {$prohibited=@('Start-Service','Stop-Service','Restart-Service','Set-Service','Restart-WebAppPool','Start-WebAppPool','Stop-WebAppPool','Stop-Process','Set-ItemProperty','New-NetFirewallRule','Setspn');$found=@();foreach($file in Get-ChildItem $collectorRoot -Filter *EndToEnd*.ps1){$tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors);$errors.Count|Should Be 0;$found+=@($ast.FindAll({param($node)$node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -in $prohibited},$true))};$found.Count|Should Be 0}
}
