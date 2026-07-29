[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SqlServer,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$DatabaseName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][guid]$ManagedServerId,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TargetFqdn,
    [Parameter(Mandatory)][ValidateSet('Baseline','PostRun','Verification')][string]$Phase,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ExpectedMigrationId,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$EvidenceRoot,
    [Parameter(DontShow)][hashtable]$Operations
)

Set-StrictMode -Version 2
$ErrorActionPreference='Stop'

function Assert-WP0062SqlInputs {
    param([string]$Server,[string]$Database,[string]$Migration)
    if($Server -notmatch '^[A-Za-z0-9._-]+(?:\\[A-Za-z0-9._-]+)?(?:,[0-9]{1,5})?$'){
        throw 'SqlServer contains unsupported characters.'
    }
    if($Database -notmatch '^[A-Za-z0-9_-]+$'){throw 'DatabaseName is invalid.'}
    if($Migration -notmatch '^[A-Za-z0-9_]+$'){throw 'ExpectedMigrationId is invalid.'}
}

function Get-WP0062SqlOperations {
    @{
        Query = {
            param($server,$database,$query)
            $output=& sqlcmd -S $server -d $database -E -b -h -1 -W -w 65535 -Q $query 2>$null
            if($LASTEXITCODE -ne 0){throw 'Read-only SQL query failed.'}
            ($output -join [Environment]::NewLine).Trim()
        }
        WriteText = { param($path,$content) [IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false)) }
        WriteCsv = { param($path,$rows) $rows | Export-Csv -LiteralPath $path -NoTypeInformation -Encoding UTF8 }
        PathExists = { param($path) Test-Path -LiteralPath $path }
    }
}

function Get-WP0062ReadOnlyQueries {
    param(
        [guid]$TargetId,
        [string]$Migration,
        [DateTime]$EvaluationTime=(Get-Date)
    )
    $id=$TargetId.ToString('D')
    $evaluation=$EvaluationTime.ToString('yyyy-MM-ddTHH:mm:ss.fff',[Globalization.CultureInfo]::InvariantCulture)
    [ordered]@{
        Migration = "SET NOCOUNT ON; SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'$Migration' FOR JSON PATH;"
        ManagedServer = @"
SET NOCOUNT ON;
DECLARE @ManagedServerId uniqueidentifier='$id';
SELECT Id,Fqdn,IsEnabled,WinRmTransportMode,WinRmHttpsPort,WinRmHttpPort,
 LastConnectivityState,LastConnectivityFailureCategory,
 ConsecutiveConnectivityFailures,LastConnectivityAttemptAt,
 LastConnectivitySuccessAt,LastSuccessfulTransport,NextConnectivityAttemptAt
FROM configuration.ManagedServer WHERE Id=@ManagedServerId FOR JSON PATH;
"@
        EligibleTargets = @"
SET NOCOUNT ON;
DECLARE @EvaluationTime datetime2(3)='$evaluation';
SELECT Id,Fqdn FROM configuration.ManagedServer
WHERE IsEnabled=1 AND (NextConnectivityAttemptAt IS NULL OR NextConnectivityAttemptAt<=@EvaluationTime)
FOR JSON PATH;
"@
        Computer = "SET NOCOUNT ON; DECLARE @ManagedServerId uniqueidentifier='$id'; SELECT ManagedServerId,ComputerName,Fqdn,DomainName,Manufacturer,Model,SerialNumber,CapturedAt FROM inventory.WindowsComputerInventory WHERE ManagedServerId=@ManagedServerId FOR JSON PATH;"
        OperatingSystem = "SET NOCOUNT ON; DECLARE @ManagedServerId uniqueidentifier='$id'; SELECT ManagedServerId,Caption,Version,BuildNumber,Architecture,InstallDate,LastBootTime,CapturedAt FROM inventory.WindowsOperatingSystemInventory WHERE ManagedServerId=@ManagedServerId FOR JSON PATH;"
        Memory = "SET NOCOUNT ON; DECLARE @ManagedServerId uniqueidentifier='$id'; SELECT ManagedServerId,TotalPhysicalMemoryBytes,CapturedAt FROM inventory.WindowsMemoryInventory WHERE ManagedServerId=@ManagedServerId FOR JSON PATH;"
        Processor = "SET NOCOUNT ON; DECLARE @ManagedServerId uniqueidentifier='$id'; SELECT StableSourceKey,Name,Manufacturer,CoreCount,LogicalProcessorCount,MaxClockSpeedMhz,CapturedAt FROM inventory.WindowsProcessorInventory WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey FOR JSON PATH;"
        Disk = "SET NOCOUNT ON; DECLARE @ManagedServerId uniqueidentifier='$id'; SELECT StableSourceKey,DiskNumber,FriendlyName,SerialNumber,SizeBytes,BusType,PartitionStyle,CapturedAt FROM inventory.WindowsDiskInventory WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey FOR JSON PATH;"
        Volume = "SET NOCOUNT ON; DECLARE @ManagedServerId uniqueidentifier='$id'; SELECT StableSourceKey,DriveLetter,FileSystem,Label,SizeBytes,FreeSpaceBytes,CapturedAt FROM inventory.WindowsVolumeInventory WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey FOR JSON PATH;"
        NetworkAdapter = "SET NOCOUNT ON; DECLARE @ManagedServerId uniqueidentifier='$id'; SELECT Id,StableSourceKey,Name,InterfaceDescription,MacAddress,LinkSpeedBitsPerSecond,OperationalStatus,CapturedAt FROM inventory.WindowsNetworkAdapterInventory WHERE ManagedServerId=@ManagedServerId ORDER BY StableSourceKey FOR JSON PATH;"
        Ipv4 = @"
SET NOCOUNT ON;
DECLARE @ManagedServerId uniqueidentifier='$id';
SELECT ip.StableSourceKey,ip.Address,ip.PrefixLength,ip.CapturedAt,
 a.StableSourceKey AS AdapterStableSourceKey
FROM inventory.WindowsIpv4AddressInventory ip
LEFT JOIN inventory.WindowsNetworkAdapterInventory a
 ON a.Id=ip.NetworkAdapterInventoryId AND a.ManagedServerId=ip.ManagedServerId
WHERE ip.ManagedServerId=@ManagedServerId ORDER BY ip.StableSourceKey FOR JSON PATH;
"@
    }
}

