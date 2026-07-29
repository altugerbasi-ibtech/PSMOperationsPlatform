[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorHost,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorServiceName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$CollectorExecutablePath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$LoggingConfigurationPath,
    [Parameter(Mandatory)][ValidateSet('PreStart','Running','PostStop')][string]$Snapshot,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$EvidenceRoot,
    [Parameter(DontShow)][hashtable]$Operations
)

Set-StrictMode -Version 2
$ErrorActionPreference='Stop'

function Get-WP0062HostOperations {
    @{
        MachineName={ [Environment]::MachineName }
        PathExists={param($path)Test-Path -LiteralPath $path}
        GetHash={param($path)(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
        GetService={
            param($name)
            Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name.Replace("'","''")) `
                -Property Name,DisplayName,State,StartMode,StartName,PathName,ProcessId
        }
        GetOperatingSystem={
            Get-CimInstance Win32_OperatingSystem -Property Caption,Version,BuildNumber,OSArchitecture
        }
        GetProcess={param($id)Get-Process -Id $id -ErrorAction Stop}
        GetTimeZone={Get-TimeZone}
        WriteText={param($path,$content)[IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false))}
    }
}

function Get-WP0062HostSnapshot {
    param([hashtable]$InputValues,[hashtable]$Ops)
    $local=& $Ops.MachineName
    if($InputValues.CollectorHost -notin @($local,"$local.$env:USERDNSDOMAIN")){
        throw 'Host evidence must run locally on the declared Collector host.'
    }
    if(-not (& $Ops.PathExists $InputValues.EvidenceRoot)){throw 'EvidenceRoot must already exist.'}
    $service=& $Ops.GetService $InputValues.CollectorServiceName
    if($null -eq $service){throw 'Collector service was not found.'}
    $exeHash=if(& $Ops.PathExists $InputValues.CollectorExecutablePath){
        & $Ops.GetHash $InputValues.CollectorExecutablePath
    }else{$null}
    $loggingHash=if(& $Ops.PathExists $InputValues.LoggingConfigurationPath){
        & $Ops.GetHash $InputValues.LoggingConfigurationPath
    }else{$null}
    $process=$null
    if([int]$service.ProcessId -gt 0){
        try{$process=& $Ops.GetProcess ([int]$service.ProcessId)}catch{}
    }
    $os=& $Ops.GetOperatingSystem
    $tz=& $Ops.GetTimeZone
    [pscustomobject][ordered]@{
        SchemaVersion='1.0';WorkPackage='WP-006.2';Snapshot=$InputValues.Snapshot
        GeneratedAt=[DateTimeOffset]::Now.ToString('o')
        MachineName=$local
        OperatingSystem=[pscustomobject]@{
            Caption=$os.Caption;Version=$os.Version;BuildNumber=$os.BuildNumber
            Architecture=$os.OSArchitecture
        }
        PowerShellVersion=$PSVersionTable.PSVersion.ToString()
        TimeZone=[pscustomobject]@{Id=$tz.Id;DisplayName=$tz.DisplayName}
        Service=[pscustomobject]@{
            Name=$service.Name;DisplayName=$service.DisplayName;State=$service.State
            StartMode=$service.StartMode;StartName=$service.StartName
            PathName=$service.PathName;ProcessId=[int]$service.ProcessId
        }
        Executable=[pscustomobject]@{Path=$InputValues.CollectorExecutablePath;Sha256=$exeHash}
        Process=if($process){[pscustomobject]@{
            Id=$process.Id;StartTime=$process.StartTime.ToString('o')
            WorkingSetBytes=[int64]$process.WorkingSet64
            PrivateMemoryBytes=[int64]$process.PrivateMemorySize64
            HandleCount=$process.HandleCount;ThreadCount=@($process.Threads).Count
        }}else{$null}
        LoggingConfiguration=[pscustomobject]@{
            Path=$InputValues.LoggingConfigurationPath;Sha256=$loggingHash
            SinkEvidence='Repository defines Microsoft.Extensions.Logging only; record the approved lab capture specification separately.'
        }
    }
}

if($MyInvocation.InvocationName -ne '.'){
    if(-not $Operations){$Operations=Get-WP0062HostOperations}
    $values=@{};foreach($key in $PSBoundParameters.Keys){if($key -ne 'Operations'){$values[$key]=$PSBoundParameters[$key]}}
    try{
        $result=Get-WP0062HostSnapshot $values $Operations
        $path=Join-Path $EvidenceRoot "WP-006.2-Host-$Snapshot.json"
        & $Operations.WriteText $path ($result | ConvertTo-Json -Depth 8)
        $result | Add-Member -NotePropertyName EvidencePath -NotePropertyValue $path
        $result
        exit 0
    }catch{
        [pscustomobject]@{SchemaVersion='1.0';WorkPackage='WP-006.2';Snapshot=$Snapshot
            Status='NOT_READY';ErrorCode='HOST_EVIDENCE_UNAVAILABLE'}
        exit 2
    }
}
