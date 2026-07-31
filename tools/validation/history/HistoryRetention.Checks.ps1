#requires -Version 5.1
Set-StrictMode -Version Latest

function New-HistoryRetentionCheck {
    param([string]$Id,[string]$Category,[string]$Name,[string]$Target,[object]$Actual,[string]$Status='PASS',[string]$Severity='INFO',[object]$Expected=$true,[string]$Message='Repository contract observed.',[string]$Recommendation=$null,[bool]$Mandatory=$true)
    New-OperationalObservation "H001.$Id" $Category $Name $Name $Target $Status $Severity $Expected $Actual $Message $Recommendation $Actual $Mandatory
}

function Test-HistoryRetentionConfiguration {
    param([Parameter(Mandatory)][object]$Configuration,[string]$DatabaseTarget)
    if($null -eq $Configuration.HistoryValidation){throw 'HistoryValidation configuration is required.'}
    $h=$Configuration.HistoryValidation
    if($h.ExpectedHistorySchemaVersion -ne 1 -or $h.RetentionPolicyProfile -ne 'ExecutionHistoryV1' -or $h.RetentionBatchSize -ne 500 -or $h.RetentionDryRunEnabled){throw 'History schema or retention profile configuration is invalid.'}
    $targets=@($Configuration.SqlTargets|Where-Object{$_.ExpectedRole -eq 'OperationsDatabase' -and $_.ValidationEnabled})
    if($targets.Count -ne 1){throw 'Exactly one enabled Operations database target is required.'}
    if($DatabaseTarget -and -not [string]::Equals($DatabaseTarget,$targets[0].Name,[StringComparison]::OrdinalIgnoreCase)){throw 'DatabaseTarget must identify the configured Operations database target.'}
    $serialized=$h|ConvertTo-Json -Depth 5
    if($serialized -match '(?i)password|secret|token|credential|connectionstring|sqlauthentication'){throw 'Secret-bearing or SQL Authentication configuration is prohibited.'}
    $targets[0]
}

function Get-HistoryRepositoryFacts {
    param([Parameter(Mandatory)][string]$RepositoryRoot)
    $application=Get-Content -Raw (Join-Path $RepositoryRoot 'src\PSMOperationsPlatform.Application\Runtime\ExecutionHistory.cs')
    $store=Get-Content -Raw (Join-Path $RepositoryRoot 'src\PSMOperationsPlatform.Infrastructure\Persistence\ExecutionHistoryStore.cs')
    $migration=Get-Content -Raw (Join-Path $RepositoryRoot 'src\PSMOperationsPlatform.Infrastructure\Persistence\Migrations\20260729191745_WP0088ExecutionHistory.cs')
    $tests=Get-Content -Raw (Join-Path $RepositoryRoot 'tests\PSMOperationsPlatform.Infrastructure.Tests\ExecutionHistoryTests.cs')
    $boundary=Get-Content -Raw (Join-Path $RepositoryRoot 'docs\architecture\Execution-History-vs-Audit.md')
    [pscustomobject]@{
        SchemaVersion=$application -match 'ExecutionHistorySchemaVersion.*Value\s*=\s*1';Migration=$migration -match 'WP0088ExecutionHistory';SixTables=(@('ExecutionRunHistory','ExecutionStepHistory','ExecutionAttemptHistory','ExecutionStateTransitionHistory','ExecutionArtifactHistory','ExecutionPolicyHistory')|Where-Object{$migration -match $_}).Count -eq 6
        NoHistoryAudit=$migration -notmatch 'audit\.|AuditLog';NoJsonStore=$migration -notmatch '(?i)EventJson|PayloadJson|MonitoringHistory';Projection=($application -match 'ExecutionHistoryProjection' -and $application -match 'Completed' -and $application -match 'Partial');Idempotency=$store -match 'ExecutionHistoryWriteDisposition\.Duplicate';Partial=$application -match 'HistoryFactsIncomplete'
        Transitions=$application -match 'TransitionSequence';Artifacts=($application -match 'ArtifactSchemaVersion' -and $application -match 'ByteCount');Provenance=($application -match 'TimeoutPolicyCode' -and $application -match 'PluginId');Queries=($store -match 'AsNoTracking' -and $store -match 'CompletedFrom' -and $store -match 'CompletedTo');Pagination=($application -match 'DefaultPageSize\s*=\s*50' -and $application -match 'MaximumPageSize\s*=\s*200' -and $store -match 'ThenBy\(x => x.ExecutionRunId\)')
        Retention=($application -match 'new\(180,\s*90,\s*90,\s*500\)' -and $application -match 'BatchSize\s*>\s*5000' -and $store -match 'TimeProvider');Cleanup=($store -match 'Take\(policy.BatchSize\)' -and $store -match 'CancellationToken' -and $tests -match 'cleanup_is_bounded_idempotent');NoScheduler=$store -notmatch 'SqlAgent|ScheduledTask|Timer';NoDryRun=$application -notmatch '(?i)DryRun';FailureIsolation=$tests -match 'failure';NoHistoryMetric=$application -notmatch 'Meter|Counter|Histogram';HistoryAuditBoundary=($boundary -match 'History' -and $boundary -match 'Audit' -and $boundary -match 'Execution State')
    }
}