function Convert-WP0062JsonRows {
    param([string]$Text)
    if([string]::IsNullOrWhiteSpace($Text)){return @()}
    $value=$Text | ConvertFrom-Json
    @($value)
}

function Get-WP0062Integrity {
    param([hashtable]$Data)
    $singularDuplicates=0
    foreach($name in @('Computer','OperatingSystem','Memory')){
        if(@($Data[$name]).Count -gt 1){$singularDuplicates++}
    }
    $stableDuplicates=0
    foreach($name in @('Processor','Disk','Volume','NetworkAdapter','Ipv4')){
        $stableDuplicates+=@($Data[$name] | Group-Object StableSourceKey | Where-Object Count -gt 1).Count
    }
    $orphans=@($Data.Ipv4 | Where-Object {[string]::IsNullOrWhiteSpace([string]$_.AdapterStableSourceKey)}).Count
    [pscustomobject][ordered]@{
        SingularDuplicateGroups=$singularDuplicates
        StableKeyDuplicateGroups=$stableDuplicates
        Ipv4OrphanCount=$orphans
    }
}

function Invoke-WP0062SqlEvidence {
    param([hashtable]$InputValues,[hashtable]$Ops)
    Assert-WP0062SqlInputs $InputValues.SqlServer $InputValues.DatabaseName $InputValues.ExpectedMigrationId
    if(-not (& $Ops.PathExists $InputValues.EvidenceRoot)){throw 'EvidenceRoot must already exist.'}
    $evaluationTime=Get-Date
    $queries=Get-WP0062ReadOnlyQueries $InputValues.ManagedServerId $InputValues.ExpectedMigrationId $evaluationTime
    $data=@{}; $errors=New-Object System.Collections.Generic.List[object]
    foreach($name in $queries.Keys){
        try{$data[$name]=@(Convert-WP0062JsonRows (& $Ops.Query $InputValues.SqlServer $InputValues.DatabaseName $queries[$name]))}
        catch{$data[$name]=@();$errors.Add([pscustomobject]@{DataSet=$name;ErrorCode='QUERY_UNAVAILABLE'})}
    }
    $integrity=Get-WP0062Integrity $data
    $managed=@($data.ManagedServer)
    $eligible=@($data.EligibleTargets)
    $approvedEligible=@($eligible | Where-Object {
        [string]$_.Id -eq $InputValues.ManagedServerId.ToString() -and
        [string]::Equals([string]$_.Fqdn,$InputValues.TargetFqdn,'OrdinalIgnoreCase')
    }).Count
    $counts=[ordered]@{}
    foreach($name in @('Computer','OperatingSystem','Memory','Processor','Disk','Volume','NetworkAdapter','Ipv4')){
        $rows=@($data[$name]); $times=@($rows | ForEach-Object {$_.CapturedAt} | Where-Object {$_})
        $counts[$name]=[pscustomobject]@{
            RowCount=$rows.Count
            StableKeys=@($rows | ForEach-Object {$_.StableSourceKey} | Where-Object {$_})
            CapturedAtMinimum=if($times.Count){($times | Sort-Object | Select-Object -First 1)}else{$null}
            CapturedAtMaximum=if($times.Count){($times | Sort-Object | Select-Object -Last 1)}else{$null}
        }
    }
    $migrationPresent=@($data.Migration | Where-Object MigrationId -eq $InputValues.ExpectedMigrationId).Count -eq 1
    $status=if($errors.Count -or -not $migrationPresent -or $managed.Count -ne 1 -or
        $eligible.Count -ne 1 -or $approvedEligible -ne 1 -or
        $integrity.SingularDuplicateGroups -or $integrity.StableKeyDuplicateGroups -or $integrity.Ipv4OrphanCount){'NOT_READY'}else{'READY'}
    [pscustomobject][ordered]@{
        SchemaVersion='1.0';WorkPackage='WP-006.2';Phase=$InputValues.Phase
        GeneratedAt=[DateTimeOffset]::Now.ToString('o')
        EligibilityEvaluationTime=$evaluationTime.ToString('o')
        SqlServer=$InputValues.SqlServer
        DatabaseName=$InputValues.DatabaseName
        Status=$status
        Target=[pscustomobject]@{ManagedServerId=[string]$InputValues.ManagedServerId;TargetFqdn=$InputValues.TargetFqdn}
        ExpectedMigrationId=$InputValues.ExpectedMigrationId
        MigrationPresent=$migrationPresent
        EligibleTargetCount=$eligible.Count
        ApprovedEligibleTargetCount=$approvedEligible
        ConnectivityState=if($managed.Count -eq 1){$managed[0]}else{$null}
        Counts=$counts
        Integrity=$integrity
        QueryErrors=$errors.ToArray()
        Data=$data
    }
}

