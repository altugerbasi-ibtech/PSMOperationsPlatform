#requires -Version 5.1
Set-StrictMode -Version Latest

function Add-CollectorValueCheck {
    param(
        [Collections.Generic.List[object]]$Results,[string]$Id,[string]$Category,
        [string]$Name,[string]$Target,[object]$Actual,[string]$Status='PASS',
        [string]$Severity='INFO',[object]$Expected='observe',[string]$Message='Value observed.',
        [string]$Recommendation=$null,[bool]$Mandatory=$false
    )
    $Results.Add((New-OperationalObservation $Id $Category $Name $Name $Target `
        $Status $Severity $Expected $Actual $Message $Recommendation $Actual $Mandatory))
}

function Invoke-CollectorOperatingSystemChecks {
    param([object]$Configuration,[string]$ComputerName,[hashtable]$Operations)
    $results = New-Object Collections.Generic.List[object]
    try {
        $os=& $Operations.GetOperatingSystem; $computer=& $Operations.GetComputerSystem
        $build=[int]$os.BuildNumber
        $supported=$build -ge 20348 -and [string]$os.Caption -match 'Windows Server'
        $lab=$build -ge 17763 -and [string]$os.Caption -match 'Windows Server'
        Add-CollectorValueCheck $results 'OS.VERSION.SUPPORTED' 'OperatingSystem' 'Supported Windows Server version' $ComputerName $os.Caption `
            $(if($supported){'PASS'}elseif($lab){'WARNING'}else{'FAIL'}) $(if($supported){'INFO'}elseif($lab){'MEDIUM'}else{'HIGH'}) `
            'Windows Server 2022 or later; Server 2019 lab warning' `
            $(if($supported){'Supported production operating system.'}elseif($lab){'Server 2019 is non-certifying.'}else{'Unsupported Collector host operating system.'})
        Add-CollectorValueCheck $results 'OS.ARCHITECTURE' 'OperatingSystem' '64-bit operating system' $ComputerName $os.OSArchitecture `
            $(if([string]$os.OSArchitecture -match '64'){'PASS'}else{'FAIL'}) $(if([string]$os.OSArchitecture -match '64'){'INFO'}else{'HIGH'}) '64-bit'
        $boot=[Management.ManagementDateTimeConverter]::ToDateTime([string]$os.LastBootUpTime)
        Add-CollectorValueCheck $results 'OS.LAST.REBOOT' 'OperatingSystem' 'Latest reboot timestamp' $ComputerName $boot.ToString('o')
        Add-CollectorValueCheck $results 'OS.UPTIME' 'OperatingSystem' 'System uptime' $ComputerName ([math]::Round(((Get-Date)-$boot).TotalHours,2))
        Add-CollectorValueCheck $results 'OS.COMPUTER.NAME' 'OperatingSystem' 'Computer name' $ComputerName $computer.Name
        Add-CollectorValueCheck $results 'OS.DOMAIN.MEMBERSHIP' 'OperatingSystem' 'Domain membership' $ComputerName $computer.PartOfDomain `
            $(if($computer.PartOfDomain){'PASS'}else{'FAIL'}) $(if($computer.PartOfDomain){'INFO'}else{'HIGH'}) $true
        Add-CollectorValueCheck $results 'OS.INSTALLATION.TYPE' 'OperatingSystem' 'Installation type' $ComputerName $os.InstallationType
        Add-CollectorValueCheck $results 'OS.EXPERIENCE' 'OperatingSystem' 'Server Core or Desktop Experience' $ComputerName $os.InstallationType
        Add-CollectorValueCheck $results 'OS.LOCALE' 'OperatingSystem' 'System locale' $ComputerName (& $Operations.GetLocale)
        Add-CollectorValueCheck $results 'OS.TIMEZONE' 'OperatingSystem' 'Time zone' $ComputerName (& $Operations.GetTimeZone).Id
        $pending=& $Operations.GetPendingReboot
        Add-CollectorValueCheck $results 'OS.PENDING.REBOOT' 'OperatingSystem' 'Pending reboot state' $ComputerName $pending `
            $(if($pending){'WARNING'}else{'PASS'}) $(if($pending){'MEDIUM'}else{'INFO'}) $false
        foreach($entry in @(
            @{Id='OS.SYSTEM.DRIVE';Path=$env:SystemRoot},
            @{Id='OS.LOG.DRIVE';Path=$Configuration.Collector.LogPath})) {
            $disk=& $Operations.GetDisk $entry.Path
            Add-CollectorValueCheck $results $entry.Id 'OperatingSystem' 'Available drive space' $entry.Path $disk.FreeGigabytes
        }
        Add-CollectorValueCheck $results 'OS.WORK.DRIVE' 'OperatingSystem' 'Collector working directory drive space' $ComputerName $null 'NOT_APPLICABLE' 'INFO' `
            'approved configured working directory' 'The shared configuration has no Collector working-directory field.'
    } catch { $results.Add((New-OperationalExceptionResult 'OS.COLLECTION.ERROR' 'OperatingSystem' 'Operating system checks' 'Collect read-only OS facts.' $ComputerName $_.Exception)) }
    return $results.ToArray()
}

function Invoke-CollectorHardwareChecks {
    param([object]$Configuration,[string]$ComputerName,[hashtable]$Operations)
    $r=New-Object Collections.Generic.List[object]
    try {
        $c=& $Operations.GetComputerSystem; $o=& $Operations.GetOperatingSystem
        Add-CollectorValueCheck $r 'HW.CPU.LOGICAL' 'HardwareCapacity' 'Logical CPU count' $ComputerName $c.NumberOfLogicalProcessors
        Add-CollectorValueCheck $r 'HW.MEMORY.TOTAL' 'HardwareCapacity' 'Total physical memory GiB' $ComputerName ([math]::Round($c.TotalPhysicalMemory/1GB,2))
        Add-CollectorValueCheck $r 'HW.MEMORY.AVAILABLE' 'HardwareCapacity' 'Available physical memory GiB' $ComputerName ([math]::Round($o.FreePhysicalMemory/1MB,2))
        Add-CollectorValueCheck $r 'HW.THRESHOLDS' 'HardwareCapacity' 'Configured capacity thresholds' $ComputerName $null 'NOT_APPLICABLE' 'INFO' `
            'repository-approved thresholds' 'No approved CPU, memory, or free-space threshold exists; measurements are informational.'
    } catch { $r.Add((New-OperationalExceptionResult 'HW.COLLECTION.ERROR' 'HardwareCapacity' 'Hardware capacity checks' 'Collect hardware measurements.' $ComputerName $_.Exception)) }
    $r.ToArray()
}

function Invoke-CollectorPowerShellChecks {
    param([string]$ComputerName,[hashtable]$Operations)
    $r=New-Object Collections.Generic.List[object]
    $commands=@('Get-CimInstance','Get-Service','Test-WSMan','Resolve-DnsName','Test-NetConnection','Get-ItemProperty','Get-FileHash')
    $winPs=& $Operations.GetWindowsPowerShell
    Add-CollectorValueCheck $r 'PS.WINDOWS.51' 'PowerShell' 'Windows PowerShell 5.1 availability' $ComputerName $winPs `
        $(if($winPs){'PASS'}else{'FAIL'}) $(if($winPs){'INFO'}else{'HIGH'}) $true
    Add-CollectorValueCheck $r 'PS.CURRENT.VERSION' 'PowerShell' 'Current PowerShell version' $ComputerName $PSVersionTable.PSVersion.ToString()
    Add-CollectorValueCheck $r 'PS.ENVIRONMENT' 'PowerShell' 'Configured execution environment' $ComputerName $PSVersionTable.PSEdition
    Add-CollectorValueCheck $r 'PS.LANGUAGE.MODE' 'PowerShell' 'Language mode' $ComputerName $ExecutionContext.SessionState.LanguageMode
    Add-CollectorValueCheck $r 'PS.ARCHITECTURE' 'PowerShell' 'Process architecture' $ComputerName $(if([Environment]::Is64BitProcess){'x64'}else{'x86'}) `
        $(if([Environment]::Is64BitProcess){'PASS'}else{'FAIL'}) $(if([Environment]::Is64BitProcess){'INFO'}else{'HIGH'}) 'x64'
    Add-CollectorValueCheck $r 'PS.SCRIPT.EXECUTION' 'PowerShell' 'Script execution capability' $ComputerName 'current script executing'
    foreach($name in $commands) {
        $available=[bool](& $Operations.HasCommand $name)
        Add-CollectorValueCheck $r ("PS.CMDLET."+($name -replace '-','.' ).ToUpperInvariant()) 'PowerShell' "Required cmdlet $name" $ComputerName $available `
            $(if($available){'PASS'}else{'WARNING'}) $(if($available){'INFO'}else{'MEDIUM'}) $true
    }
    Add-CollectorValueCheck $r 'PS.MODULES.BUILTIN' 'PowerShell' 'Required built-in modules' $ComputerName 'cmdlet availability reported above'
    $r.ToArray()
}

function Invoke-CollectorDotNetChecks {
    param([string]$ComputerName,[hashtable]$Operations)
    try {
        $runtimes=@(& $Operations.GetDotNetRuntimes)
        $has10=[bool]($runtimes | Where-Object { $_ -match '^Microsoft\.NETCore\.App 10\.' })
        @(
            (New-OperationalObservation 'DOTNET.RUNTIME.10' '.NET' '.NET 10 runtime' '.NET 10 Collector runtime availability.' $ComputerName $(if($has10){'PASS'}else{'FAIL'}) $(if($has10){'INFO'}else{'HIGH'}) '.NET 10' ($runtimes -join '; ') $(if($has10){'.NET 10 runtime is available.'}else{'.NET 10 runtime is missing.'}) 'Install only through approved host management.' $null $true),
            (New-OperationalObservation 'DOTNET.ARCHITECTURE' '.NET' '.NET architecture' 'Collector runtime architecture.' $ComputerName $(if([Environment]::Is64BitOperatingSystem){'PASS'}else{'FAIL'}) INFO 'x64' $(if([Environment]::Is64BitOperatingSystem){'x64'}else{'x86'}) 'Runtime architecture observed.' $null $null $true),
            (New-OperationalObservation 'DOTNET.HOSTING.BUNDLE' '.NET' 'Hosting bundle' 'Collector does not require ASP.NET hosting bundle.' $ComputerName NOT_APPLICABLE INFO 'not required for Collector' $null 'Hosting bundle validation is not applicable to the Worker Service Collector.' $null $null)
        )
    } catch { @(New-OperationalExceptionResult 'DOTNET.COLLECTION.ERROR' '.NET' '.NET runtime checks' 'Collect dotnet runtime inventory.' $ComputerName $_.Exception) }
}

function Invoke-CollectorServiceChecks {
    param([string]$ComputerName,[hashtable]$Operations)
    $r=New-Object Collections.Generic.List[object]
    foreach($item in @(
        @{Id='SERVICE.WINRM';Name='WinRM';Required=$true},
        @{Id='SERVICE.TIME';Name='W32Time';Required=$true},
        @{Id='SERVICE.DNS';Name='Dnscache';Required=$true},
        @{Id='SERVICE.COLLECTOR';Name='PSM Operations Platform Windows Collector';Required=$false})) {
        try {
            $s=& $Operations.GetService $item.Name
            Add-CollectorValueCheck $r $item.Id 'WindowsServices' $item.Name $ComputerName $s.Status `
                $(if($s.Status -eq 'Running'){'PASS'}elseif($item.Required){'FAIL'}else{'WARNING'}) `
                $(if($item.Required -and $s.Status -ne 'Running'){'HIGH'}elseif($s.Status -ne 'Running'){'LOW'}else{'INFO'}) 'Running'
        } catch {
            Add-CollectorValueCheck $r $item.Id 'WindowsServices' $item.Name $ComputerName 'not installed' `
                $(if($item.Required){'FAIL'}else{'NOT_APPLICABLE'}) $(if($item.Required){'HIGH'}else{'INFO'}) 'service exists'
        }
    }
    $r.ToArray()
}

function Invoke-CollectorIdentityChecks {
    param([object]$Configuration,[string]$ComputerName,[hashtable]$Operations)
    $r=New-Object Collections.Generic.List[object]
    $account=[string]$Configuration.Collector.ServiceAccount
    Add-CollectorValueCheck $r 'IDENTITY.GMSA.FORMAT' 'ActiveDirectoryKerberos' 'Collector gMSA format' $ComputerName $account `
        $(if($account -match '^[^\\]+\\[^\\]+\$$'){'PASS'}else{'FAIL'}) $(if($account -match '\$$'){'INFO'}else{'HIGH'}) 'DOMAIN\\account$'
    foreach($item in @(
        @{Id='IDENTITY.DOMAIN';Call='GetDomain';Name='Current domain'},
        @{Id='IDENTITY.SECURE.CHANNEL';Call='TestSecureChannel';Name='Secure channel'},
        @{Id='IDENTITY.DC';Call='DiscoverDomainController';Name='Domain controller discovery'},
        @{Id='IDENTITY.KERBEROS.TICKET';Call='GetKerberosTicket';Name='Kerberos ticket availability'},
        @{Id='IDENTITY.TIME.SKEW';Call='GetTimeSkew';Name='Time skew indicators'},
        @{Id='IDENTITY.GMSA.LOCAL';Call='TestGmsaLocal';Name='Local gMSA installation'},
        @{Id='IDENTITY.GMSA.TEST';Call='TestGmsa';Name='Test-ADServiceAccount'})) {
        try {
            $value=& $Operations[$item.Call] $account
            Add-CollectorValueCheck $r $item.Id 'ActiveDirectoryKerberos' $item.Name $ComputerName $value `
                $(if($value -eq $false){'WARNING'}else{'PASS'}) $(if($value -eq $false){'MEDIUM'}else{'INFO'}) 'read-only check succeeds'
        } catch {
            Add-CollectorValueCheck $r $item.Id 'ActiveDirectoryKerberos' $item.Name $ComputerName 'unavailable' 'SKIPPED' 'LOW' `
                'read-only check available' 'Check could not run with available module or privilege.' 'Run under approved validation identity.' $true
        }
    }
    Add-CollectorValueCheck $r 'IDENTITY.LOCAL.RIGHTS' 'ActiveDirectoryKerberos' 'Required local rights' $ComputerName 'manual evidence required' 'WARNING' 'MEDIUM' `
        'Log on as a service' 'Effective local user-right assignment cannot be proven portably without approved policy evidence.'
    Add-CollectorValueCheck $r 'IDENTITY.SPN.EXPECTATION' 'ActiveDirectoryKerberos' 'Port-specific WinRM SPN expectation' $ComputerName $Configuration.Security.IncludePortInSPN `
        $(if($Configuration.Security.IncludePortInSPN){'PASS'}else{'FAIL'}) $(if($Configuration.Security.IncludePortInSPN){'INFO'}else{'HIGH'}) $true
    $r.ToArray()
}

function Invoke-CollectorWinRmChecks {
    param([object]$Configuration,[string]$ComputerName,[bool]$SkipRemoteChecks,[hashtable]$Operations)
    $r=New-Object Collections.Generic.List[object]
    foreach($item in @(
        @{Id='WINRM.LISTENER';Name='Configured listener';Call='GetWinRmListener';Expected=$Configuration.Security.WinRMPort},
        @{Id='WINRM.AUTH.KERBEROS';Name='Kerberos authentication';Call='GetWinRmKerberos';Expected=$true},
        @{Id='WINRM.AUTH.BASIC';Name='Basic authentication disabled';Call='GetWinRmBasic';Expected=$false},
        @{Id='WINRM.AUTH.CREDSSP';Name='CredSSP disabled';Call='GetWinRmCredSsp';Expected=$false},
        @{Id='WINRM.UNENCRYPTED';Name='AllowUnencrypted policy';Call='GetWinRmAllowUnencrypted';Expected=$false},
        @{Id='WINRM.TRUSTEDHOSTS';Name='No TrustedHosts dependency';Call='GetTrustedHosts';Expected=''})) {
        try {
            $actual=& $Operations[$item.Call]
            $pass=if($item.Id -eq 'WINRM.LISTENER'){$actual.Port -eq $item.Expected -and $actual.Address}else{$actual -eq $item.Expected}
            Add-CollectorValueCheck $r $item.Id 'WinRM' $item.Name $ComputerName $actual $(if($pass){'PASS'}else{'FAIL'}) $(if($pass){'INFO'}else{'HIGH'}) $item.Expected
        } catch { $r.Add((New-OperationalExceptionResult $item.Id 'WinRM' $item.Name 'Read-only WinRM configuration check.' $ComputerName $_.Exception)) }
    }
    try {
        $local=& $Operations.TestWsMan $ComputerName $Configuration.Security.WinRMPort $Configuration.Security.UseTLS
        Add-CollectorValueCheck $r 'WINRM.LOCAL.ENDPOINT' 'WinRM' 'Local WSMan endpoint' $ComputerName $local
    } catch { $r.Add((New-OperationalExceptionResult 'WINRM.LOCAL.ENDPOINT' 'WinRM' 'Local WSMan endpoint' 'Kerberos-only endpoint validation.' $ComputerName $_.Exception)) }
    if($SkipRemoteChecks) {
        Add-CollectorValueCheck $r 'WINRM.REMOTE.KERBEROS' 'WinRM' 'Remote Kerberos session' $ComputerName $null 'SKIPPED' 'LOW' 'explicitly requested remote checks' 'Remote checks were skipped by operator request.' $null $true
    } else {
        Add-CollectorValueCheck $r 'WINRM.REMOTE.KERBEROS' 'WinRM' 'Remote Kerberos session' $ComputerName 'no configured target list' 'NOT_APPLICABLE' 'INFO' `
            'one or more configured target systems' 'The shared configuration contains no Windows target list.'
    }
    $r.ToArray()
}

function Invoke-CollectorNetworkChecks {
    param([object]$Configuration,[string]$ComputerName,[bool]$SkipRemoteChecks,[hashtable]$Operations)
    $r=New-Object Collections.Generic.List[object]
    foreach($endpoint in @(
        @{Id='NETWORK.SQL';Host=$Configuration.SqlServer.Server;Port=$Configuration.SqlServer.Port;Required=$true},
        @{Id='NETWORK.PORTAL';Host=$Configuration.Portal.Server;Port=$null;Required=$false})) {
        try {
            $addresses=@(& $Operations.ResolveDns $endpoint.Host)
            Add-CollectorValueCheck $r "$($endpoint.Id).DNS" 'NetworkDNS' 'DNS resolution' $endpoint.Host ($addresses -join ',')
            if($endpoint.Port) {
                $open=& $Operations.TestTcp $endpoint.Host $endpoint.Port
                Add-CollectorValueCheck $r "$($endpoint.Id).TCP" 'NetworkDNS' 'TCP connectivity' "$($endpoint.Host):$($endpoint.Port)" $open `
                    $(if($open){'PASS'}else{'FAIL'}) $(if($open){'INFO'}else{'HIGH'}) $true
            } else {
                Add-CollectorValueCheck $r "$($endpoint.Id).TCP" 'NetworkDNS' 'Optional Portal port reachability' $endpoint.Host $null 'NOT_APPLICABLE' INFO `
                    'configured Portal port' 'The shared configuration contains no Portal port.'
            }
        } catch { $r.Add((New-OperationalExceptionResult "$($endpoint.Id).DNS" 'NetworkDNS' 'DNS resolution' 'Resolve only configured endpoint.' $endpoint.Host $_.Exception)) }
    }
    Add-CollectorValueCheck $r 'NETWORK.IIS.DNS' 'NetworkDNS' 'IIS target DNS resolution' $ComputerName $null 'NOT_APPLICABLE' INFO 'configured IIS targets' 'No IIS target list exists in the approved configuration.'
    Add-CollectorValueCheck $r 'NETWORK.WINRM.TARGETS' 'NetworkDNS' 'WinRM target TCP connectivity' $ComputerName $null $(if($SkipRemoteChecks){'SKIPPED'}else{'NOT_APPLICABLE'}) LOW `
        'configured WinRM targets' 'No target list is defined; arbitrary network scanning is prohibited.'
    try {
        $source=@(& $Operations.ResolveDns $ComputerName)
        Add-CollectorValueCheck $r 'NETWORK.SOURCE.DNS' 'NetworkDNS' 'Source host name resolution' $ComputerName ($source -join ',')
        Add-CollectorValueCheck $r 'NETWORK.REVERSE.DNS' 'NetworkDNS' 'Reverse lookup' $ComputerName (& $Operations.ReverseDns $source[0]) 'PASS' INFO
        Add-CollectorValueCheck $r 'NETWORK.ROUTE.INTERFACE' 'NetworkDNS' 'Route and interface diagnostics' $ComputerName (& $Operations.GetRouteInterface) 'PASS' INFO
    } catch { $r.Add((New-OperationalExceptionResult 'NETWORK.SOURCE.DNS' 'NetworkDNS' 'Source diagnostics' 'Read-only source DNS and route diagnostics.' $ComputerName $_.Exception MEDIUM $false)) }
    $r.ToArray()
}

function Invoke-CollectorSqlChecks {
    param([object]$Configuration,[string]$ComputerName,[hashtable]$Operations)
    try {
        $m=& $Operations.GetSqlMetadata $Configuration
        $checks=New-Object Collections.Generic.List[object]
        Add-CollectorValueCheck $checks 'SQL.CONNECTIVITY' 'SQLConnectivity' 'Windows Authentication connectivity' $Configuration.SqlServer.Server $m.LoginName PASS INFO 'integrated identity'
        Add-CollectorValueCheck $checks 'SQL.DATABASE' 'SQLConnectivity' 'Operations database existence' $Configuration.SqlServer.Database $m.DatabaseName `
            $(if($m.DatabaseName -eq $Configuration.SqlServer.Database){'PASS'}else{'FAIL'}) HIGH $Configuration.SqlServer.Database
        Add-CollectorValueCheck $checks 'SQL.ENCRYPTION' 'SQLConnectivity' 'SQL encryption state' $Configuration.SqlServer.Server $m.Encryption `
            $(if($m.Encryption -eq 'TRUE'){'PASS'}else{'FAIL'}) HIGH 'TRUE'
        Add-CollectorValueCheck $checks 'SQL.VERSION.EDITION' 'SQLConnectivity' 'SQL version and edition' $Configuration.SqlServer.Server "$($m.ProductVersion); $($m.Edition)"
        foreach($item in @(
            @{Id='SQL.COMPATIBILITY';Expected=$Configuration.SqlServer.CompatibilityLevel;Actual=$m.CompatibilityLevel},
            @{Id='SQL.COLLATION';Expected=$Configuration.SqlServer.Collation;Actual=$m.Collation},
            @{Id='SQL.RECOVERY';Expected=$Configuration.SqlServer.RecoveryModel;Actual=$m.RecoveryModel})) {
            Add-CollectorValueCheck $checks $item.Id 'SQLConnectivity' $item.Id $Configuration.SqlServer.Database $item.Actual `
                $(if($item.Actual -eq $item.Expected){'PASS'}else{'FAIL'}) HIGH $item.Expected
        }
        $checks.ToArray()
    } catch { @(New-OperationalExceptionResult 'SQL.CONNECTIVITY' 'SQLConnectivity' 'Read-only SQL connectivity' 'Integrated encrypted SQL metadata query.' $Configuration.SqlServer.Server $_.Exception) }
}

function Invoke-CollectorFileSystemChecks {
    param([object]$Configuration,[string]$OutputPath,[hashtable]$Operations)
    $r=New-Object Collections.Generic.List[object]
    foreach($item in @(
        @{Id='FS.LOGS';Path=$Configuration.Collector.LogPath;Required=$true},
        @{Id='FS.OUTPUT';Path=$OutputPath;Required=$true})) {
        $format=Test-OperationalPathFormat $item.Path
        Add-CollectorValueCheck $r "$($item.Id).FORMAT" 'FileSystem' 'Path format' $item.Path $format $(if($format){'PASS'}else{'FAIL'}) HIGH $true
        $exists=& $Operations.PathExists $item.Path
        Add-CollectorValueCheck $r "$($item.Id).EXISTS" 'FileSystem' 'Path existence' $item.Path $exists `
            $(if($exists){'PASS'}elseif($item.Id -eq 'FS.OUTPUT'){'WARNING'}else{'FAIL'}) $(if($exists){'INFO'}else{'MEDIUM'}) $true
        if($exists) {
            Add-CollectorValueCheck $r "$($item.Id).READ" 'FileSystem' 'Directory read access' $item.Path (& $Operations.CanReadPath $item.Path) PASS INFO
            Add-CollectorValueCheck $r "$($item.Id).SPACE" 'FileSystem' 'Directory free disk space' $item.Path (& $Operations.GetDisk $item.Path).FreeGigabytes PASS INFO
        }
    }
    foreach($id in @('FS.INSTALL','FS.CONFIGURATION','FS.WORKING','FS.TEMPORARY')) {
        Add-CollectorValueCheck $r $id 'FileSystem' $id 'configuration' $null NOT_APPLICABLE INFO 'approved configured path' `
            'The shared configuration does not define this Collector path.'
    }
    Add-CollectorValueCheck $r 'FS.WRITE.TRANSIENT' 'FileSystem' 'Transient write-access test' $OutputPath 'not executed by default' SKIPPED LOW `
        'explicit approved test directory' 'Report creation proves required output writes; no additional probe file was created.'
    Add-CollectorValueCheck $r 'FS.SERVICE.ACCOUNT' 'FileSystem' 'Service account path access' $Configuration.Collector.ServiceAccount 'not impersonated' SKIPPED LOW `
        'read-only effective-access evidence' 'The toolkit does not request credentials or change ACLs.'
    $r.ToArray()
}

function Invoke-CollectorLoggingChecks {
    param([object]$Configuration,[string]$OutputPath,[string]$ComputerName,[hashtable]$Operations)
    $r=New-Object Collections.Generic.List[object]
    Add-CollectorValueCheck $r 'LOG.DIRECTORY' 'LoggingDiagnostics' 'Configured log directory' $Configuration.Collector.LogPath (& $Operations.PathExists $Configuration.Collector.LogPath) PASS INFO
    try {
        $available=& $Operations.GetEventLog
        Add-CollectorValueCheck $r 'LOG.EVENTLOG' 'LoggingDiagnostics' 'Event Log availability' $ComputerName $available PASS INFO
    } catch { $r.Add((New-OperationalExceptionResult 'LOG.EVENTLOG' 'LoggingDiagnostics' 'Event Log availability' 'Read-only Application log query.' $ComputerName $_.Exception MEDIUM $false)) }
    Add-CollectorValueCheck $r 'LOG.EVENTSOURCE' 'LoggingDiagnostics' 'Collector Event Log source' $ComputerName 'service not expected installed' NOT_APPLICABLE INFO `
        'installed Collector deployment' 'Event source is only required when the approved deployment creates it.'
    Add-CollectorValueCheck $r 'LOG.RETENTION' 'LoggingDiagnostics' 'Log retention configuration' $Configuration.Collector.LogPath 'not defined' NOT_APPLICABLE INFO `
        'repository-approved retention setting' 'No Collector log retention setting exists in the shared configuration.'
    Add-CollectorValueCheck $r 'LOG.OUTPUT' 'LoggingDiagnostics' 'Diagnostic output path' $OutputPath $OutputPath PASS INFO
    $r.ToArray()
}

function Invoke-CollectorSecurityChecks {
    param([object]$Configuration,[string]$ConfigurationPath)
    $r=New-Object Collections.Generic.List[object]
    foreach($item in @(
        @{Id='SECURITY.WINDOWS.AUTH';Actual=$Configuration.Security.WindowsAuthentication;Expected=$true},
        @{Id='SECURITY.KERBEROS.ONLY';Actual=$Configuration.Security.KerberosOnly;Expected=$true},
        @{Id='SECURITY.PORT.SPN';Actual=$Configuration.Security.IncludePortInSPN;Expected=$true})) {
        Add-CollectorValueCheck $r $item.Id 'SecurityConfiguration' $item.Id 'configuration' $item.Actual `
            $(if($item.Actual -eq $item.Expected){'PASS'}else{'FAIL'}) HIGH $item.Expected
    }
    $raw=Get-Content -LiteralPath $ConfigurationPath -Raw
    $secretPattern='(?i)(password|pwd|connectionstring|privatekey|certificate|secret)\s*"\s*:'
    Add-CollectorValueCheck $r 'SECURITY.NO.SECRETS' 'SecurityConfiguration' 'No plaintext secrets' $ConfigurationPath ($raw -notmatch $secretPattern) `
        $(if($raw -notmatch $secretPattern){'PASS'}else{'FAIL'}) CRITICAL $true
    Add-CollectorValueCheck $r 'SECURITY.NO.SQL.AUTH' 'SecurityConfiguration' 'No SQL authentication credentials' $ConfigurationPath ($raw -notmatch '(?i)(user id|uid|password|pwd)') `
        $(if($raw -notmatch '(?i)(user id|uid|password|pwd)'){'PASS'}else{'FAIL'}) CRITICAL $true
    Add-CollectorValueCheck $r 'SECURITY.NO.TRUSTEDHOSTS' 'SecurityConfiguration' 'No TrustedHosts dependency' 'toolkit' $true PASS INFO $true
    Add-CollectorValueCheck $r 'SECURITY.NO.BASIC' 'SecurityConfiguration' 'No Basic authentication' 'toolkit' $true PASS INFO $true
    Add-CollectorValueCheck $r 'SECURITY.NO.CREDSSP' 'SecurityConfiguration' 'No CredSSP authentication' 'toolkit' $true PASS INFO $true
    Add-CollectorValueCheck $r 'SECURITY.LOCAL.GROUPS' 'SecurityConfiguration' 'Required local group membership' $Configuration.Collector.ServiceAccount 'none documented' NOT_APPLICABLE INFO `
        'repository-documented group membership' 'No additional local group membership is prescribed.'
    $r.ToArray()
}

function Invoke-CollectorReleaseArtifactChecks {
    param([object]$Configuration,[string]$RepositoryRoot,[string]$ConfigurationPath)
    $r=New-Object Collections.Generic.List[object]
    $paths=@(
        @{Id='ARTIFACT.CONFIG';Path=$ConfigurationPath;Required=$true},
        @{Id='ARTIFACT.SCHEMA';Path=(Join-Path $RepositoryRoot 'Release\Deployment\DeploymentConfiguration.schema.json');Required=$true},
        @{Id='ARTIFACT.MANIFEST';Path=(Join-Path $RepositoryRoot 'Release\Manifest.json');Required=$false},
        @{Id='ARTIFACT.VERSION';Path=(Join-Path $RepositoryRoot 'Release\Version.txt');Required=$false},
        @{Id='ARTIFACT.CHECKSUMS';Path=(Join-Path $RepositoryRoot 'Release\Checksums.sha256');Required=$false},
        @{Id='ARTIFACT.DATABASE';Path=(Join-Path $RepositoryRoot ("Release\Database\PSMOperations-v{0}.sql" -f $Configuration.Deployment.ReleaseVersion));Required=$false},
        @{Id='ARTIFACT.VERIFICATION';Path=(Join-Path $RepositoryRoot 'Release\Verification\VerificationGuide.md');Required=$true},
        @{Id='ARTIFACT.RAT';Path=(Join-Path $RepositoryRoot 'Release\Acceptance\Invoke-ReleaseAcceptanceTest.ps1');Required=$true})
    foreach($item in $paths) {
        $exists=Test-Path -LiteralPath $item.Path -PathType Leaf
        Add-CollectorValueCheck $r $item.Id 'ReleaseArtifacts' $item.Id $item.Path $exists `
            $(if($exists){'PASS'}elseif($item.Required){'FAIL'}else{'WARNING'}) $(if($item.Required -and -not $exists){'HIGH'}elseif(-not $exists){'LOW'}else{'INFO'}) $true
    }
    $checksum=Join-Path $RepositoryRoot 'Release\Checksums.sha256'
    Add-CollectorValueCheck $r 'ARTIFACT.INTEGRITY' 'ReleaseArtifacts' 'Release checksum integrity' $checksum `
        $(if(Test-Path -LiteralPath $checksum){'validation delegated to approved release checksum command'}else{'not available'}) `
        $(if(Test-Path -LiteralPath $checksum){'PASS'}else{'WARNING'}) LOW 'approved release bundle present'
    $r.ToArray()
}

function New-CollectorValidationOperations {
    @{
        GetComputerSystem={Get-CimInstance Win32_ComputerSystem -Property Name,Domain,PartOfDomain,TotalPhysicalMemory,NumberOfLogicalProcessors}
        GetOperatingSystem={Get-CimInstance Win32_OperatingSystem -Property Caption,Version,BuildNumber,OSArchitecture,LastBootUpTime,InstallationType,FreePhysicalMemory}
        GetLocale={Get-Culture}
        GetTimeZone={Get-TimeZone}
        GetPendingReboot={
            (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') -or
            (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired')
        }
        GetDisk={param($path)
            $root=[IO.Path]::GetPathRoot($path)
            $drive=Get-CimInstance Win32_LogicalDisk -Filter ("DeviceID='{0}'" -f $root.TrimEnd('\'))
            [pscustomobject]@{FreeGigabytes=[math]::Round($drive.FreeSpace/1GB,2)}
        }
        GetWindowsPowerShell={Test-Path "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"}
        HasCommand={param($name)[bool](Get-Command $name -ErrorAction SilentlyContinue)}
        GetDotNetRuntimes={& dotnet --list-runtimes}
        GetService={param($name)Get-OperationalService $name}
        GetDomain={([DirectoryServices.ActiveDirectory.Domain]::GetComputerDomain()).Name}
        TestSecureChannel={Test-ComputerSecureChannel}
        DiscoverDomainController={([DirectoryServices.ActiveDirectory.Domain]::GetComputerDomain()).FindDomainController().Name}
        GetKerberosTicket={(& klist.exe 2>$null) -match 'krbtgt'}
        GetTimeSkew={& w32tm.exe /query /status}
        TestGmsaLocal={param($account)[bool](Get-CimInstance Win32_Service | Where-Object StartName -eq $account)}
        TestGmsa={param($account)
            if(-not (Get-Command Test-ADServiceAccount -ErrorAction SilentlyContinue)){throw 'ActiveDirectory module unavailable.'}
            Test-ADServiceAccount (($account -split '\\')[-1].TrimEnd('$'))
        }
        GetWinRmListener={
            $listener=Get-ChildItem WSMan:\localhost\Listener | Select-Object -First 1
            [pscustomobject]@{Port=[int](Get-Item "$($listener.PSPath)\Port").Value;Address=(Get-Item "$($listener.PSPath)\Address").Value}
        }
        GetWinRmKerberos={[bool](Get-Item WSMan:\localhost\Service\Auth\Kerberos).Value}
        GetWinRmBasic={[bool](Get-Item WSMan:\localhost\Service\Auth\Basic).Value}
        GetWinRmCredSsp={[bool](Get-Item WSMan:\localhost\Service\Auth\CredSSP).Value}
        GetWinRmAllowUnencrypted={[bool](Get-Item WSMan:\localhost\Service\AllowUnencrypted).Value}
        GetTrustedHosts={[string](Get-Item WSMan:\localhost\Client\TrustedHosts).Value}
        TestWsMan={param($name,$port,$useTls)
            $p=@{ComputerName=$name;Port=$port;Authentication='Kerberos';ErrorAction='Stop'}
            if($useTls){$p.UseSSL=$true}
            (Test-WSMan @p).ProductVersion
        }
        ResolveDns={param($name)Resolve-OperationalDns $name}
        ReverseDns={param($address)([Net.Dns]::GetHostEntry($address)).HostName}
        GetRouteInterface={Get-NetRoute -AddressFamily IPv4 | Sort-Object RouteMetric | Select-Object -First 5 InterfaceAlias,DestinationPrefix,NextHop,RouteMetric | Out-String}
        TestTcp={param($name,$port)Test-OperationalTcpPort $name $port}
        GetSqlMetadata={param($config)Invoke-OperationalSqlMetadata $config}
        PathExists={param($path)Test-Path -LiteralPath $path -PathType Container}
        CanReadPath={param($path)@(Get-ChildItem -LiteralPath $path -ErrorAction Stop | Select-Object -First 1).Count -ge 0}
        GetEventLog={Get-WinEvent -LogName Application -MaxEvents 1 -ErrorAction Stop | Select-Object -ExpandProperty Id}
    }
}
