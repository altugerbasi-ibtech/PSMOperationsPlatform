[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ManifestPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputRoot,
    [Parameter(DontShow)][hashtable]$Operations
)

Set-StrictMode -Version 2
$ErrorActionPreference='Stop'

function Get-WP0062EvidenceOperations {
    @{
        PathExists={param($path)Test-Path -LiteralPath $path -PathType Leaf}
        DirectoryExists={param($path)Test-Path -LiteralPath $path -PathType Container}
        ReadText={param($path)Get-Content -Raw -LiteralPath $path}
        ReadJson={param($path)Get-Content -Raw -LiteralPath $path | ConvertFrom-Json}
        WriteText={param($path,$content)[IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false))}
    }
}

function New-WP0062EvidenceCheck {
    param([string]$Id,[bool]$Passed,[string]$Failure,[bool]$Warning=$false)
    [pscustomobject][ordered]@{
        CheckId=$Id
        Status=if($Passed){'PASS'}elseif($Warning){'WARNING'}else{'FAIL'}
        Summary=if($Passed){'Evidence requirement satisfied.'}else{$Failure}
    }
}

function Resolve-WP0062EvidencePath {
    param([string]$Root,[string]$Value)
    if([string]::IsNullOrWhiteSpace($Value) -or $Value -match '^<REQUIRED'){return $null}
    $candidate=if([IO.Path]::IsPathRooted($Value)){$Value}else{Join-Path $Root $Value}
    $full=[IO.Path]::GetFullPath($candidate)
    $rootFull=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\'
    if(-not $full.StartsWith($rootFull,[StringComparison]::OrdinalIgnoreCase)){return $null}
    $full
}

