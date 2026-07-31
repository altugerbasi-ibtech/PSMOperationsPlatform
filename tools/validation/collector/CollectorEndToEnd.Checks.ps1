#requires -Version 5.1
Set-StrictMode -Version Latest

function Copy-EndToEndResult {
    param([object]$Result,[string]$CheckId)
    New-OperationalValidationResult -CheckId $CheckId -Category $Result.Category -Name $Result.Name `
        -Description $Result.Description -Target $Result.Target -Status $Result.Status -Severity $Result.Severity `
        -StartedAt ([datetime]$Result.StartedAt) -CompletedAt ([datetime]$Result.CompletedAt) `
        -Expected $Result.Expected -Actual $Result.Actual -Message $Result.Message `
        -Recommendation $Result.Recommendation -Evidence $Result.Evidence `
        -ExceptionType $Result.ExceptionType -ExceptionMessage $Result.ExceptionMessage `
        -Mandatory ([bool]$Result.Mandatory)
}

function New-EndToEndCheck {
    param([string]$Id,[string]$Category,[string]$Name,[string]$Target,[object]$Actual,
        [string]$Status='PASS',[string]$Severity='INFO',[object]$Expected='observe',
        [string]$Message='Value observed.',[string]$Recommendation=$null,[bool]$Mandatory=$false,
        [object]$Evidence=$null)
    New-OperationalObservation $Id $Category $Name $Name $Target $Status $Severity $Expected $Actual `
        $Message $Recommendation $Evidence $Mandatory
}

function Test-CollectorEndToEndConfiguration {
    param([object]$Configuration,[string]$TargetCollector)
    if(-not [string]::Equals($Configuration.Collector.Server,$TargetCollector,[StringComparison]::OrdinalIgnoreCase)){throw 'TargetCollector must match the configured Collector.Server.'}
    if(@($Configuration.IisTargets).Count -eq 0){throw 'At least one configured IIS target is required.'}
    if(@($Configuration.SqlTargets|Where-Object ValidationEnabled).Count -eq 0){throw 'At least one enabled SQL target is required.'}
    $operations=@($Configuration.SqlTargets|Where-Object{$_.ValidationEnabled -and $_.ExpectedRole -eq 'OperationsDatabase'})
    if($operations.Count -ne 1){throw 'Exactly one enabled OperationsDatabase SQL target is required.'}
    $accounts=@($Configuration.Collector.ServiceAccount,$Configuration.Portal.ServiceAccount,$Configuration.SqlCollector.ServiceAccount)
    if(@($accounts|Group-Object{$_.ToUpperInvariant()}|Where-Object Count -gt 1).Count){throw 'Collector, Portal, and SQL Collector identities must remain distinct.'}
    if(-not $Configuration.Security.WindowsAuthentication -or -not $Configuration.Security.KerberosOnly -or -not $Configuration.Security.IncludePortInSPN){throw 'Windows authentication, Kerberos-only, and IncludePortInSPN are required.'}
    $true
}

function Get-EndToEndStatus {
    param([object[]]$Results)
    if($Results|Where-Object Status -eq FAIL){return 'FAIL'}
    if($Results|Where-Object Status -eq WARNING){return 'WARNING'}
    if($Results|Where-Object{$_.Mandatory -and $_.Status -eq 'SKIPPED'}){return 'SKIPPED'}
    'PASS'
}

function New-EndToEndPhaseSummary {
    param([string]$Id,[string]$Category,[string]$Name,[string]$Target,[object[]]$Constituents,[bool]$Mandatory=$true)
    if(@($Constituents).Count -eq 0){return New-EndToEndCheck $Id $Category $Name $Target 'missing constituent evidence' FAIL CRITICAL 'one or more required constituent checks' 'Required constituent evidence is missing.' 'Correct the child validation/result mapping.' $Mandatory $null}
    $status=Get-EndToEndStatus $Constituents;$mapped=if($status -eq 'SKIPPED'){'SKIPPED'}else{$status}
    New-EndToEndCheck $Id $Category $Name $Target $mapped $mapped `
        $(if($mapped -eq 'FAIL'){'HIGH'}elseif($mapped -in @('WARNING','SKIPPED')){'MEDIUM'}else{'INFO'}) `
        'all required constituent checks pass' 'Phase result derived from existing checks.' `
        'Resolve constituent findings before Collector execution.' $Mandatory (($Constituents|ForEach-Object CheckId)-join ',')
}

