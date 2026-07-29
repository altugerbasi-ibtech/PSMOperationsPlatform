[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorHost,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorInstallPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorServiceName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ExpectedServiceAccount,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$RepositoryRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$ExpectedCommit,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorExecutablePath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SqlServer,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DatabaseName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][guid]$ManagedServerId,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TargetFqdn,
    [Parameter(Mandatory)][ValidateSet('Auto','HttpsOnly','HttpOnly')][string]$ExpectedTransportPolicy,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ReadinessJsonPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$EvidenceRoot,
    [Parameter(Mandatory)][ValidateRange(1,1440)][int]$ObservationMinutes,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$LoggingConfigurationPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ExpectedMigrationId,
    [Parameter(Mandatory)][bool]$AllowedCollectorHostWarning,
    [Parameter(Mandatory)][bool]$ApprovedHttpFallback,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OperatorName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ApproverName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$RollbackOwner,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ChangeReference,
    [switch]$RequireElevation,
    [Parameter(DontShow)][hashtable]$Operations
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

function New-WP0062Check {
    param([string]$Id,[string]$Status,[string]$Summary,[object]$Evidence=$null)
    [pscustomobject][ordered]@{
        CheckId=$Id; Status=$Status; Summary=$Summary; Evidence=$Evidence
    }
}