function Test-WP0062OfflineEvidence {
    param([string]$Path,[hashtable]$Ops)
    $checks=New-Object System.Collections.Generic.List[object]
    function Add([string]$id,[bool]$ok,[string]$failure,[bool]$warning=$false){
        $checks.Add((New-WP0062EvidenceCheck $id $ok $failure $warning))
    }
    if(-not (& $Ops.PathExists $Path)){
        return [pscustomobject]@{SchemaVersion='1.0';Status='NOT_READY';Checks=@(
            (New-WP0062EvidenceCheck 'MANIFEST.FILE' $false 'Manifest is missing.'))}
    }
    try{$manifest=& $Ops.ReadJson $Path}catch{
        return [pscustomobject]@{SchemaVersion='1.0';Status='NOT_READY';Checks=@(
            (New-WP0062EvidenceCheck 'MANIFEST.JSON' $false 'Manifest JSON is invalid.'))}
    }
    $root=[string]$manifest.EvidenceRoot
    Add 'MANIFEST.SCHEMA' ($manifest.SchemaVersion -eq '1.0' -and $manifest.WorkPackage -eq 'WP-006.2') 'Manifest schema/work package is invalid.'
    $required=@('ExecutionId','RepositoryCommit','CollectorArtifactHash','CollectorHost',
        'CollectorHostOperatingSystem','CollectorInstallPath','CollectorServiceName',
        'ExpectedServiceAccount','SqlServer',
        'DatabaseName','ManagedServerId','TargetFqdn','TransportPolicy','EvidenceRoot',
        'ReadinessStatus','ReadinessExitCode','ObservationMinutes',
        'Operator','Approver','RollbackOwner','ChangeReference','ConfigurationSource',
        'StartTime','StopTime')
    $missing=@($required | Where-Object {
        $property=$manifest.PSObject.Properties[$_]
        if(-not $property){return $true}
        $value=[string]$property.Value
        [string]::IsNullOrWhiteSpace($value) -or $value -match '^<REQUIRED'
    })
    Add 'MANIFEST.COMPLETE' ($missing.Count -eq 0) ('Missing manifest fields: '+($missing -join ', '))
    Add 'IDENTITY.REPOSITORY' ([string]$manifest.RepositoryCommit -match '^[0-9a-fA-F]{40}$') 'Repository commit is not recorded.'
    Add 'IDENTITY.ARTIFACT' ([string]$manifest.CollectorArtifactHash -match '^[0-9a-fA-F]{64}$') 'Artifact SHA-256 is not recorded.'
    Add 'CONFIG.SOURCE' (-not [string]::IsNullOrWhiteSpace([string]$manifest.ConfigurationSource)) 'Configuration source is not recorded.'
    Add 'TRANSPORT.POLICY' ($manifest.TransportPolicy -in @('Auto','HttpsOnly','HttpOnly')) 'Transport policy is invalid.'
    $loggingProperty=$manifest.PSObject.Properties['LoggingProfile']
    $loggingValue=if($loggingProperty){$loggingProperty.Value}else{$null}
    $loggingPathProperty=if($loggingValue){$loggingValue.PSObject.Properties['ConfigurationPath']}else{$null}
    $loggingHashProperty=if($loggingValue){$loggingValue.PSObject.Properties['OriginalHash']}else{$null}
    Add 'MANIFEST.LOGGING' ($null -ne $loggingValue -and
        $loggingPathProperty -and -not [string]::IsNullOrWhiteSpace([string]$loggingPathProperty.Value) -and
        $loggingHashProperty -and -not [string]::IsNullOrWhiteSpace([string]$loggingHashProperty.Value)) 'Logging profile metadata is incomplete.'
    $labProperty=$manifest.PSObject.Properties['LabOnlyException']
    $labValue=if($labProperty){$labProperty.Value}else{$null}
    $labApplies=$false
    if($labValue -and $labValue.PSObject.Properties['Applies']){
        $labApplies=[bool]$labValue.Applies
    }
    Add 'MANIFEST.LAB' ($null -ne $labValue -and
        $null -ne $labValue.PSObject.Properties['Applies']) 'Lab-only exception state is missing.'

    $files=@{}
    if($manifest.EvidenceFiles){
        foreach($property in $manifest.EvidenceFiles.PSObject.Properties){
            $files[$property.Name]=Resolve-WP0062EvidencePath $root ([string]$property.Value)
        }
    }
    foreach($name in @('ReadinessJson','PreflightJson','SqlBaselineJson','SqlPostRunJson',
        'SqlVerificationJson','HostPreStartJson','HostRunningJson','HostPostStopJson',
        'ModuleEvidenceJson','RestorationEvidenceJson')){
        Add "FILE.$($name.ToUpperInvariant())" ($files.ContainsKey($name) -and $files[$name] -and (& $Ops.PathExists $files[$name])) "$name is missing."
    }
    $readiness=$null;$preflight=$null;$baseline=$null;$post=$null;$verify=$null
    $hostPre=$null;$hostRun=$null;$hostPost=$null;$modules=$null;$restoration=$null
    foreach($pair in @(
        @('ReadinessJson','readiness'),@('PreflightJson','preflight'),
        @('SqlBaselineJson','baseline'),@('SqlPostRunJson','post'),
        @('SqlVerificationJson','verify'),@('HostPreStartJson','hostPre'),
        @('HostRunningJson','hostRun'),@('HostPostStopJson','hostPost'),
        @('ModuleEvidenceJson','modules'),@('RestorationEvidenceJson','restoration'))){
        if($files[$pair[0]] -and (& $Ops.PathExists $files[$pair[0]])){
            try{Set-Variable -Name $pair[1] -Value (& $Ops.ReadJson $files[$pair[0]])}catch{}
        }
    }
    if(-not $readiness){$readiness=[pscustomobject]@{OverallStatus='';ExitCode=-1}}
    if(-not $preflight){$preflight=[pscustomobject]@{OverallStatus=''}}
    $emptyTarget=[pscustomobject]@{ManagedServerId='';TargetFqdn=''}
    $emptyIntegrity=[pscustomobject]@{SingularDuplicateGroups=-1;StableKeyDuplicateGroups=-1;Ipv4OrphanCount=-1}
    if(-not $baseline){$baseline=[pscustomobject]@{Phase='';EligibleTargetCount=-1;Target=$emptyTarget;Integrity=$emptyIntegrity}}
    if(-not $post){$post=[pscustomobject]@{Phase='';EligibleTargetCount=-1;Target=$emptyTarget;Integrity=$emptyIntegrity}}
    if(-not $verify){$verify=[pscustomobject]@{Phase='';EligibleTargetCount=-1;Target=$emptyTarget;Integrity=$emptyIntegrity}}
    if(-not $hostRun){$hostRun=[pscustomobject]@{Snapshot='';Service=[pscustomobject]@{State=''}}}
    if(-not $hostPost){$hostPost=[pscustomobject]@{Snapshot='';Service=[pscustomobject]@{State=''}}}
    if(-not $modules){$modules=[pscustomobject]@{ModuleOrder=@();ModuleOutcomes=@()}}
    if(-not $restoration){$restoration=[pscustomobject]@{RestorationCompleted=$false}}
    $readyOk=($readiness.OverallStatus -eq 'READY' -and [int]$readiness.ExitCode -eq 0) -or
        ($readiness.OverallStatus -eq 'WARNING' -and [int]$readiness.ExitCode -eq 1 -and
         $labApplies)
    Add 'READINESS.RESULT' $readyOk 'Readiness is not READY or an approved WARNING.'
    Add 'PREFLIGHT.RESULT' ($preflight.OverallStatus -in @('READY','WARNING')) 'Preflight result is not acceptable.'
    Add 'TARGET.ONE' ($baseline.EligibleTargetCount -eq 1 -and
        [string]$baseline.Target.ManagedServerId -eq [string]$manifest.ManagedServerId -and
        [string]::Equals([string]$baseline.Target.TargetFqdn,[string]$manifest.TargetFqdn,'OrdinalIgnoreCase')) `
        'Baseline does not prove exactly one approved target.'
    Add 'SQL.BASELINE' ($baseline.Phase -eq 'Baseline') 'Baseline SQL phase is absent.'
    Add 'SQL.POSTRUN' ($post.Phase -eq 'PostRun') 'PostRun SQL phase is absent.'
    Add 'SQL.VERIFICATION' ($verify.Phase -eq 'Verification') 'Verification SQL phase is absent.'
    Add 'SQL.SINGULAR' ($verify.Integrity.SingularDuplicateGroups -eq 0) 'Singular duplicates were detected.'
    Add 'SQL.STABLEKEY' ($verify.Integrity.StableKeyDuplicateGroups -eq 0) 'Stable-key duplicates were detected.'
    Add 'SQL.ORPHANS' ($verify.Integrity.Ipv4OrphanCount -eq 0) 'IPv4 orphans were detected.'
    Add 'SERVICE.START' ($hostRun.Snapshot -eq 'Running' -and $hostRun.Service.State -eq 'Running') 'Running service evidence is absent.'
    Add 'SERVICE.STOP' ($hostPost.Snapshot -eq 'PostStop' -and $hostPost.Service.State -ne 'Running') 'Post-stop evidence is absent.'
    $expectedOrder=@('Computer','OperatingSystem','Memory','Processor','Disk','Volume','NetworkAdapter')
    Add 'MODULE.ORDER' (@($modules.ModuleOrder).Count -eq 7 -and
        (@($modules.ModuleOrder) -join '|') -eq ($expectedOrder -join '|')) 'Deterministic module-order evidence is absent.'
    Add 'MODULE.SUCCESS' (@($modules.ModuleOutcomes | Where-Object Outcome -ne 'Succeeded').Count -eq 0 -and
        @($modules.ModuleOutcomes).Count -eq 7) 'All seven successful module outcomes are not evidenced.'
    $originalHash=if($loggingValue -and $loggingValue.PSObject.Properties['OriginalHash']){[string]$loggingValue.OriginalHash}else{$null}
    $restoredHash=if($loggingValue -and $loggingValue.PSObject.Properties['RestoredHash']){[string]$loggingValue.RestoredHash}else{$null}
    Add 'RESTORATION' ([bool]$manifest.RestorationCompleted -and
        [bool]$restoration.RestorationCompleted -and
        -not [string]::IsNullOrWhiteSpace($originalHash) -and
        $originalHash -eq $restoredHash) 'Restoration evidence or hash match is absent.'
    $deviationsProperty=$manifest.PSObject.Properties['Deviations']
    $deviations=@()
    if($deviationsProperty){$deviations=@($deviationsProperty.Value)}
    if($deviations.Count -gt 0){
        Add 'DEVIATIONS.RECORDED' $false 'Execution contains recorded deviations that require reviewer disposition.' $true
    }else{
        Add 'DEVIATIONS.RECORDED' $true 'Execution deviations were not reviewed.'
    }
    if($labApplies){
        Add 'LAB.EXCEPTION' $false 'Lab-only host exception is recorded; evidence is non-certifying.' $true
    }else{
        Add 'LAB.EXCEPTION' $true 'Lab exception state is invalid.'
    }

    $ordered=$false
    try{
        [DateTimeOffset]$start=[DateTimeOffset]::Parse([string]$manifest.StartTime)
        [DateTimeOffset]$stop=[DateTimeOffset]::Parse([string]$manifest.StopTime)
        $ordered=$start -le $stop
    }catch{}
    Add 'TIME.ORDER' $ordered 'Execution timestamps are absent or unordered.'

    $secretFound=$false
    foreach($file in $files.Values | Where-Object {$_ -and (& $Ops.PathExists $_)}){
        try{
            $text=& $Ops.ReadText $file
            if($text -match '(?i)"(Password|ConnectionString|AccessToken|Secret|PrivateKey)"\s*:' -or
               $text -match '(?i)(Password|Integrated Security|User ID)\s*='){$secretFound=$true}
        }catch{}
    }
    Add 'SECURITY.REDACTION' (-not $secretFound) 'Obvious secret-bearing field or connection-string material was detected.'

    $fails=@($checks | Where-Object Status -eq 'FAIL').Count
    $warnings=@($checks | Where-Object Status -eq 'WARNING').Count
    [pscustomobject][ordered]@{
        SchemaVersion='1.0';WorkPackage='WP-006.2'
        GeneratedAt=[DateTimeOffset]::Now.ToString('o')
        Status=if($fails){'NOT_READY'}elseif($warnings){'WARNING'}else{'READY_FOR_REVIEW'}
        ExecutionId=[string]$manifest.ExecutionId
        Checks=$checks.ToArray()
        Limitations=@(
            'Offline validation proves only the supplied evidence.',
            'It does not independently contact or attest infrastructure.',
            'Session disposal is not proven unless the referenced evidence objectively captures it.'
        )
    }
}

if($MyInvocation.InvocationName -ne '.'){
    if(-not $Operations){$Operations=Get-WP0062EvidenceOperations}
    if(-not (& $Operations.DirectoryExists $OutputRoot)){throw 'OutputRoot must already exist.'}
    $result=Test-WP0062OfflineEvidence $ManifestPath $Operations
    $json=Join-Path $OutputRoot 'WP-006.2-Evidence-Validation.json'
    $md=Join-Path $OutputRoot 'WP-006.2-Evidence-Validation.md'
    & $Operations.WriteText $json ($result | ConvertTo-Json -Depth 8)
    $lines=@('# WP-006.2 Evidence Validation','','Result: **{0}**' -f $result.Status,'',
        '| Check | Status | Summary |','|---|---|---|')
    foreach($check in $result.Checks){$lines+='| {0} | {1} | {2} |' -f $check.CheckId,$check.Status,($check.Summary -replace '\|','/')}
    & $Operations.WriteText $md ($lines -join [Environment]::NewLine)
    $result
    exit $(if($result.Status -eq 'READY_FOR_REVIEW'){0}elseif($result.Status -eq 'WARNING'){1}else{2})
}
