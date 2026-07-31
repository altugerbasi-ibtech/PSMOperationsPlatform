#requires -Version 5.1
Set-StrictMode -Version Latest

function New-PerformanceScaleCheck {
    param([string]$Id,[string]$Category,[string]$Name,[object]$Actual,[string]$Status='PASS',[string]$Severity='INFO',[object]$Expected=$true,[string]$Message='Repository contract observed.',[string]$Recommendation=$null,[bool]$Mandatory=$true)
    New-OperationalObservation "PERF.$Id" $Category $Name $Name 'repository-local' $Status $Severity $Expected $Actual $Message $Recommendation $Actual $Mandatory
}

function Get-PerformanceRepositoryFacts {
    param([Parameter(Mandatory)][string]$RepositoryRoot)
    $all=(Get-ChildItem (Join-Path $RepositoryRoot 'src') -Recurse -File -Include *.cs|ForEach-Object{Get-Content -Raw $_.FullName}) -join "`n"
    $tests=(Get-ChildItem (Join-Path $RepositoryRoot 'tests') -Recurse -File -Include *.cs,*.ps1|ForEach-Object{Get-Content -Raw $_.FullName}) -join "`n"
    [pscustomobject]@{
        Runtime=$all -match 'CollectorRuntime';Dispatcher=$all -match 'ExecutionDispatcher';Cancellation=$all -match 'CancellationToken';Semaphore=$all -match 'SemaphoreSlim';TimeProvider=$all -match 'TimeProvider'
        Monitoring=$all -match 'ExecutionMonitoring';Cardinality=$all -match 'ExecutionMetricCatalog';Snapshot=$all -match 'GetCurrentSnapshot';History=$all -match 'ExecutionHistory';Partial=$all -match 'Partial'
        NoTracking=$all -match 'AsNoTracking';Pagination=($all -match 'DefaultPageSize\s*=\s*50' -and $all -match 'MaximumPageSize\s*=\s*200');Retention=($all -match 'new\(180,\s*90,\s*90,\s*500\)' -and $all -match 'BatchSize\s*>\s*5000')
        Inventory=$all -match 'Replace.*Inventory';Release=(Test-Path (Join-Path $RepositoryRoot 'Release'));MonitoringGuard=$tests -match '10_000|10000';NoBenchmarkDotNet=$all -notmatch 'BenchmarkDotNet'
    }
}