function New-CollectorEndToEndOperations {
    param([bool]$SkipRemoteChecks)
    $runHost={param($configuration,$collector,$outputPath,$skip)
        $ops=New-CollectorValidationOperations;$all=New-Object Collections.Generic.List[object]
        foreach($group in @(
            {Invoke-CollectorOperatingSystemChecks $configuration $collector $ops},
            {Invoke-CollectorHardwareChecks $configuration $collector $ops},
            {Invoke-CollectorPowerShellChecks $collector $ops},
            {Invoke-CollectorDotNetChecks $collector $ops},
            {Invoke-CollectorServiceChecks $collector $ops},
            {Invoke-CollectorIdentityChecks $configuration $collector $ops},
            {Invoke-CollectorWinRmChecks $configuration $collector $skip $ops},
            {Invoke-CollectorNetworkChecks $configuration $collector $skip $ops},
            {Invoke-CollectorSqlChecks $configuration $collector $ops},
            {Invoke-CollectorFileSystemChecks $configuration $outputPath $ops},
            {Invoke-CollectorLoggingChecks $configuration $outputPath $collector $ops},
            {Invoke-CollectorSecurityChecks $configuration $script:EndToEndConfigurationPath},
            {Invoke-CollectorReleaseArtifactChecks $configuration $script:EndToEndRepositoryRoot $script:EndToEndConfigurationPath}
        )){foreach($result in @(& $group)){if($null -ne $result){$all.Add($result)}}};$all.ToArray()
    }
    $operations=@{RunCollectorHost=$runHost;CurrentIdentity={ [Security.Principal.WindowsIdentity]::GetCurrent().Name }}
    if(-not $SkipRemoteChecks){
        $operations.RunIisTarget={param($configuration,$target,$index)Invoke-IisTargetChecks $configuration $target $index (New-IisValidationOperations)}
        $operations.RunSqlTarget={param($configuration,$target,$index,$source)Invoke-SqlTargetChecks $configuration $target $index $source $false $false (New-SqlTargetValidationOperations)}
    }
    $operations
}

