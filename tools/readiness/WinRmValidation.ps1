Set-StrictMode -Version Latest

function ConvertTo-WinRmFailureCategory {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Management.Automation.ErrorRecord]$ErrorRecord)
    $id = [string]$ErrorRecord.FullyQualifiedErrorId
    $type = $ErrorRecord.Exception.GetType().Name
    $safe = "$id $type $($ErrorRecord.Exception.Message)"
    if ($safe -match 'Certificate|TLS|SSL|Trust') { return 'TlsFailure' }
    if ($safe -match 'OperationCanceled|TaskCanceled|Cancellation') { return 'Cancelled' }
    if ($safe -match 'Unauthorized|Authorization') { return 'AuthorizationFailure' }
    if ($safe -match 'Authentication|LogonFailure|AccessDenied') { return 'AuthenticationFailure' }
    if ($safe -match 'NameResolution|Dns|SocketException') { return 'DnsFailure' }
    if ($safe -match 'ConnectionRefused') { return 'ConnectionRefused' }
    if ($safe -match 'Timeout') { return 'Timeout' }
    if ($safe -match 'WinRM|WSMan') { return 'WinRmUnavailable' }
    if ($safe -match 'Protocol') { return 'ProtocolFailure' }
    return 'Unexpected'
}

function Test-WinRmAttempt {
    [CmdletBinding()]
    param([string]$HostName,[int]$Port,[bool]$UseSsl,[hashtable]$Operations)
    if (-not $Operations) {
        $Operations = @{
            TestWsMan = {
                param($name,$endpointPort,$ssl)
                if ($ssl) { Test-WSMan -ComputerName $name -Port $endpointPort -UseSSL -Authentication Negotiate -ErrorAction Stop }
                else { Test-WSMan -ComputerName $name -Port $endpointPort -Authentication Negotiate -ErrorAction Stop }
            }
        }
    }
    try {
        $null = & $Operations.TestWsMan $HostName $Port $UseSsl
        return @{ Success=$true; Category='None'; Evidence='Authenticated WSMan identity response received.' }
    } catch {
        return @{ Success=$false; Category=(ConvertTo-WinRmFailureCategory $_); Evidence='WSMan failure safely classified; exception text suppressed.' }
    }
}

function Test-WinRmReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    $results = New-Object System.Collections.Generic.List[object]
    if ($Parameters.SkipWinRmAuthenticationTest) {
        $results.Add((New-ReadinessCheck -CheckId 'WINRM.AUTHENTICATION' -Category WinRM -Name 'WinRM authentication' `
            -Status SKIPPED -Severity HIGH -Summary 'Mandatory WinRM authentication was explicitly skipped.' `
            -Evidence 'No Test-WSMan call made.' -Recommendation 'Rerun without -SkipWinRmAuthenticationTest for smoke-test readiness.' `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    if ([string]::IsNullOrWhiteSpace($Parameters.TargetFqdn) -or
        [string]::IsNullOrWhiteSpace($Parameters.TransportPolicy)) {
        $results.Add((New-ReadinessCheck -CheckId 'WINRM.INPUTS' -Category WinRM -Name 'WinRM inputs' `
            -Status FAIL -Severity HIGH -Summary 'WinRM target or transport policy is missing.' `
            -Evidence 'No endpoint inferred.' -Recommendation 'Supply target and transport policy explicitly.' `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    if ($Parameters.TransportPolicy -eq 'HttpOnly') {
        $http = Test-WinRmAttempt -HostName $Parameters.TargetFqdn -Port $Parameters.WinRmHttpPort -UseSsl $false -Operations $Operations
        $results.Add((New-ReadinessCheck -CheckId 'WINRM.HTTP' -Category WinRM -Name 'HTTP WSMan' `
            -Status $(if ($http.Success) {'PASS'} else {'FAIL'}) -Severity $(if ($http.Success) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($http.Success) {'HTTP WSMan authentication succeeded.'} else {'HTTP WSMan authentication failed.'}) `
            -Evidence $http.Evidence -Recommendation $(if ($http.Success) {$null} else {'Review the classified failure without changing target configuration.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    $https = Test-WinRmAttempt -HostName $Parameters.TargetFqdn -Port $Parameters.WinRmHttpsPort -UseSsl $true -Operations $Operations
    $eligible = $https.Category -in @('TlsFailure','ConnectionRefused','Timeout','WinRmUnavailable','ProtocolFailure')
    $httpsStatus = if ($https.Success) { 'PASS' }
        elseif ($Parameters.TransportPolicy -eq 'Auto' -and $eligible) { 'WARNING' }
        else { 'FAIL' }
    $results.Add((New-ReadinessCheck -CheckId 'WINRM.HTTPS' -Category WinRM -Name 'HTTPS WSMan' `
        -Status $httpsStatus -Severity $(if ($https.Success) {'INFO'} elseif ($httpsStatus -eq 'WARNING') {'MEDIUM'} else {'HIGH'}) `
        -Summary $(if ($https.Success) {'HTTPS WSMan authentication succeeded.'} else {"HTTPS WSMan failed: $($https.Category)."}) `
        -Evidence $https.Evidence -Recommendation $(if ($https.Success) {$null} else {'Review endpoint, certificate, authorization, and policy without bypassing validation.'}) `
        -IsBlocking ($Parameters.TransportPolicy -eq 'HttpsOnly') -IsMandatory ($Parameters.TransportPolicy -eq 'HttpsOnly') -DurationMilliseconds 0))
    if ($https.Success) { return $results.ToArray() }
    if ($Parameters.TransportPolicy -eq 'Auto' -and $eligible) {
        $http = Test-WinRmAttempt -HostName $Parameters.TargetFqdn -Port $Parameters.WinRmHttpPort -UseSsl $false -Operations $Operations
        $results.Add((New-ReadinessCheck -CheckId 'WINRM.HTTP.FALLBACK' -Category WinRM -Name 'Conditional HTTP fallback' `
            -Status $(if ($http.Success) {'PASS'} else {'FAIL'}) -Severity $(if ($http.Success) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($http.Success) {'Eligible HTTPS failure was followed by successful HTTP fallback.'} else {'Eligible HTTP fallback also failed.'}) `
            -Evidence $http.Evidence -Recommendation $(if ($http.Success) {$null} else {'Review the classified endpoint failures without modifying WinRM or TrustedHosts.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    } elseif ($Parameters.TransportPolicy -eq 'Auto') {
        $results.Add((New-ReadinessCheck -CheckId 'WINRM.HTTP.FALLBACK' -Category WinRM -Name 'Conditional HTTP fallback' `
            -Status SKIPPED -Severity INFO -Summary "HTTP fallback is prohibited after $($https.Category)." `
            -Evidence 'No HTTP authentication call made.' -Recommendation 'Correct the non-fallback failure before rerunning.' `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    }
    $results.ToArray()
}