function Get-WP0062PreflightOperations {
    @{
        PathExists = { param($path) Test-Path -LiteralPath $path }
        GetHash = { param($path) (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
        GetCommit = { param($root) (& git -C $root rev-parse HEAD 2>$null | Select-Object -First 1) }
        GetServices = { Get-CimInstance Win32_Service -Property Name,StartName,PathName,State,ProcessId }
        ReadJson = { param($path) Get-Content -Raw -LiteralPath $path | ConvertFrom-Json }
        GetEnvironmentValue = {
            param($name)
            foreach($target in @(
                [EnvironmentVariableTarget]::Process,
                [EnvironmentVariableTarget]::Machine,
                [EnvironmentVariableTarget]::User)){
                $value=[Environment]::GetEnvironmentVariable($name,$target)
                if(-not [string]::IsNullOrWhiteSpace($value)){return $value}
            }
            $null
        }
        IsElevated = {
            $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
            $principal=[Security.Principal.WindowsPrincipal]::new($identity)
            $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        }
        TestWritable = {
            param($path)
            $probe=Join-Path $path ('.wp0062-write-{0}.tmp' -f [guid]::NewGuid())
            try { [IO.File]::WriteAllText($probe,'probe'); $true }
            finally { if(Test-Path -LiteralPath $probe){Remove-Item -LiteralPath $probe -Force} }
        }
        WriteText = { param($path,$content) [IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false)) }
        MachineName = { [Environment]::MachineName }
    }
}

function Get-ServiceExecutablePath {
    param([string]$PathName)
    if([string]::IsNullOrWhiteSpace($PathName)){return $null}
    $value=$PathName.Trim()
    if($value.StartsWith('"')){
        $end=$value.IndexOf('"',1)
        if($end -gt 1){return $value.Substring(1,$end-1)}
    }
    return ($value -split '\s+',2)[0]
}

function Invoke-WP0062PreflightValidation {
    param([hashtable]$InputValues,[hashtable]$Ops)
    $checks=New-Object System.Collections.Generic.List[object]
    function Add([string]$id,[bool]$ok,[string]$pass,[string]$fail,[object]$evidence=$null,[string]$failure='FAIL'){
        $checks.Add((New-WP0062Check $id $(if($ok){'PASS'}else{$failure}) $(if($ok){$pass}else{$fail}) $evidence))
    }

    $localName=& $Ops.MachineName
    Add 'HOST.LOCAL' ($InputValues.CollectorHost -in @($localName,"$localName.$env:USERDNSDOMAIN")) `
        'Preflight is running on the declared Collector host.' `
        'Preflight must run locally on the declared Collector host.' $localName
    if($InputValues.ContainsKey('RequireElevation') -and $InputValues.RequireElevation){
        Add 'HOST.ELEVATED' ([bool](& $Ops.IsElevated)) 'Process is elevated.' `
            'Elevation is required by the approved execution procedure.'
    }
    Add 'FILES.INSTALL' ([bool](& $Ops.PathExists $InputValues.CollectorInstallPath)) `
        'Collector install path exists.' 'Collector install path is missing.'
    $exeExists=[bool](& $Ops.PathExists $InputValues.CollectorExecutablePath)
    Add 'FILES.EXECUTABLE' $exeExists 'Collector executable exists.' 'Collector executable is missing.'
    $artifactHash=$null
    if($exeExists){try{$artifactHash=& $Ops.GetHash $InputValues.CollectorExecutablePath}catch{}}
    Add 'FILES.HASH' (-not [string]::IsNullOrWhiteSpace($artifactHash)) `
        'Collector SHA-256 was captured.' 'Collector SHA-256 could not be captured.' $artifactHash
    $commit=$null
    try{$commit=([string](& $Ops.GetCommit $InputValues.RepositoryRoot)).Trim()}catch{}
    Add 'REPOSITORY.COMMIT' ($commit -match '^[0-9a-fA-F]{40}$') `
        'Repository commit was captured.' 'Repository commit could not be captured.' $commit
    Add 'REPOSITORY.MATCH' ([string]::Equals($commit,$InputValues.ExpectedCommit,'OrdinalIgnoreCase')) `
        'Repository commit matches approval.' 'Repository commit differs from approval.' $commit

    $services=@()
    try{$services=@(& $Ops.GetServices)}catch{}
    $service=@($services | Where-Object Name -eq $InputValues.CollectorServiceName)
    Add 'SERVICE.EXISTS' ($service.Count -eq 1) 'Exactly one named service exists.' `
        'The named service is missing or duplicated.' $service.Count
    if($service.Count -eq 1){
        $actualPath=Get-ServiceExecutablePath ([string]$service[0].PathName)
        $pathMatches=$false
        if(-not [string]::IsNullOrWhiteSpace($actualPath)){
            try{$pathMatches=[IO.Path]::GetFullPath($actualPath) -eq [IO.Path]::GetFullPath($InputValues.CollectorExecutablePath)}catch{}
        }
        Add 'SERVICE.PATH' $pathMatches `
            'Service executable path matches.' 'Service executable path differs.' $actualPath
        Add 'SERVICE.ACCOUNT' ([string]::Equals([string]$service[0].StartName,$InputValues.ExpectedServiceAccount,'OrdinalIgnoreCase')) `
            'Service account matches.' 'Service account differs.' ([string]$service[0].StartName)
        $sameBinary=@($services | Where-Object {
            $_.Name -ne $InputValues.CollectorServiceName -and
            (Get-ServiceExecutablePath ([string]$_.PathName)) -eq $actualPath
        })
        Add 'SERVICE.UNIQUE_BINARY' ($sameBinary.Count -eq 0) `
            'No second service uses the approved executable.' `
            'Another service uses the approved executable.' `
            (($sameBinary | ForEach-Object {$_.Name}) -join ',')
    }

    $readinessExists=[bool](& $Ops.PathExists $InputValues.ReadinessJsonPath)
    Add 'READINESS.FILE' $readinessExists 'Readiness report exists.' 'Readiness report is missing.'
    $readiness=$null
    if($readinessExists){try{$readiness=& $Ops.ReadJson $InputValues.ReadinessJsonPath}catch{}}
    $readyStatus=[string]$readiness.OverallStatus
    $readyCode=if($null -ne $readiness){[int]$readiness.ExitCode}else{-1}
    $approvedWarning=$readyStatus -eq 'WARNING' -and $readyCode -eq 1 -and
        $InputValues.AllowedCollectorHostWarning
    $acceptable=($readyStatus -eq 'READY' -and $readyCode -eq 0) -or $approvedWarning
    if($approvedWarning){
        $checks.Add((New-WP0062Check 'READINESS.RESULT' 'WARNING' `
            'Readiness WARNING/1 is explicitly approved for controlled lab use.' "$readyStatus/$readyCode"))
    }else{
        Add 'READINESS.RESULT' $acceptable 'Readiness result is READY.' `
            'Readiness must be READY/0 or explicitly approved WARNING/1.' "$readyStatus/$readyCode"
    }
    $osText=[string]$readiness.OperatingSystem
    $is2019=$osText -match '2019'
    Add 'READINESS.SERVER2019' (-not $is2019 -or $InputValues.AllowedCollectorHostWarning) `
        'Collector host support status is acknowledged.' `
        'Server 2019 requires explicit lab-only approval.' $is2019

    $envExists=-not [string]::IsNullOrWhiteSpace(
        [string](& $Ops.GetEnvironmentValue 'PSM__ConnectionStrings__OperationsDatabase'))
    Add 'CONFIG.ENVIRONMENT' $envExists 'Required environment variable exists.' `
        'Required environment variable is absent.' 'Value redacted'
    Add 'CONFIG.LOGGING' ([bool](& $Ops.PathExists $InputValues.LoggingConfigurationPath)) `
        'Logging configuration exists.' 'Logging configuration is missing.'
    $writable=$false
    if(& $Ops.PathExists $InputValues.EvidenceRoot){
        try{$writable=[bool](& $Ops.TestWritable $InputValues.EvidenceRoot)}catch{}
    }
    Add 'EVIDENCE.WRITABLE' $writable 'Evidence directory is writable.' `
        'Evidence directory is missing or not writable.'

    Add 'TARGET.INPUT' ($InputValues.ManagedServerId -ne [guid]::Empty -and
        -not [string]::IsNullOrWhiteSpace($InputValues.TargetFqdn)) `
        'Exactly one approved target identity is supplied.' 'Approved target input is incomplete.'
    Add 'CONNECTIVITY.INPUT' (-not [string]::IsNullOrWhiteSpace($InputValues.SqlServer) -and
        -not [string]::IsNullOrWhiteSpace($InputValues.DatabaseName) -and
        -not [string]::IsNullOrWhiteSpace($InputValues.ExpectedTransportPolicy)) `
        'SQL and WinRM policy inputs are present.' 'Required SQL or WinRM policy input is absent.'
    Add 'APPROVAL.INPUT' (-not [string]::IsNullOrWhiteSpace($InputValues.RollbackOwner) -and
        -not [string]::IsNullOrWhiteSpace($InputValues.ChangeReference) -and
        -not [string]::IsNullOrWhiteSpace($InputValues.ApproverName)) `
        'Approval and rollback inputs are present.' 'Approval or rollback input is absent.'

    $failures=@($checks | Where-Object Status -eq 'FAIL').Count
    $warnings=@($checks | Where-Object Status -eq 'WARNING').Count
    $overall=if($failures){'NOT_READY'}elseif($warnings){'WARNING'}else{'READY'}
    [pscustomobject][ordered]@{
        SchemaVersion='1.0'; WorkPackage='WP-006.2'
        GeneratedAt=[DateTimeOffset]::Now.ToString('o')
        OverallStatus=$overall
        ExitCode=if($failures){2}elseif($warnings){1}else{0}
        CollectorHost=$InputValues.CollectorHost
        ManagedServerId=[string]$InputValues.ManagedServerId
        TargetFqdn=$InputValues.TargetFqdn
        ExpectedTransportPolicy=$InputValues.ExpectedTransportPolicy
        ApprovedHttpFallback=$InputValues.ApprovedHttpFallback
        ObservationMinutes=$InputValues.ObservationMinutes
        ExpectedMigrationId=$InputValues.ExpectedMigrationId
        Operator=$InputValues.OperatorName
        Approver=$InputValues.ApproverName
        RollbackOwner=$InputValues.RollbackOwner
        ChangeReference=$InputValues.ChangeReference
        RepositoryCommit=$commit
        CollectorArtifactHash=$artifactHash
        Checks=$checks.ToArray()
    }
}

function Write-WP0062PreflightReports {
    param($Result,[string]$Root,[hashtable]$Ops)
    $jsonPath=Join-Path $Root 'WP-006.2-Preflight.json'
    $mdPath=Join-Path $Root 'WP-006.2-Preflight.md'
    & $Ops.WriteText $jsonPath ($Result | ConvertTo-Json -Depth 8)
    $lines=@('# WP-006.2 Preflight','','Overall: **{0}**' -f $Result.OverallStatus,'',
        '| Check | Status | Summary |','|---|---|---|')
    foreach($check in $Result.Checks){
        $lines+='| {0} | {1} | {2} |' -f $check.CheckId,$check.Status,($check.Summary -replace '\|','/')
    }
    & $Ops.WriteText $mdPath ($lines -join [Environment]::NewLine)
    [pscustomobject]@{JsonPath=$jsonPath;MarkdownPath=$mdPath}
}

if($MyInvocation.InvocationName -ne '.'){
    if(-not $Operations){$Operations=Get-WP0062PreflightOperations}
    $values=@{}; foreach($key in $PSBoundParameters.Keys){
        if($key -ne 'Operations'){$values[$key]=$PSBoundParameters[$key]}
    }
    $result=Invoke-WP0062PreflightValidation $values $Operations
    $reports=Write-WP0062PreflightReports $result $EvidenceRoot $Operations
    $result | Add-Member -NotePropertyName Reports -NotePropertyValue $reports
    $result
    exit $result.ExitCode
}
