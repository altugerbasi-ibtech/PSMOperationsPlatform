Set-StrictMode -Version Latest

function Test-CollectorHostReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)

    if (-not $Operations) {
        $Operations = @{
            GetComputerSystem = { Get-CimInstance Win32_ComputerSystem -Property Name,Domain,PartOfDomain,TotalPhysicalMemory,NumberOfLogicalProcessors }
            GetOperatingSystem = { Get-CimInstance Win32_OperatingSystem -Property Version,BuildNumber,OSArchitecture,Caption }
            GetTimeZone = { Get-TimeZone }
            GetTimeService = { Get-Service -Name W32Time -ErrorAction Stop }
            GetVolume = { param($path) Get-CimInstance Win32_LogicalDisk -Filter ("DeviceID='{0}'" -f ([IO.Path]::GetPathRoot($path).TrimEnd('\'))) -Property FreeSpace,Size }
        }
    }
    $results = New-Object System.Collections.Generic.List[object]
    try {
        $computer = & $Operations.GetComputerSystem
        $os = & $Operations.GetOperatingSystem
        $supported = [Environment]::Is64BitOperatingSystem -and
            ([version]$os.Version).Major -ge 10 -and [int]$os.BuildNumber -ge 20348
        $results.Add((New-ReadinessCheck -CheckId 'HOST.OS.SUPPORTED' -Category CollectorHost `
            -Name 'Supported Windows Server host' -Status $(if ($supported) {'PASS'} else {'FAIL'}) `
            -Severity $(if ($supported) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($supported) {'Supported 64-bit Windows Server host detected.'} else {'Host does not meet the documented Windows Server 2022 minimum.'}) `
            -Evidence "$($os.Caption); build $($os.BuildNumber); $($os.OSArchitecture)" `
            -Recommendation $(if ($supported) {$null} else {'Use a supported 64-bit Windows Server 2022 or later collector host.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        $fqdn = if ($computer.PartOfDomain -and $computer.Domain) {
            "$($computer.Name).$($computer.Domain)"
        } else { $null }
        $results.Add((New-ReadinessCheck -CheckId 'HOST.DOMAIN.MEMBERSHIP' -Category CollectorHost `
            -Name 'Domain membership' -Status $(if ($fqdn) {'PASS'} else {'FAIL'}) `
            -Severity $(if ($fqdn) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($fqdn) {'Collector host is domain joined.'} else {'Collector host is not domain joined or its FQDN cannot be established.'}) `
            -Evidence $(if ($fqdn) {$fqdn} else {$computer.Name}) `
            -Recommendation $(if ($fqdn) {$null} else {'Use a domain-joined collector host and verify its DNS suffix manually.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        $timezone = & $Operations.GetTimeZone
        $isTurkey = $timezone.Id -eq 'Turkey Standard Time'
        $results.Add((New-ReadinessCheck -CheckId 'HOST.TIMEZONE' -Category CollectorHost `
            -Name 'Repository time zone' -Status $(if ($isTurkey) {'PASS'} else {'FAIL'}) `
            -Severity $(if ($isTurkey) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($isTurkey) {'Türkiye repository time-zone standard is configured.'} else {'Collector host time zone differs from the repository standard.'}) `
            -Evidence $timezone.Id -Recommendation $(if ($isTurkey) {$null} else {'Configure the approved Türkiye time zone through the controlled host process before validation.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        $timeService = & $Operations.GetTimeService
        $running = $timeService.Status -eq 'Running'
        $results.Add((New-ReadinessCheck -CheckId 'HOST.TIME.SERVICE' -Category CollectorHost `
            -Name 'Windows Time service' -Status $(if ($running) {'PASS'} else {'WARNING'}) `
            -Severity $(if ($running) {'INFO'} else {'MEDIUM'}) `
            -Summary $(if ($running) {'Windows Time service is running.'} else {'Windows Time service is not running.'}) `
            -Evidence "$($timeService.Status); $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss.fffK')" `
            -Recommendation $(if ($running) {$null} else {'Have the host owner verify time synchronization; this tool will not change service state.'}) `
            -IsBlocking $false -IsMandatory $true -DurationMilliseconds 0))
        $volume = & $Operations.GetVolume $Parameters.CollectorInstallPath
        $metrics = "MemoryBytes=$($computer.TotalPhysicalMemory); LogicalProcessors=$($computer.NumberOfLogicalProcessors); FreeBytes=$($volume.FreeSpace)"
        $results.Add((New-ReadinessCheck -CheckId 'HOST.CAPACITY.MEASURED' -Category CollectorHost `
            -Name 'Host capacity measurements' -Status PASS -Severity INFO `
            -Summary 'CPU, memory, and install-volume free space were measured without applying invented thresholds.' `
            -Evidence $metrics -Recommendation $null -IsBlocking $false -IsMandatory $false -DurationMilliseconds 0))
    } catch {
        $results.Add((New-InternalErrorCheck -CheckId 'HOST.INTERNAL.ERROR' -Category CollectorHost -Name 'Collector host checks'))
    }
    $results.ToArray()
}
