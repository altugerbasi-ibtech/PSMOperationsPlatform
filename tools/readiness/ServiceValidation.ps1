Set-StrictMode -Version Latest

function Get-ServiceExecutablePath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PathName)
    $trimmed = $PathName.Trim()
    if ($trimmed.StartsWith('"')) {
        $end = $trimmed.IndexOf('"', 1)
        if ($end -gt 1) { return $trimmed.Substring(1, $end - 1) }
    }
    return ($trimmed -split '\s+', 2)[0]
}

function Test-ServiceReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    if (-not $Operations) {
        $Operations = @{
            GetService = { param($name) Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name.Replace("'","''")) -Property Name,DisplayName,StartMode,State,StartName,PathName,ProcessId }
            GetProcessPath = { param($id) (Get-Process -Id $id -ErrorAction Stop).Path }
        }
    }
    $results = New-Object System.Collections.Generic.List[object]
    try { $service = & $Operations.GetService $Parameters.CollectorServiceName }
    catch { $service = $null }
    if (-not $service) {
        $results.Add((New-ReadinessCheck -CheckId 'SERVICE.REGISTRATION' -Category Service -Name 'Collector service registration' `
            -Status FAIL -Severity HIGH -Summary 'Collector Windows Service registration was not found.' `
            -Evidence $Parameters.CollectorServiceName -Recommendation 'Install the approved service through the separate deployment process.' `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    $results.Add((New-ReadinessCheck -CheckId 'SERVICE.REGISTRATION' -Category Service -Name 'Collector service registration' `
        -Status PASS -Severity INFO -Summary 'Collector Windows Service registration exists.' `
        -Evidence "$($service.Name); $($service.DisplayName); StartMode=$($service.StartMode)" -Recommendation $null `
        -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    $expectedExe = [IO.Path]::GetFullPath((Join-Path $Parameters.CollectorInstallPath 'PSMOperationsPlatform.WindowsCollector.exe'))
    $actualExe = Get-ServiceExecutablePath $service.PathName
    $pathMatches = [string]::Equals([IO.Path]::GetFullPath($actualExe), $expectedExe, [StringComparison]::OrdinalIgnoreCase)
    $results.Add((New-ReadinessCheck -CheckId 'SERVICE.BINARYPATH' -Category Service -Name 'Service binary path' `
        -Status $(if ($pathMatches) {'PASS'} else {'FAIL'}) -Severity $(if ($pathMatches) {'INFO'} else {'HIGH'}) `
        -Summary $(if ($pathMatches) {'Service binary path matches the collector deployment.'} else {'Service binary path does not match the expected collector executable.'}) `
        -Evidence $actualExe -Recommendation $(if ($pathMatches) {$null} else {'Correct service registration through the approved deployment process.'}) `
        -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    $quotedSafely = $service.PathName.Trim().StartsWith('"') -or $actualExe -notmatch '\s'
    $results.Add((New-ReadinessCheck -CheckId 'SERVICE.BINARYPATH.QUOTING' -Category Service -Name 'Service path quoting' `
        -Status $(if ($quotedSafely) {'PASS'} else {'FAIL'}) -Severity $(if ($quotedSafely) {'INFO'} else {'HIGH'}) `
        -Summary $(if ($quotedSafely) {'Service binary path quoting is safe.'} else {'A service binary path containing spaces is not quoted.'}) `
        -Evidence 'Arguments were not executed.' -Recommendation $(if ($quotedSafely) {$null} else {'Correct service path quoting through the approved service deployment process.'}) `
        -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    $accountMatches = [string]::Equals($service.StartName, $Parameters.ExpectedServiceAccount, [StringComparison]::OrdinalIgnoreCase)
    $results.Add((New-ReadinessCheck -CheckId 'SERVICE.ACCOUNT' -Category Service -Name 'Service account' `
        -Status $(if ($accountMatches) {'PASS'} else {'FAIL'}) -Severity $(if ($accountMatches) {'INFO'} else {'CRITICAL'}) `
        -Summary $(if ($accountMatches) {'Service account matches the expected identity.'} else {'Service account does not match the expected identity.'}) `
        -Evidence $service.StartName -Recommendation $(if ($accountMatches) {$null} else {'Correct the service identity through the approved deployment process.'}) `
        -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    $running = $service.State -eq 'Running'
    $results.Add((New-ReadinessCheck -CheckId 'SERVICE.STATE' -Category Service -Name 'Service state' `
        -Status $(if ($running) {'PASS'} else {'WARNING'}) -Severity $(if ($running) {'INFO'} else {'LOW'}) `
        -Summary $(if ($running) {'Collector service is running.'} else {'Collector service is not running; no state change was attempted.'}) `
        -Evidence $service.State -Recommendation $(if ($running) {$null} else {'Start the service only under the separately approved smoke-test runbook.'}) `
        -IsBlocking $false -IsMandatory $false -DurationMilliseconds 0))
    if ($running -and $service.ProcessId) {
        try { $processPath = & $Operations.GetProcessPath $service.ProcessId } catch { $processPath = $null }
        $processMatches = $processPath -and [string]::Equals([IO.Path]::GetFullPath($processPath), $expectedExe, [StringComparison]::OrdinalIgnoreCase)
        $results.Add((New-ReadinessCheck -CheckId 'SERVICE.PROCESSPATH' -Category Service -Name 'Running process path' `
            -Status $(if ($processMatches) {'PASS'} else {'FAIL'}) -Severity $(if ($processMatches) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($processMatches) {'Running service process belongs to the expected deployment.'} else {'Running service process path could not be matched.'}) `
            -Evidence $processPath -Recommendation $(if ($processMatches) {$null} else {'Have the host owner investigate the running service registration.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    }
    $results.ToArray()
}
