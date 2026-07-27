Set-StrictMode -Version Latest

function Test-ReadinessTcpPort {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$HostName, [Parameter(Mandatory)][int]$Port)
    $client = New-Object System.Net.Sockets.TcpClient
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $async = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne(5000)) { return @{ Success=$false; Duration=$watch.ElapsedMilliseconds } }
        $client.EndConnect($async)
        return @{ Success=$true; Duration=$watch.ElapsedMilliseconds }
    } catch {
        return @{ Success=$false; Duration=$watch.ElapsedMilliseconds }
    } finally {
        $client.Dispose()
        $watch.Stop()
    }
}

function Test-NetworkEndpoint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Prefix,
        [Parameter(Mandatory)][string]$HostName,
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][bool]$Mandatory,
        [hashtable]$Operations
    )
    if (-not $Operations) {
        $Operations = @{
            Resolve = { param($name) @(Resolve-DnsName $name -Type A -ErrorAction Stop | Where-Object IPAddress | Select-Object -ExpandProperty IPAddress) }
            TestTcp = { param($name,$endpointPort) Test-ReadinessTcpPort -HostName $name -Port $endpointPort }
        }
    }
    $category = 'Network'
    try { $addresses = @(& $Operations.Resolve $HostName) } catch { $addresses = @() }
    $dnsOk = $addresses.Count -gt 0
    $dns = New-ReadinessCheck -CheckId "$Prefix.DNS" -Category $category -Name "$HostName DNS" `
        -Status $(if ($dnsOk) {'PASS'} elseif ($Mandatory) {'FAIL'} else {'WARNING'}) `
        -Severity $(if ($dnsOk) {'INFO'} elseif ($Mandatory) {'HIGH'} else {'LOW'}) `
        -Summary $(if ($dnsOk) {'Forward DNS resolution succeeded.'} else {'Forward DNS resolution failed.'}) `
        -Evidence $(if ($dnsOk) {$addresses -join ', '} else {'No IPv4 result.'}) `
        -Recommendation $(if ($dnsOk) {$null} else {'Correct DNS through the authorized infrastructure process; no IP fallback is used.'}) `
        -IsBlocking $Mandatory -IsMandatory $Mandatory -DurationMilliseconds 0
    if (-not $dnsOk) {
        $tcp = New-ReadinessCheck -CheckId "$Prefix.TCP" -Category $category -Name "$HostName TCP $Port" `
            -Status SKIPPED -Severity INFO -Summary 'TCP check was skipped because DNS failed.' `
            -Evidence 'Dependency: DNS.' -Recommendation 'Resolve the DNS failure first.' `
            -IsBlocking $Mandatory -IsMandatory $Mandatory -DurationMilliseconds 0
        return @($dns,$tcp)
    }
    $tcpResult = & $Operations.TestTcp $HostName $Port
    $tcp = New-ReadinessCheck -CheckId "$Prefix.TCP" -Category $category -Name "$HostName TCP $Port" `
        -Status $(if ($tcpResult.Success) {'PASS'} elseif ($Mandatory) {'FAIL'} else {'WARNING'}) `
        -Severity $(if ($tcpResult.Success) {'INFO'} elseif ($Mandatory) {'HIGH'} else {'LOW'}) `
        -Summary $(if ($tcpResult.Success) {'TCP endpoint is reachable.'} else {'TCP endpoint is not reachable.'}) `
        -Evidence "Port=$Port; DurationMilliseconds=$($tcpResult.Duration)" `
        -Recommendation $(if ($tcpResult.Success) {$null} else {'Have the network owner validate the existing route and firewall policy.'}) `
        -IsBlocking $Mandatory -IsMandatory $Mandatory -DurationMilliseconds $tcpResult.Duration
    @($dns,$tcp)
}

function Test-NetworkReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    $results = New-Object System.Collections.Generic.List[object]
    if ([string]::IsNullOrWhiteSpace($Parameters.TargetFqdn)) {
        $results.Add((New-ReadinessCheck -CheckId 'NETWORK.TARGET.INPUT' -Category Network -Name 'Target endpoint' `
            -Status FAIL -Severity HIGH -Summary 'Target FQDN is missing.' -Evidence 'No target inferred.' `
            -Recommendation 'Supply -TargetFqdn explicitly.' -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    } else {
        foreach ($item in @(Test-NetworkEndpoint -Prefix 'NETWORK.TARGET.HTTPS' -HostName $Parameters.TargetFqdn `
                -Port $Parameters.WinRmHttpsPort -Mandatory ($Parameters.TransportPolicy -eq 'HttpsOnly') -Operations $Operations)) {
            $results.Add($item)
        }
        if ($Parameters.TransportPolicy -in @('Auto','HttpOnly')) {
            foreach ($item in @(Test-NetworkEndpoint -Prefix 'NETWORK.TARGET.HTTP' -HostName $Parameters.TargetFqdn `
                    -Port $Parameters.WinRmHttpPort -Mandatory ($Parameters.TransportPolicy -eq 'HttpOnly') -Operations $Operations)) {
                $results.Add($item)
            }
        }
    }
    if ([string]::IsNullOrWhiteSpace($Parameters.SqlServer)) {
        $results.Add((New-ReadinessCheck -CheckId 'NETWORK.SQL.INPUT' -Category Network -Name 'SQL endpoint' `
            -Status FAIL -Severity HIGH -Summary 'SQL server is missing.' -Evidence 'No server inferred.' `
            -Recommendation 'Supply -SqlServer explicitly.' -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    } else {
        foreach ($item in @(Test-NetworkEndpoint -Prefix 'NETWORK.SQL' -HostName $Parameters.SqlServer `
                -Port $Parameters.SqlPort -Mandatory $true -Operations $Operations)) { $results.Add($item) }
    }
    $results.ToArray()
}