function Write-WP0062SqlEvidence {
    param($Evidence,[string]$Root,[hashtable]$Ops)
    $stem="WP-006.2-Sql-$($Evidence.Phase)"
    $json=Join-Path $Root "$stem.json";$csv=Join-Path $Root "$stem.csv"
    & $Ops.WriteText $json ($Evidence | ConvertTo-Json -Depth 12)
    $rows=foreach($property in $Evidence.Counts.PSObject.Properties){
        [pscustomobject]@{Phase=$Evidence.Phase;Boundary=$property.Name
            RowCount=$property.Value.RowCount
            StableKeys=($property.Value.StableKeys -join ';')
            CapturedAtMinimum=$property.Value.CapturedAtMinimum
            CapturedAtMaximum=$property.Value.CapturedAtMaximum}
    }
    & $Ops.WriteCsv $csv $rows
    [pscustomobject]@{JsonPath=$json;CsvPath=$csv}
}

if($MyInvocation.InvocationName -ne '.'){
    if(-not $Operations){$Operations=Get-WP0062SqlOperations}
    $values=@{};foreach($key in $PSBoundParameters.Keys){if($key -ne 'Operations'){$values[$key]=$PSBoundParameters[$key]}}
    $evidence=Invoke-WP0062SqlEvidence $values $Operations
    $paths=Write-WP0062SqlEvidence $evidence $EvidenceRoot $Operations
    $evidence | Add-Member -NotePropertyName EvidenceFiles -NotePropertyValue $paths
    $evidence
    exit $(if($evidence.Status -eq 'READY'){0}else{2})
}