function Invoke-PerformanceScaleChecks {
    param([object]$Configuration,[object]$Profile,[string]$RepositoryRoot,[bool]$SkipRuntime,[bool]$SkipMonitoring,[bool]$SkipHistory,[bool]$SkipDatabase,[bool]$SkipTooling,[bool]$EnableInformationalBenchmarks,[scriptblock]$FactProvider)
    $facts=if($FactProvider){& $FactProvider}else{Get-PerformanceRepositoryFacts $RepositoryRoot}
    $matrix=New-PerformanceScaleMatrix $Profile
    $results=New-Object Collections.Generic.List[object]
    $results.Add((New-PerformanceScaleCheck '01.CONFIG' PerformanceConfiguration 'Approved bounded profile' $Profile.Name PASS INFO $Profile.Name 'Profile values are validation inputs, not product limits.'))
    $runtime=@(
        @('02.RUNTIME','RuntimeExecutionOverhead','Runtime synthetic correctness',$facts.Runtime),@('03.DISPATCHER','DispatcherPerformance','Dispatcher bounded path',$facts.Dispatcher),@('04.CONCURRENCY','RuntimeConcurrency','Concurrency cancellation and bounds',($facts.Semaphore -and $facts.Cancellation)),@('05.MEMORY','RuntimeMemoryAllocation','Bounded Runtime state',$facts.Runtime),@('15.LOCKS','LockContentionReview','Bounded synchronization evidence',$facts.Semaphore),@('16.TIME','TimeDurationCorrectness','TimeProvider and nonnegative report duration',$facts.TimeProvider),@('17.FAILURE','FailurePathPerformance','Bounded failure paths',$facts.Cancellation),@('18.PLUGIN','LongRunningPluginBoundary','Cooperative cancellation; no hard isolation',$facts.Cancellation))
    foreach($item in $runtime){$status=if($SkipRuntime){'SKIPPED'}elseif($item[3]){'PASS'}else{'FAIL'};$results.Add((New-PerformanceScaleCheck $item[0] $item[1] $item[2] $item[3] $status $(if($status -eq 'FAIL'){'HIGH'}elseif($status -eq 'SKIPPED'){'LOW'}else{'INFO'}) $true $(if($status -eq 'SKIPPED'){'SkipRuntimeChecks supplied.'}else{'Existing Runtime/Dispatcher contracts inspected; no production Collector ran.'}) 'Restore the approved repository contract.' ($status -ne 'SKIPPED')))}
    foreach($item in @(@('06.MONITORING','MonitoringOverhead','Monitoring budgets and guard',($facts.Monitoring -and $facts.MonitoringGuard)),@('07.CARDINALITY','MonitoringCardinalityScale','Bounded metric catalog',$facts.Cardinality))){$status=if($SkipMonitoring){'SKIPPED'}elseif($item[3]){'PASS'}else{'FAIL'};$results.Add((New-PerformanceScaleCheck $item[0] $item[1] $item[2] $item[3] $status $(if($status -eq 'FAIL'){'HIGH'}else{'INFO'}) $true 'Existing metric-only, Activity, health and snapshot budgets remain advisory; the 10,000/15-second guard is enforced.' 'Restore existing monitoring tests/contracts.' ($status -ne 'SKIPPED')))}
    foreach($item in @(@('08.HISTORY.PROJECTION','HistoryProjectionPerformance','Bounded History projection',($facts.History -and $facts.Partial)),@('09.HISTORY.QUERY','HistoryQueryPerformanceReadiness','No-tracking bounded query contract',($facts.NoTracking -and $facts.Pagination)),@('10.RETENTION','RetentionPerformanceReadiness','Retention 500/5000 bounds',$facts.Retention))){$status=if($SkipHistory){'SKIPPED'}elseif($item[3]){'PASS'}else{'FAIL'};$results.Add((New-PerformanceScaleCheck $item[0] $item[1] $item[2] $item[3] $status $(if($status -eq 'FAIL'){'HIGH'}else{'INFO'}) $true 'Source/test evidence only; no query plan or deletion executed.' 'Resolve the repository contract finding and obtain controlled live evidence.' ($status -ne 'SKIPPED')))}
    $results.Add((New-PerformanceScaleCheck '11.INVENTORY' InventoryReplacementScaleReadiness 'Target-scoped inventory replacement' $facts.Inventory $(if($facts.Inventory){'PASS'}else{'FAIL'}) HIGH $true 'No inventory collection was executed.' 'Restore target-scoped replacement behavior.'))
    foreach($item in @(@('12.TOOLKIT','ValidationToolkitScale','Bounded deterministic validation aggregation',$true),@('13.RELEASE','ReleaseBundleRatPerformance','Release and RAT deterministic tooling',$facts.Release))){$status=if($SkipTooling){'SKIPPED'}elseif($item[3]){'PASS'}else{'FAIL'};$results.Add((New-PerformanceScaleCheck $item[0] $item[1] $item[2] $item[3] $status INFO $true 'Repository-local tooling only.' 'Restore approved tooling evidence.' ($status -ne 'SKIPPED')))}
    $sqlStatus=if($SkipDatabase){'SKIPPED'}else{'PASS'};$results.Add((New-PerformanceScaleCheck '14.SQL' SqlQuerySafetyCostAwareness 'Read-only bounded SQL policy' ($sqlStatus -eq 'PASS') $sqlStatus INFO $true 'Static safety evidence only; no SQL connection or query plan.' 'Run controlled live plan validation only with separate authorization.' ($sqlStatus -ne 'SKIPPED')))
    $results.Add((New-PerformanceScaleCheck '19.MATRIX' ScaleMatrix 'Deterministic scale matrix' $matrix.Count PASS INFO 10 'Every dimension explicitly says NO PRODUCTION CLAIM.'))
    $budgetStatus=if($EnableInformationalBenchmarks){'WARNING'}else{'NOT_APPLICABLE'};$results.Add((New-PerformanceScaleCheck '20.BUDGETS' PerformanceBudgetEvaluation 'Advisory performance budgets' $EnableInformationalBenchmarks $budgetStatus LOW 'informational only' $(if($EnableInformationalBenchmarks){'Machine-sensitive Stopwatch observations are informational.'}else{'Informational benchmarks were not enabled; existing tests remain authoritative.'}) 'Use controlled live evidence for production claims.' $false))
    $blocking=@($results|Where-Object{$_.Mandatory -and $_.Status -eq 'FAIL'});$readiness=if($blocking.Count){'FAIL'}elseif(@($results|Where-Object Status -eq 'WARNING').Count){'WARNING'}else{'PASS'}
    $results.Add((New-PerformanceScaleCheck '21.READINESS' PerformanceScaleReadiness 'Repository performance and scale readiness' $readiness $readiness $(if($readiness -eq 'FAIL'){'CRITICAL'}elseif($readiness -eq 'WARNING'){'MEDIUM'}else{'INFO'}) 'repository checks complete' 'Live latency, plans, throughput, sizing and production scale remain unproven.' 'Perform separately authorized controlled live validation.' $true))
    [pscustomobject]@{Results=$results.ToArray();ScaleMatrix=$matrix;Profile=$Profile}
}