function Invoke-CollectorEndToEndChecks {
    [CmdletBinding()]
    param([object]$Configuration,[string]$ConfigurationPath,[string]$OutputPath,[string]$TargetCollector,[bool]$SkipRemoteChecks,[hashtable]$Operations)
    [void](Test-CollectorEndToEndConfiguration $Configuration $TargetCollector)
    $results=New-Object Collections.Generic.List[object]
    $results.Add((New-EndToEndCheck 'CONFIGURATION.SHARED.VALID' DeploymentConfiguration 'Shared deployment configuration' $TargetCollector $true PASS INFO $true 'Configuration and end-to-end cross-section validation passed.' $null $true))
    $results.Add((New-EndToEndCheck 'CONFIGURATION.TARGETS.COUNT' DeploymentConfiguration 'Configured target counts' $TargetCollector "IIS=$(@($Configuration.IisTargets).Count); SQL=$(@($Configuration.SqlTargets|Where-Object ValidationEnabled).Count)"))

    try{$runCollectorHost=$Operations.RunCollectorHost;$hostRaw=@(& $runCollectorHost $Configuration $TargetCollector $OutputPath $SkipRemoteChecks);$hostResults=@();foreach($child in $hostRaw){$copy=Copy-EndToEndResult $child "HOST.$($child.CheckId)";$results.Add($copy);$hostResults+=$copy};$results.Add((New-EndToEndPhaseSummary 'PREREQUISITE.HOST' CollectorPrerequisites 'Collector host prerequisites' $TargetCollector $hostResults))}
    catch{$results.Add((New-OperationalExceptionResult 'HOST.PHASE.ERROR' Framework 'Collector host phase' 'Reuse WP-007.Z.3 host checks.' $TargetCollector $_.Exception CRITICAL $true));$hostResults=@()}

    $iisByTarget=@{}
    $iisTargets=@($Configuration.IisTargets|Sort-Object{$_.ToUpperInvariant()},{$_.ToString()})
    for($index=0;$index -lt $iisTargets.Count;$index++){$target=$iisTargets[$index];$ordinal=$index+1
        if($SkipRemoteChecks){$skip=New-EndToEndCheck ('IIS.T{0:D3}.SKIPPED' -f $ordinal) IISTargets 'IIS target validation skipped' $target $null SKIPPED HIGH 'complete WP-007.Z.4 evidence' 'SkipRemoteChecks was supplied.' 'Run approved remote validation.' $true;$results.Add($skip);$iisByTarget[$target]=@($skip);continue}
        try{$runIisTarget=$Operations.RunIisTarget;$childResults=@(& $runIisTarget $Configuration $target $ordinal);$composed=@();foreach($child in $childResults){$suffix=$child.CheckId -replace '^T\d{3}\.','';$copy=Copy-EndToEndResult $child ('IIS.T{0:D3}.{1}' -f $ordinal,$suffix);$results.Add($copy);$composed+=$copy};$summary=New-EndToEndPhaseSummary ('PREREQUISITE.IIS.T{0:D3}' -f $ordinal) CollectorPrerequisites 'IIS target prerequisites' $target $composed;$results.Add($summary);$iisByTarget[$target]=$composed}
        catch{$errorResult=New-OperationalExceptionResult ('IIS.T{0:D3}.PHASE.ERROR' -f $ordinal) Framework 'IIS target phase' 'Reuse WP-007.Z.4 checks.' $target $_.Exception CRITICAL $true;$results.Add($errorResult);$iisByTarget[$target]=@($errorResult)}
    }

    $sqlByTarget=@{};$enabledSql=@($Configuration.SqlTargets|Where-Object ValidationEnabled|Sort-Object{$_.Name.ToUpperInvariant()},{$_.Name})
    for($index=0;$index -lt $enabledSql.Count;$index++){$target=$enabledSql[$index];$ordinal=$index+1
        if($SkipRemoteChecks){$skip=New-EndToEndCheck ('SQL.T{0:D3}.SKIPPED' -f $ordinal) SQLTargets 'SQL target validation skipped' $target.Name $null SKIPPED HIGH 'complete WP-007.Z.5 evidence' 'SkipRemoteChecks was supplied.' 'Run approved SQL validation.' $true;$results.Add($skip);$sqlByTarget[$target.Name]=@($skip);continue}
        try{$runSqlTarget=$Operations.RunSqlTarget;$childResults=@(& $runSqlTarget $Configuration $target $ordinal $TargetCollector);$composed=@();foreach($child in $childResults){$suffix=$child.CheckId -replace '^T\d{3}\.','';$copy=Copy-EndToEndResult $child ('SQL.T{0:D3}.{1}' -f $ordinal,$suffix);$results.Add($copy);$composed+=$copy};$summary=New-EndToEndPhaseSummary ('PREREQUISITE.SQL.T{0:D3}' -f $ordinal) CollectorPrerequisites 'SQL target prerequisites' $target.Name $composed;$results.Add($summary);$sqlByTarget[$target.Name]=$composed}
        catch{$errorResult=New-OperationalExceptionResult ('SQL.T{0:D3}.PHASE.ERROR' -f $ordinal) Framework 'SQL target phase' 'Reuse WP-007.Z.5 checks.' $target.Name $_.Exception CRITICAL $true;$results.Add($errorResult);$sqlByTarget[$target.Name]=@($errorResult)}
    }

    $matrixResults=New-Object Collections.Generic.List[object]
    for($index=0;$index -lt $iisTargets.Count;$index++){$target=$iisTargets[$index];$source=@($iisByTarget[$target]|Where-Object{$_.CheckId -match 'CONNECTIVITY\.(DNS\.FORWARD|TCP\.WINRM|WSMAN|KERBEROS\.SESSION)$'});$edge=New-EndToEndPhaseSummary ('MATRIX.IIS.T{0:D3}' -f ($index+1)) ConnectivityMatrix 'Collector to IIS' $target $source;$results.Add($edge);$matrixResults.Add($edge)}
    for($index=0;$index -lt $enabledSql.Count;$index++){$target=$enabledSql[$index];$source=@($sqlByTarget[$target.Name]|Where-Object{$_.CheckId -match '(NETWORK\.DNS\.FORWARD|NETWORK\.TCP|CONNECTION\.(OPEN|ENDPOINT)|KERBEROS\.AUTHSCHEME|TLS\.ENCRYPTED)$'});$edge=New-EndToEndPhaseSummary ('MATRIX.SQL.T{0:D3}' -f ($index+1)) ConnectivityMatrix 'Collector to SQL' $target.Name $source;$results.Add($edge);$matrixResults.Add($edge)}
    $operationsTarget=$enabledSql|Where-Object ExpectedRole -eq OperationsDatabase;$operationsResults=@($sqlByTarget[$operationsTarget.Name]|Where-Object{$_.CheckId -match 'OPERATIONS\.(ONLINE|SCHEMA|PERMISSIONPROFILE)$'});$databaseEdge=New-EndToEndPhaseSummary 'MATRIX.OPERATIONSDATABASE' ConnectivityMatrix 'Collector to Operations database' $operationsTarget.Name $operationsResults;$results.Add($databaseEdge);$matrixResults.Add($databaseEdge)
    $portal=New-EndToEndCheck 'MATRIX.PORTAL.CONFIGURATION' ConnectivityMatrix 'Collector to Portal configuration' $Configuration.Portal.Server 'DNS/configuration only' NOT_APPLICABLE INFO 'no approved Portal TCP endpoint' 'Portal TCP reachability is not evaluated because no port is configured.' $null $false;$results.Add($portal);$matrixResults.Add($portal)

    $current=& $Operations.CurrentIdentity;$collectorIdentityOk=[string]::Equals($current,$Configuration.Collector.ServiceAccount,[StringComparison]::OrdinalIgnoreCase);$results.Add((New-EndToEndCheck 'IDENTITY.COLLECTOR.CURRENT' IdentityValidation 'Current Collector identity' $TargetCollector $current $(if($collectorIdentityOk){'PASS'}else{'FAIL'}) $(if($collectorIdentityOk){'INFO'}else{'CRITICAL'}) $Configuration.Collector.ServiceAccount 'Current identity compared with configured Windows Collector gMSA.' 'Run under the approved Collector identity.' $true))
    $distinct=@($Configuration.Collector.ServiceAccount,$Configuration.SqlCollector.ServiceAccount,$Configuration.Portal.ServiceAccount)|Select-Object -Unique;$results.Add((New-EndToEndCheck 'IDENTITY.BOUNDARIES.DISTINCT' IdentityValidation 'Distinct service identities' $TargetCollector $distinct.Count $(if($distinct.Count -eq 3){'PASS'}else{'FAIL'}) HIGH 3 'Windows Collector, SQL Collector, and Portal identities remain separate.' $null $true))
    $sqlIdentityResults=@($results|Where-Object{$_.CheckId -match '^SQL\.T\d{3}\.CONNECTION\.WINDOWSAUTH$'});$sqlIdentityOk=$sqlIdentityResults.Count -eq $enabledSql.Count -and @($sqlIdentityResults|Where-Object{![string]::Equals([string]$_.Actual,$Configuration.SqlCollector.ServiceAccount,[StringComparison]::OrdinalIgnoreCase)}).Count -eq 0;$results.Add((New-EndToEndCheck 'IDENTITY.SQLCOLLECTOR.EVIDENCE' IdentityValidation 'SQL Collector identity evidence' $Configuration.SqlCollector.Server $sqlIdentityOk $(if($sqlIdentityOk){'PASS'}else{'FAIL'}) $(if($sqlIdentityOk){'INFO'}else{'CRITICAL'}) $true 'SQL child evidence compared with the distinct SQL Collector gMSA.' 'Obtain SQL evidence under the approved separate identity without credential sharing.' $true))

    $results.Add((New-EndToEndPhaseSummary 'DATABASE.OPERATIONS.SUMMARY' OperationsDatabase 'Operations database readiness' $operationsTarget.Name $operationsResults))
    $prerequisites=@($results|Where-Object{$_.CheckId -match '^PREREQUISITE\.|^MATRIX\.(IIS|SQL|OPERATIONSDATABASE)|^IDENTITY\.'});$results.Add((New-EndToEndPhaseSummary 'PREREQUISITE.COMPLETE' CollectorPrerequisites 'Complete Collector prerequisites' $TargetCollector $prerequisites))
    $beforeReadiness=$results.ToArray();$blocking=@($beforeReadiness|Where-Object{$_.Status -eq 'FAIL' -or ($_.Mandatory -and $_.Status -eq 'SKIPPED')}|Sort-Object CheckId);$warnings=@($beforeReadiness|Where-Object Status -eq WARNING);$decision=if($SkipRemoteChecks -or $blocking.Count){'COLLECTOR NOT READY'}elseif($warnings.Count){'COLLECTOR READY WITH WARNINGS'}else{'COLLECTOR READY'};$decisionStatus=if($decision -eq 'COLLECTOR NOT READY'){'FAIL'}elseif($decision -eq 'COLLECTOR READY WITH WARNINGS'){'WARNING'}else{'PASS'};$results.Add((New-EndToEndCheck 'READINESS.DECISION' EndToEndReadiness 'Collector end-to-end readiness' $TargetCollector $decision $decisionStatus $(if($decisionStatus -eq 'FAIL'){'CRITICAL'}elseif($decisionStatus -eq 'WARNING'){'MEDIUM'}else{'INFO'}) 'all mandatory prerequisites pass' 'Deterministic advisory decision; Collector was not executed.' 'Resolve blocked reasons before separately authorized execution.' $true (($blocking|ForEach-Object CheckId)-join ',')))
    [pscustomobject]@{Results=$results.ToArray();ReadinessDecision=$decision;BlockedExecutionReasons=@($blocking|ForEach-Object{"$($_.CheckId): $($_.Message)"})}
}