function New-HistoryRetentionOperations {
    param([string]$RepositoryRoot)
    $sqlOperations=New-SqlTargetValidationOperations
    @{
        GetSqlResults={param($configuration,$target)@(Invoke-SqlTargetChecks $configuration $target 1 ([Environment]::MachineName) $false $false $sqlOperations)}
        GetCatalog={param($configuration,$target)
            $connection=$null;try{$connection=& $sqlOperations.OpenConnection $target;$connection.ChangeDatabase($configuration.SqlServer.Database);$queries=Get-HistoryRetentionQueries;[void](Assert-HistoryRetentionQueriesReadOnly $queries);$result=@{};foreach($key in $queries.Keys){$result[$key]=@(& $sqlOperations.Query $connection $queries[$key] @{})};$result}finally{if($connection){& $sqlOperations.CloseConnection $connection}}}
        GetFacts={Get-HistoryRepositoryFacts $RepositoryRoot}
    }
}

function Invoke-HistoryRetentionChecks {
    param([object]$Configuration,[object]$Target,[string]$RepositoryRoot,[bool]$SkipSchema,[bool]$SkipProjection,[bool]$SkipQuery,[bool]$SkipRetention,[hashtable]$Operations)
    [void](Test-HistoryRetentionConfiguration $Configuration $Target.Name);$facts=& $Operations.GetFacts;$results=New-Object Collections.Generic.List[object];$name=$Target.Name
    $results.Add((New-HistoryRetentionCheck '01.CONFIG' HistoryConfiguration 'History configuration' $name $true PASS INFO $true 'Exact schema/profile/batch and dry-run=false configuration.'))
    try{$sql=@(& $Operations.GetSqlResults $Configuration $Target);$sqlOk=-not ($sql|Where-Object{$_.Mandatory -and $_.Status -eq 'FAIL'});$results.Add((New-HistoryRetentionCheck '02.SQL' SQLConnectivity 'Reused SQL target validation' $name $sqlOk $(if($sqlOk){'PASS'}else{'FAIL'}) CRITICAL $true 'WP-007.Z.5 current-identity, Kerberos and TLS evidence.' 'Resolve SQL validator findings.' $true))}catch{$results.Add((New-OperationalExceptionResult 'H001.02.SQL' SQLConnectivity 'Reused SQL target validation' 'WP-007.Z.5 evidence.' $name $_.Exception CRITICAL $true));$sqlOk=$false}
    $catalog=$null;if($SkipSchema){foreach($item in @(@('03.SCHEMA','HistorySchemaVersion'),@('04.TABLES','HistoryTables'),@('05.INDEXES','IndexesConstraints'))){$results.Add((New-HistoryRetentionCheck $item[0] $item[1] 'Schema evidence skipped' $name $null SKIPPED HIGH 'WP-009 and catalog evidence' 'SkipSchemaChecks supplied.' 'Run approved schema validation.' $true))}}elseif($sqlOk){try{$catalog=& $Operations.GetCatalog $Configuration $Target}catch{$catalog=$null};$migrationOk=$facts.SchemaVersion -and $facts.Migration -and $catalog -and @($catalog.Migration).Count -eq 1;$results.Add((New-HistoryRetentionCheck '03.SCHEMA' HistorySchemaVersion 'History schema version and migration' $name $migrationOk $(if($migrationOk){'PASS'}else{'FAIL'}) HIGH 'schema 1 and WP0088 migration' 'Repository and deployed migration evidence compared.' 'Deploy only through approved release process.' $true));$tableNames=@($catalog.Tables|Where-Object SchemaName -eq history|ForEach-Object TableName);$tablesOk=$facts.SixTables -and $tableNames.Count -eq 6 -and $facts.NoHistoryAudit -and $facts.NoJsonStore;$results.Add((New-HistoryRetentionCheck '04.TABLES' HistoryTables 'Six authoritative history tables' $name $tablesOk $(if($tablesOk){'PASS'}else{'FAIL'}) HIGH 'six history tables; no JSON/Audit/Monitoring leakage' 'Pre-existing audit schema is allowed.' 'Correct deployed schema through approved forward migration.' $true));$indexOk=$facts.SixTables -and @($catalog.Indexes).Count -ge 6;$results.Add((New-HistoryRetentionCheck '05.INDEXES' IndexesConstraints 'Keys indexes and constraints' $name $indexOk $(if($indexOk){'PASS'}else{'FAIL'}) HIGH 'authoritative keys and query indexes' 'Read-only catalog evidence.' 'Use approved forward migration for defects.' $true))}else{foreach($item in @(@('03.SCHEMA','HistorySchemaVersion'),@('04.TABLES','HistoryTables'),@('05.INDEXES','IndexesConstraints'))){$results.Add((New-HistoryRetentionCheck $item[0] $item[1] 'Schema evidence unavailable' $name $null SKIPPED HIGH 'successful SQL prerequisite' 'SQL validation failed.' 'Resolve SQL prerequisites.' $true))}}
    $items=@(
        @('06.PROJECTION','HistoryProjectionModel','Projection model',$facts.Projection,$SkipProjection),@('07.IDEMPOTENCY','Idempotency','Idempotent duplicate handling',$facts.Idempotency,$SkipProjection),@('08.PARTIAL','PartialHistory','Partial history semantics',$facts.Partial,$SkipProjection),@('09.TRANSITIONS','StateTransitionHistory','Ordered typed transitions',$facts.Transitions,$SkipProjection),@('10.ARTIFACTS','ArtifactMetadataHistory','Bounded artifact metadata',$facts.Artifacts,$SkipProjection),@('11.PROVENANCE','PolicyPluginProvenance','Policy and plugin provenance',$facts.Provenance,$SkipProjection),@('12.QUERY','QueryContract','Bounded query contract',$facts.Queries,$SkipQuery),@('13.PAGING','Pagination','Paging 50/200 and stable order',$facts.Pagination,$SkipQuery),@('14.RETENTION','RetentionPolicy','Retention 180/90/90 and 500/5000',$facts.Retention,$SkipRetention),@('15.CLEANUP','RetentionCleanupSafety','Bounded cleanup contract not executed',($facts.Cleanup -and $facts.NoScheduler),$SkipRetention),@('17.PERFORMANCE','RetentionQueryPerformanceReadiness','Sargable bounded retention readiness',$facts.Cleanup,$SkipRetention),@('18.ISOLATION','HistoryFailureIsolation','History failure isolation',$facts.FailureIsolation,$SkipProjection),@('20.BOUNDARY','HistoryVersusAudit','History versus Audit boundary',($facts.HistoryAuditBoundary -and $facts.NoHistoryAudit),$false))
    foreach($item in $items){$status=if($item[4]){'SKIPPED'}elseif($item[3]){'PASS'}else{'FAIL'};$results.Add((New-HistoryRetentionCheck $item[0] $item[1] $item[2] $name $item[3] $status $(if($status -eq 'FAIL'){'HIGH'}elseif($status -eq 'SKIPPED'){'MEDIUM'}else{'INFO'}) $true $(if($status -eq 'SKIPPED'){'Operator skip supplied.'}else{'Existing repository contract/source/test evidence; no workload or mutation.'}) 'Restore the approved contract; validate live only with authorization.' ($status -ne 'SKIPPED')))}
    $results.Add((New-HistoryRetentionCheck '16.DRYRUN' RetentionDryRun 'Retention dry-run availability' $name $facts.NoDryRun NOT_APPLICABLE INFO $false 'No dry-run API exists and none was invented.' 'A future feature requires separate approval.' $false))
    $results.Add((New-HistoryRetentionCheck '19.MONITORING' MonitoringIntegration 'History monitoring integration' $name $facts.NoHistoryMetric NOT_APPLICABLE INFO 'no History metric required' 'Existing Monitoring remains independent; no exporter or persistence added.' $null $false))
    $blocking=@($results|Where-Object{$_.Mandatory -and $_.Status -eq 'FAIL'});$readiness=if($blocking.Count){'FAIL'}elseif(@($results|Where-Object{$_.Mandatory -and $_.Status -eq 'SKIPPED'}).Count){'WARNING'}else{'PASS'}
    $results.Add((New-HistoryRetentionCheck '21.READINESS' HistoryRetentionReadiness 'History and Retention repository readiness' $name $readiness $readiness $(if($readiness -eq 'FAIL'){'CRITICAL'}elseif($readiness -eq 'WARNING'){'MEDIUM'}else{'INFO'}) 'repository prerequisites complete' 'Live History and Retention validation remain separate and unexecuted.' 'Obtain approved live SQL evidence; destructive retention testing is separately authorized.' $true))
    $results.ToArray()
}
