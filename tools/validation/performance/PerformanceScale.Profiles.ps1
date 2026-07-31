#requires -Version 5.1
Set-StrictMode -Version Latest

function Get-PerformanceValidationProfile {
    param([Parameter(Mandatory)][ValidateSet('Smoke','Standard','Extended')][string]$Name)
    $profiles=@{
        Smoke=@(5,10,5,1,1,3,'normally under 2 minutes')
        Standard=@(20,100,10,2,2,5,'normally under 10 minutes')
        Extended=@(100,1000,20,4,3,7,'bounded maximum 30 minutes')
    }
    $value=$profiles[$Name]
    [pscustomobject][ordered]@{Name=$Name;SyntheticTargetCount=$value[0];SyntheticRunCount=$value[1];SyntheticStepCount=$value[2];MaximumParallelism=$value[3];WarmupIterations=$value[4];MeasurementIterations=$value[5];ExpectedDuration=$value[6]}
}

function Test-PerformanceValidationConfiguration {
    param([Parameter(Mandatory)][object]$Configuration,[string]$ProfileOverride)
    if($null -eq $Configuration.PerformanceValidation){throw 'PerformanceValidation configuration is required.'}
    $configured=$Configuration.PerformanceValidation
    $profile=Get-PerformanceValidationProfile $(if($ProfileOverride){$ProfileOverride}else{$configured.ValidationProfile})
    if(-not $ProfileOverride){foreach($name in @('SyntheticTargetCount','SyntheticRunCount','SyntheticStepCount','MaximumParallelism','WarmupIterations','MeasurementIterations')){if($configured.$name -ne $profile.$name){throw "PerformanceValidation values must exactly match the approved $($profile.Name) profile."}}}
    if($configured.LivePerformanceValidationEnabled -or $configured.QueryPlanValidationEnabled){throw 'Live performance and query-plan validation require separate authorization.'}
    $serialized=$configured|ConvertTo-Json -Depth 5
    if($serialized -match '(?i)password|secret|token|credential|connectionstring|unlimited'){throw 'Secret-bearing or unlimited performance configuration is prohibited.'}
    $profile
}

function New-PerformanceScaleMatrix {
    param([Parameter(Mandatory)][object]$Profile)
    $dimensions=@(
        @('Configured target count',$Profile.SyntheticTargetCount,'bounded profile','live target count'),
        @('Concurrent synthetic work',$Profile.MaximumParallelism,'bounded harness','production concurrency'),
        @('Runs',$Profile.SyntheticRunCount,'bounded profile','production throughput'),
        @('Steps per run',$Profile.SyntheticStepCount,'bounded profile','production plan size'),
        @('Runtime policy parallelism','1 / 2','repository contract','live scheduler behavior'),
        @('Throttle limits','4 / 2 / 1','repository contract','live target pressure'),
        @('Monitoring events','10000','existing 15-second guard','production telemetry load'),
        @('History page size','50 / 200','repository contract','live query plans'),
        @('Retention batch size','500 / 5000','repository contract','live cleanup throughput'),
        @('Report result count',21,'one result per category','large live topology')
    )
    @($dimensions|ForEach-Object{[pscustomobject][ordered]@{Dimension=$_[0];RepositoryTestedValue=$_[1];Enforcement=$_[2];ProductionValidationNeed=$_[3];CurrentLimitation='Synthetic/local evidence only';Claim='NO PRODUCTION CLAIM'}})
}
