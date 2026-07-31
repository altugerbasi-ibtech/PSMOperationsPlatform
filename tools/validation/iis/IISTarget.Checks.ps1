#requires -Version 5.1
Set-StrictMode -Version Latest

function New-IisCheck {
    param(
        [string]$Prefix,[string]$Id,[string]$Category,[string]$Name,[string]$Target,
        [object]$Actual,[string]$Status='PASS',[string]$Severity='INFO',
        [object]$Expected='observe',[string]$Message='Value observed.',
        [string]$Recommendation=$null,[bool]$Mandatory=$false
    )
    New-OperationalObservation "$Prefix.$Id" $Category $Name $Name $Target $Status $Severity `
        $Expected $Actual $Message $Recommendation $Actual $Mandatory
}

function New-IisSkippedCategoryChecks {
    param([string]$Prefix,[string]$Target,[string]$Reason)
    foreach ($item in @(
        @('OS','OperatingSystem'),@('INSTALL','IISInstallation'),@('CONFIG','IISConfiguration'),
        @('POOLS','ApplicationPools'),@('WORKERS','WorkerProcesses'),@('DOTNET','.NETRuntime'),
        @('LOGGING','IISLogging'),@('SECURITY','Security'),@('FILES','FileSystem'))) {
        New-IisCheck $Prefix "$($item[0]).SKIPPED" $item[1] 'Remote checks skipped' $Target `
            $null SKIPPED MEDIUM 'authenticated remote session' $Reason `
            'Resolve DNS, TCP, WinRM, and Kerberos failures, then rerun validation.' $true
    }
}

function Get-IisRemoteSnapshotScript {
    {
        $ErrorActionPreference='Stop'
        $os=Get-CimInstance -ClassName Win32_OperatingSystem
        $computer=Get-CimInstance -ClassName Win32_ComputerSystem
        $pending=@(
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending',
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') |
            Where-Object { Test-Path -LiteralPath $_ }
        $iisService=Get-Service -Name W3SVC -ErrorAction SilentlyContinue
        $webFeature=Get-WindowsFeature -Name Web-Server -ErrorAction SilentlyContinue
        $assemblyAvailable=$false
        $manager=$null
        try {
            Add-Type -AssemblyName Microsoft.Web.Administration -ErrorAction Stop
            $manager=[Microsoft.Web.Administration.ServerManager]::OpenRemote([Environment]::MachineName)
            $assemblyAvailable=$true
        } catch { $assemblyAvailable=$false }

        $sites=@();$applications=@();$virtualDirectories=@();$bindings=@();$pools=@();$workers=@()
        if($manager){
            foreach($site in $manager.Sites){
                $sites+= [pscustomobject]@{Name=$site.Name;State=[string]$site.State;Id=$site.Id;LogEnabled=$site.LogFile.Enabled;LogDirectory=[string]$site.LogFile.Directory}
                foreach($binding in $site.Bindings){
                    $parts=([string]$binding.BindingInformation).Split(':',3)
                    $bindings+=[pscustomobject]@{Site=$site.Name;Protocol=[string]$binding.Protocol;BindingInformation=[string]$binding.BindingInformation;HostHeader=$(if($parts.Count -eq 3){$parts[2]}else{''});CertificateHash=$(if($binding.CertificateHash){[Convert]::ToBase64String($binding.CertificateHash)}else{$null});ClientCertificateMode=[string]$site.GetWebConfiguration().GetSection('system.webServer/security/access').Attributes['sslFlags'].Value}
                }
                foreach($application in $site.Applications){
                    $applications+=[pscustomobject]@{Site=$site.Name;Path=[string]$application.Path;ApplicationPoolName=[string]$application.ApplicationPoolName}
                    foreach($directory in $application.VirtualDirectories){
                        $virtualDirectories+=[pscustomobject]@{Site=$site.Name;Application=[string]$application.Path;Path=[string]$directory.Path;PhysicalPath=[string]$directory.PhysicalPath}
                    }
                }
            }
            foreach($pool in $manager.ApplicationPools){
                $identity=[string]$pool.ProcessModel.IdentityType
                $userName=if($identity -eq 'SpecificUser') {[string]$pool.ProcessModel.UserName} else {$identity}
                $pools+=[pscustomobject]@{Name=$pool.Name;RuntimeVersion=[string]$pool.ManagedRuntimeVersion;PipelineMode=[string]$pool.ManagedPipelineMode;IdentityType=$identity;UserName=$userName;State=[string]$pool.State;AutoStart=[bool]$pool.AutoStart;PeriodicRestartMinutes=[double]$pool.Recycling.PeriodicRestart.Time.TotalMinutes}
            }
            foreach($worker in $manager.WorkerProcesses){$workers+=[pscustomobject]@{ProcessId=$worker.ProcessId;ApplicationPoolName=$worker.AppPoolName;State='Running'}}
        }

        $framework=@()
        foreach($path in @('HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','HKLM:\SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full')){
            if(Test-Path -LiteralPath $path){$value=Get-ItemProperty -LiteralPath $path -ErrorAction SilentlyContinue;$framework+=@([pscustomobject]@{Path=$path;Version=[string]$value.Version;Release=$value.Release})}
        }
        $dotnet=@()
        foreach($root in @('HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost','HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedhost')){
            if(Test-Path -LiteralPath $root){$value=Get-ItemProperty -LiteralPath $root -ErrorAction SilentlyContinue;$dotnet+=@([pscustomobject]@{Path=$root;Version=[string]$value.Version})}
        }
        $auth=@()
        if($manager){
            foreach($site in $manager.Sites){
                $config=$site.GetWebConfiguration()
                $auth+=[pscustomobject]@{
                    Site=$site.Name
                    Windows=[bool]$config.GetSection('system.webServer/security/authentication/windowsAuthentication').Attributes['enabled'].Value
                    Anonymous=[bool]$config.GetSection('system.webServer/security/authentication/anonymousAuthentication').Attributes['enabled'].Value
                    Basic=[bool]$config.GetSection('system.webServer/security/authentication/basicAuthentication').Attributes['enabled'].Value
                }
            }
        }
        $paths=@($virtualDirectories|ForEach-Object PhysicalPath)+@($sites|ForEach-Object LogDirectory)|Where-Object{-not [string]::IsNullOrWhiteSpace($_)}|Sort-Object -Unique
        $pathFacts=@()
        foreach($path in $paths){
            $expanded=[Environment]::ExpandEnvironmentVariables([string]$path)
            $exists=Test-Path -LiteralPath $expanded
            $root=if([IO.Path]::IsPathRooted($expanded)){[IO.Path]::GetPathRoot($expanded)}else{$null}
            $drive=if($root){Get-PSDrive -Name $root.TrimEnd(':','\') -ErrorAction SilentlyContinue}else{$null}
            $pathFacts+=[pscustomobject]@{Path=[string]$path;Exists=$exists;Readable=$(if($exists){$null -ne (Get-Item -LiteralPath $expanded -ErrorAction SilentlyContinue)}else{$false});FreeBytes=$(if($drive){[long]$drive.Free}else{$null})}
        }
        [pscustomobject]@{
            OperatingSystem=[pscustomobject]@{Caption=$os.Caption;Version=$os.Version;BuildNumber=$os.BuildNumber;Architecture=$os.OSArchitecture;LastBootUpTime=$os.LastBootUpTime;DomainJoined=[bool]$computer.PartOfDomain;Domain=$computer.Domain;TimeZone=(Get-TimeZone).Id;Locale=(Get-Culture).Name;PendingReboot=$pending.Count -gt 0}
            Iis=[pscustomobject]@{RoleInstalled=[bool]($webFeature -and $webFeature.Installed);ServicePresent=$null -ne $iisService;ServiceStatus=$(if($iisService){[string]$iisService.Status}else{$null});Version=[string](Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\InetStp' -Name VersionString -ErrorAction SilentlyContinue).VersionString;ManagementApiAvailable=$assemblyAvailable}
            Sites=$sites;Applications=$applications;VirtualDirectories=$virtualDirectories;Bindings=$bindings;ApplicationPools=$pools;WorkerProcesses=$workers;FrameworkVersions=$framework;DotNetRuntimes=$dotnet;Authentication=$auth;Paths=$pathFacts
        }
    }
}

function New-IisValidationOperations {
    [CmdletBinding()]
    param()
    @{
        ResolveDns={param($name) @(Resolve-OperationalDns $name)}
        ResolveReverseDns={param($address) ([Net.Dns]::GetHostEntry($address)).HostName}
        TestTcp={param($name,$port) Test-OperationalTcpPort $name $port}
        TestWsMan={param($name,$port,$useTls) Test-WSMan -ComputerName $name -Port $port -UseSSL:$useTls -Authentication Kerberos -ErrorAction Stop}
        OpenSession={param($name,$port,$useTls)
            $option=New-PSSessionOption -IncludePortInSPN
            New-PSSession -ComputerName $name -Port $port -UseSSL:$useTls -Authentication Kerberos -SessionOption $option -ErrorAction Stop
        }
        GetSnapshot={param($session) Invoke-Command -Session $session -ScriptBlock (Get-IisRemoteSnapshotScript) -ErrorAction Stop}
        CloseSession={param($session) if($null -ne $session){Remove-PSSession -Session $session -ErrorAction SilentlyContinue}}
    }
}

function Invoke-IisTargetChecks {
    [CmdletBinding()]
    param([object]$Configuration,[string]$ComputerName,[int]$TargetIndex,[hashtable]$Operations)
    $prefix='T{0:D3}' -f $TargetIndex
    $results=New-Object Collections.Generic.List[object]
    $session=$null;$snapshot=$null;$remoteReady=$false
    try {
        try {
            $addresses=@(@(& $Operations.ResolveDns $ComputerName)|Sort-Object -Unique)
            $ok=$addresses.Count -gt 0
            $results.Add((New-IisCheck $prefix 'CONNECTIVITY.DNS.FORWARD' Connectivity 'Forward DNS' $ComputerName ($addresses -join ',') $(if($ok){'PASS'}else{'FAIL'}) $(if($ok){'INFO'}else{'HIGH'}) 'one or more addresses' $(if($ok){'DNS resolution succeeded.'}else{'DNS returned no address.'}) 'Correct approved DNS records.' $true))
            if($ok){
                try{$reverse=& $Operations.ResolveReverseDns $addresses[0];$results.Add((New-IisCheck $prefix 'CONNECTIVITY.DNS.REVERSE' Connectivity 'Reverse DNS' $ComputerName $reverse PASS INFO 'reverse name when available'))}
                catch{$results.Add((New-IisCheck $prefix 'CONNECTIVITY.DNS.REVERSE' Connectivity 'Reverse DNS' $ComputerName 'unavailable' WARNING LOW 'reverse name when available' 'Reverse DNS is unavailable.' 'Review PTR records when required.' $false))}
            }
        } catch {$results.Add((New-OperationalExceptionResult "$prefix.CONNECTIVITY.DNS.FORWARD" Connectivity 'Forward DNS' 'Resolve configured target.' $ComputerName $_.Exception HIGH $true))}
        try{$tcp=[bool](& $Operations.TestTcp $ComputerName ([int]$Configuration.Security.WinRMPort));$results.Add((New-IisCheck $prefix 'CONNECTIVITY.TCP.WINRM' Connectivity 'WinRM TCP port' $ComputerName $tcp $(if($tcp){'PASS'}else{'FAIL'}) $(if($tcp){'INFO'}else{'HIGH'}) $true 'Configured WinRM port reachability.' 'Correct approved network or listener configuration.' $true))}catch{$results.Add((New-OperationalExceptionResult "$prefix.CONNECTIVITY.TCP.WINRM" Connectivity 'WinRM TCP port' 'Test configured WinRM TCP endpoint.' $ComputerName $_.Exception HIGH $true))}
        try{[void](& $Operations.TestWsMan $ComputerName ([int]$Configuration.Security.WinRMPort) ([bool]$Configuration.Security.UseTLS));$results.Add((New-IisCheck $prefix 'CONNECTIVITY.WSMAN' Connectivity 'WSMan endpoint' $ComputerName 'reachable' PASS INFO 'Kerberos WSMan endpoint'))}catch{$results.Add((New-OperationalExceptionResult "$prefix.CONNECTIVITY.WSMAN" Connectivity 'WSMan endpoint' 'Validate Kerberos WSMan endpoint.' $ComputerName $_.Exception HIGH $true))}
        try{$session=& $Operations.OpenSession $ComputerName ([int]$Configuration.Security.WinRMPort) ([bool]$Configuration.Security.UseTLS);$remoteReady=$null -ne $session;$results.Add((New-IisCheck $prefix 'CONNECTIVITY.KERBEROS.SESSION' Connectivity 'Kerberos remote session' $ComputerName $remoteReady $(if($remoteReady){'PASS'}else{'FAIL'}) $(if($remoteReady){'INFO'}else{'CRITICAL'}) $true 'Kerberos-only session establishment.' 'Correct SPN, identity, delegation, time, DNS, or WinRM policy.' $true));if($remoteReady){$snapshot=& $Operations.GetSnapshot $session}}
        catch{$results.Add((New-OperationalExceptionResult "$prefix.CONNECTIVITY.KERBEROS.SESSION" Connectivity 'Kerberos remote session' 'Establish approved read-only remote session.' $ComputerName $_.Exception CRITICAL $true));$remoteReady=$false}

        if(-not $remoteReady -or $null -eq $snapshot){foreach($check in @(New-IisSkippedCategoryChecks $prefix $ComputerName 'A Kerberos-authenticated remote snapshot was not available.')){$results.Add($check)}}
        else {
            $os=$snapshot.OperatingSystem;$build=[int]$os.BuildNumber;$supported=([string]$os.Caption -match 'Windows Server') -and $build -ge 20348;$lab=([string]$os.Caption -match 'Windows Server') -and $build -ge 17763
            $results.Add((New-IisCheck $prefix 'OS.VERSION' OperatingSystem 'Supported Windows Server' $ComputerName "$($os.Caption) build $build" $(if($supported){'PASS'}elseif($lab){'WARNING'}else{'FAIL'}) $(if($supported){'INFO'}elseif($lab){'MEDIUM'}else{'HIGH'}) 'Windows Server 2022 or newer' 'Repository support policy applied.' 'Use a repository-supported Windows Server release.' $true))
            $x64=[string]$os.Architecture -match '64';$results.Add((New-IisCheck $prefix 'OS.ARCHITECTURE' OperatingSystem 'x64 architecture' $ComputerName $os.Architecture $(if($x64){'PASS'}else{'FAIL'}) $(if($x64){'INFO'}else{'HIGH'}) '64-bit' 'Architecture observed.' 'Use a supported x64 server.' $true))
            foreach($item in @(@('UPTIME','Uptime/last boot',$os.LastBootUpTime),@('DOMAIN','Domain membership',$os.DomainJoined),@('TIMEZONE','Time zone',$os.TimeZone),@('LOCALE','Locale',$os.Locale))){$results.Add((New-IisCheck $prefix "OS.$($item[0])" OperatingSystem $item[1] $ComputerName $item[2]))}
            $results.Add((New-IisCheck $prefix 'OS.PENDINGREBOOT' OperatingSystem 'Pending reboot' $ComputerName $os.PendingReboot $(if($os.PendingReboot){'WARNING'}else{'PASS'}) $(if($os.PendingReboot){'MEDIUM'}else{'INFO'}) $false 'Pending reboot signals observed.' 'Complete reboot through approved change control before collection if required.'))

            foreach($item in @(@('ROLE','IIS role installed',$snapshot.Iis.RoleInstalled),@('SERVICE.PRESENT','IIS service present',$snapshot.Iis.ServicePresent),@('SERVICE.RUNNING','IIS service running',([string]$snapshot.Iis.ServiceStatus -eq 'Running')),@('API','Microsoft.Web.Administration available',$snapshot.Iis.ManagementApiAvailable))){$pass=[bool]$item[2];$results.Add((New-IisCheck $prefix "INSTALL.$($item[0])" IISInstallation $item[1] $ComputerName $pass $(if($pass){'PASS'}else{'FAIL'}) $(if($pass){'INFO'}else{'HIGH'}) $true 'Required IIS prerequisite.' 'Install or enable through separately approved change control.' $true))}
            $results.Add((New-IisCheck $prefix 'INSTALL.VERSION' IISInstallation 'IIS version' $ComputerName $snapshot.Iis.Version $(if($snapshot.Iis.Version){'PASS'}else{'FAIL'}) $(if($snapshot.Iis.Version){'INFO'}else{'HIGH'}) 'observable IIS version' 'IIS version observed.' 'Repair IIS metadata through approved change control.' $true))

            foreach($item in @(@('SITES','Sites',$snapshot.Sites),@('APPLICATIONS','Applications',$snapshot.Applications),@('VIRTUALDIRECTORIES','Virtual directories',$snapshot.VirtualDirectories),@('BINDINGS','Bindings',$snapshot.Bindings))){$results.Add((New-IisCheck $prefix "CONFIG.$($item[0])" IISConfiguration $item[1] $ComputerName @($item[2]).Count PASS INFO 'read-only inventory' "$(@($item[2]).Count) item(s) observed." $null))}
            $number=0;foreach($site in @($snapshot.Sites|Sort-Object Name)){$number++;$results.Add((New-IisCheck $prefix ('CONFIG.SITE.{0:D3}' -f $number) IISConfiguration 'Site configuration' $ComputerName "Name=$($site.Name); State=$($site.State); Id=$($site.Id); Logging=$($site.LogEnabled); LogDirectory=$($site.LogDirectory)"))}
            $number=0;foreach($app in @($snapshot.Applications|Sort-Object Site,Path)){$number++;$results.Add((New-IisCheck $prefix ('CONFIG.APPLICATION.{0:D3}' -f $number) IISConfiguration 'Application configuration' $ComputerName "Site=$($app.Site); Path=$($app.Path); Pool=$($app.ApplicationPoolName)"))}
            $number=0;foreach($directory in @($snapshot.VirtualDirectories|Sort-Object Site,Application,Path)){$number++;$results.Add((New-IisCheck $prefix ('CONFIG.VDIR.{0:D3}' -f $number) IISConfiguration 'Virtual directory configuration' $ComputerName "Site=$($directory.Site); Application=$($directory.Application); Path=$($directory.Path); PhysicalPath=$($directory.PhysicalPath)"))}
            $number=0;foreach($binding in @($snapshot.Bindings|Sort-Object Site,Protocol,BindingInformation)){$number++;$results.Add((New-IisCheck $prefix ('CONFIG.BINDING.{0:D3}' -f $number) IISConfiguration 'Binding configuration' $ComputerName "Site=$($binding.Site); Protocol=$($binding.Protocol); Binding=$($binding.BindingInformation); HostHeader=$($binding.HostHeader); HTTPSCertificatePresent=$($null -ne $binding.CertificateHash)"))}
            $http=@($snapshot.Bindings|Where-Object Protocol -eq 'http').Count;$https=@($snapshot.Bindings|Where-Object Protocol -eq 'https').Count;$hosts=@($snapshot.Bindings|Where-Object{-not [string]::IsNullOrWhiteSpace($_.HostHeader)}).Count
            foreach($item in @(@('HTTP','HTTP bindings',$http),@('HTTPS','HTTPS bindings',$https),@('HOSTHEADERS','Host headers',$hosts))){$results.Add((New-IisCheck $prefix "CONFIG.$($item[0])" IISConfiguration $item[1] $ComputerName $item[2]))}

            $results.Add((New-IisCheck $prefix 'POOLS.INVENTORY' ApplicationPools 'Application pools' $ComputerName @($snapshot.ApplicationPools).Count PASS INFO 'read-only inventory' 'Pool name, runtime, pipeline, identity, state, auto-start, and recycling were collected.'))
            $number=0;foreach($pool in @($snapshot.ApplicationPools|Sort-Object Name)){$number++;$results.Add((New-IisCheck $prefix ('POOLS.ITEM.{0:D3}' -f $number) ApplicationPools 'Application pool configuration' $ComputerName "Name=$($pool.Name); Runtime=$($pool.RuntimeVersion); Pipeline=$($pool.PipelineMode); IdentityType=$($pool.IdentityType); Identity=$($pool.UserName); gMSA=$([string]$pool.UserName -match '\$$'); State=$($pool.State); AutoStart=$($pool.AutoStart); PeriodicRestartMinutes=$($pool.PeriodicRestartMinutes)"))}
            $gmsa=@($snapshot.ApplicationPools|Where-Object{$_.IdentityType -eq 'SpecificUser' -and [string]$_.UserName -match '\$$'}).Count;$results.Add((New-IisCheck $prefix 'POOLS.GMSA' ApplicationPools 'gMSA pool identities' $ComputerName $gmsa))
            $results.Add((New-IisCheck $prefix 'WORKERS.INVENTORY' WorkerProcesses 'Worker processes' $ComputerName @($snapshot.WorkerProcesses).Count PASS INFO 'read-only inventory' 'PID, application-pool mapping, and state were collected.'))
            $number=0;foreach($worker in @($snapshot.WorkerProcesses|Sort-Object ApplicationPoolName,ProcessId)){$number++;$results.Add((New-IisCheck $prefix ('WORKERS.ITEM.{0:D3}' -f $number) WorkerProcesses 'Worker process' $ComputerName "PID=$($worker.ProcessId); Pool=$($worker.ApplicationPoolName); State=$($worker.State)"))}

            $results.Add((New-IisCheck $prefix 'DOTNET.FRAMEWORK' .NETRuntime '.NET Framework versions' $ComputerName (@($snapshot.FrameworkVersions|ForEach-Object Version) -join ',') PASS INFO 'observe installed versions'))
            $results.Add((New-IisCheck $prefix 'DOTNET.RUNTIME' .NETRuntime '.NET runtimes' $ComputerName (@($snapshot.DotNetRuntimes|ForEach-Object Version) -join ',') PASS INFO 'observe installed versions' 'Installed runtime metadata was collected; application requirements not observable from IIS remain unknown.'))

            $loggingEnabled=@($snapshot.Sites|Where-Object LogEnabled).Count;$loggingDisabled=@($snapshot.Sites|Where-Object{-not $_.LogEnabled}).Count;$results.Add((New-IisCheck $prefix 'LOGGING.ENABLED' IISLogging 'IIS logging enabled' $ComputerName "$loggingEnabled enabled; $loggingDisabled disabled" $(if($loggingDisabled){'WARNING'}else{'PASS'}) $(if($loggingDisabled){'MEDIUM'}else{'INFO'}) 'logging enabled for applicable sites' 'Site logging configuration observed.' 'Enable required logging through approved change control.'))
            $logPaths=@($snapshot.Paths|Where-Object{$_.Path -match '(?i)log'});$results.Add((New-IisCheck $prefix 'LOGGING.PATHS' IISLogging 'Log directory accessibility' $ComputerName @($logPaths|Where-Object{$_.Exists -and $_.Readable}).Count $(if(@($logPaths|Where-Object{-not $_.Exists -or -not $_.Readable}).Count){'WARNING'}else{'PASS'}) MEDIUM 'configured log directories readable' 'Log directory facts observed.' 'Correct access through approved change control.'))

            $basic=@($snapshot.Authentication|Where-Object Basic).Count;$results.Add((New-IisCheck $prefix 'SECURITY.BASIC' Security 'Basic Authentication' $ComputerName $basic $(if($basic){'WARNING'}else{'PASS'}) $(if($basic){'HIGH'}else{'INFO'}) 0 'Basic Authentication settings observed.' 'Disable Basic Authentication through approved change control where not explicitly required.'))
            foreach($item in @(@('WINDOWS','Windows Authentication','Windows'),@('ANONYMOUS','Anonymous Authentication','Anonymous'))){$count=@($snapshot.Authentication|Where-Object{$_.$($item[2])}).Count;$results.Add((New-IisCheck $prefix "SECURITY.$($item[0])" Security $item[1] $ComputerName $count))}
            $number=0;foreach($auth in @($snapshot.Authentication|Sort-Object Site)){$number++;$results.Add((New-IisCheck $prefix ('SECURITY.SITE.{0:D3}' -f $number) Security 'Site authentication' $ComputerName "Site=$($auth.Site); Windows=$($auth.Windows); Anonymous=$($auth.Anonymous); Basic=$($auth.Basic)"))}
            $clientCert=@($snapshot.Bindings|Where-Object{$_.ClientCertificateMode -and [string]$_.ClientCertificateMode -ne 'None'}).Count;$results.Add((New-IisCheck $prefix 'SECURITY.CLIENTCERT' Security 'Client certificate settings' $ComputerName $clientCert))
            $results.Add((New-IisCheck $prefix 'SECURITY.POOLIDENTITIES' Security 'Application pool identities' $ComputerName @($snapshot.ApplicationPools).Count PASS INFO 'sanitized identity observations'))

            $missing=@($snapshot.Paths|Where-Object{-not $_.Exists}).Count;$unreadable=@($snapshot.Paths|Where-Object{$_.Exists -and -not $_.Readable}).Count;$results.Add((New-IisCheck $prefix 'FILES.PATHS' FileSystem 'Configured IIS paths' $ComputerName "$missing missing; $unreadable unreadable" $(if($missing -or $unreadable){'WARNING'}else{'PASS'}) $(if($missing -or $unreadable){'MEDIUM'}else{'INFO'}) 'paths exist and readable' 'Path metadata observed.' 'Correct paths or permissions through approved change control.'))
            $space=@($snapshot.Paths|Where-Object{$null -ne $_.FreeBytes}).Count;$results.Add((New-IisCheck $prefix 'FILES.FREESPACE' FileSystem 'Available disk space' $ComputerName "$space volume measurement(s)" PASS INFO 'observe; no invented threshold'))
            $number=0;foreach($path in @($snapshot.Paths|Sort-Object Path)){$number++;$results.Add((New-IisCheck $prefix ('FILES.ITEM.{0:D3}' -f $number) FileSystem 'IIS path observation' $ComputerName "Path=$($path.Path); Exists=$($path.Exists); Readable=$($path.Readable); FreeBytes=$($path.FreeBytes)"))}
        }
        try{$sqlDns=@(& $Operations.ResolveDns $Configuration.SqlServer.Server);$sqlTcp=[bool](& $Operations.TestTcp $Configuration.SqlServer.Server ([int]$Configuration.SqlServer.Port));$sqlOk=@($sqlDns).Count -gt 0 -and $sqlTcp;$results.Add((New-IisCheck $prefix 'SQL.ENDPOINT' SQLConnectivity 'Configured SQL endpoint' $ComputerName $sqlOk $(if($sqlOk){'PASS'}else{'FAIL'}) $(if($sqlOk){'INFO'}else{'HIGH'}) $true 'DNS/TCP only; no database connection.' 'Correct approved DNS/network configuration.' $true))}catch{$results.Add((New-OperationalExceptionResult "$prefix.SQL.ENDPOINT" SQLConnectivity 'Configured SQL endpoint' 'DNS/TCP-only SQL endpoint validation.' $ComputerName $_.Exception HIGH $true))}
        $prerequisites=@($results|Where-Object{$_.Mandatory -and $_.Category -ne 'CollectorCompatibility'});$compat=if($prerequisites|Where-Object Status -eq FAIL){'FAIL'}elseif($prerequisites|Where-Object Status -in @('WARNING','SKIPPED')){'WARNING'}else{'PASS'}
        $ids=($prerequisites|ForEach-Object CheckId)-join ',';$results.Add((New-IisCheck $prefix 'COMPATIBILITY.RESULT' CollectorCompatibility 'Collector compatibility' $ComputerName $compat $compat $(if($compat -eq 'FAIL'){'HIGH'}elseif($compat -eq 'WARNING'){'MEDIUM'}else{'INFO'}) 'all mandatory prerequisites pass' 'Compatibility is advisory and does not start the Collector.' 'Resolve reported prerequisites before authorized collection.' $true))
        $results.Add((New-IisCheck $prefix 'COMPATIBILITY.SOURCES' CollectorCompatibility 'Compatibility source checks' $ComputerName $ids PASS INFO 'constituent check identifiers'))
    } finally {if($null -ne $session){& $Operations.CloseSession $session}}
    $results.ToArray()
}
