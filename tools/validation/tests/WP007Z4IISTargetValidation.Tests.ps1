$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$commonRoot=Join-Path $repoRoot 'tools\validation\common'
$iisRoot=Join-Path $repoRoot 'tools\validation\iis'
. (Join-Path $commonRoot 'OperationalValidation.Common.ps1')
. (Join-Path $commonRoot 'OperationalValidation.Reporting.ps1')
. (Join-Path $iisRoot 'IISTarget.Checks.ps1')

function New-TestIisSnapshot {
    [pscustomobject]@{
        OperatingSystem=[pscustomobject]@{Caption='Microsoft Windows Server 2022 Datacenter';Version='10.0';BuildNumber='20348';Architecture='64-bit';LastBootUpTime=[datetime]'2026-07-30';DomainJoined=$true;Domain='EXAMPLE';TimeZone='Turkey Standard Time';Locale='en-US';PendingReboot=$false}
        Iis=[pscustomobject]@{RoleInstalled=$true;ServicePresent=$true;ServiceStatus='Running';Version='Version 10.0';ManagementApiAvailable=$true}
        Sites=@([pscustomobject]@{Name='Default Web Site';State='Started';Id=1;LogEnabled=$true;LogDirectory='D:\IISLogs'})
        Applications=@([pscustomobject]@{Site='Default Web Site';Path='/';ApplicationPoolName='DefaultAppPool'})
        VirtualDirectories=@([pscustomobject]@{Site='Default Web Site';Application='/';Path='/';PhysicalPath='D:\Sites\Default'})
        Bindings=@([pscustomobject]@{Site='Default Web Site';Protocol='https';BindingInformation='*:443:site.example.invalid';HostHeader='site.example.invalid';CertificateHash='synthetic';ClientCertificateMode='None'})
        ApplicationPools=@([pscustomobject]@{Name='DefaultAppPool';RuntimeVersion='v4.0';PipelineMode='Integrated';IdentityType='SpecificUser';UserName='EXAMPLE\gmsa-web$';State='Started';AutoStart=$true;PeriodicRestartMinutes=1740})
        WorkerProcesses=@([pscustomobject]@{ProcessId=1234;ApplicationPoolName='DefaultAppPool';State='Running'})
        FrameworkVersions=@([pscustomobject]@{Path='registry';Version='4.8';Release=528040})
        DotNetRuntimes=@([pscustomobject]@{Path='registry';Version='10.0.0'})
        Authentication=@([pscustomobject]@{Site='Default Web Site';Windows=$true;Anonymous=$false;Basic=$false})
        Paths=@([pscustomobject]@{Path='D:\Sites\Default';Exists=$true;Readable=$true;FreeBytes=100GB},[pscustomobject]@{Path='D:\IISLogs';Exists=$true;Readable=$true;FreeBytes=100GB})
    }
}

function New-TestIisOperations {
    param([bool]$SessionSucceeds=$true,[bool]$BasicEnabled=$false)
    $snapshot=New-TestIisSnapshot
    if($BasicEnabled){$snapshot.Authentication[0].Basic=$true}
    @{
        ResolveDns={param($name) @('192.0.2.10')}
        ResolveReverseDns={param($address) 'iis01.example.invalid'}
        TestTcp={param($name,$port) $true}
        TestWsMan={param($name,$port,$useTls) 'WSMan'}
        OpenSession=({param($name,$port,$useTls) if($SessionSucceeds){[pscustomobject]@{ComputerName=$name}}else{throw 'Kerberos session unavailable'}}).GetNewClosure()
        GetSnapshot=({param($session) $snapshot}).GetNewClosure()
        CloseSession={param($session) $script:ClosedSession=$true}
    }
}

Describe 'WP-007.Z.4 IIS target validation' {
    BeforeEach {
        $script:ClosedSession=$false
        $configuration=Get-Content -Raw (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json')|ConvertFrom-Json
    }

    It 'loads IIS targets only from the valid shared configuration' {
        $loaded=Get-OperationalConfiguration (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json') $repoRoot
        @($loaded.IisTargets).Count|Should Be 2
        $loaded.IisTargets[0]|Should Be 'iis01.example.invalid'
    }

    It 'implements every required category with a passing mocked target' {
        $results=@(Invoke-IisTargetChecks $configuration 'iis01.example.invalid' 1 (New-TestIisOperations))
        {Assert-OperationalResults $results}|Should Not Throw
        foreach($category in @('Connectivity','OperatingSystem','IISInstallation','IISConfiguration',
            'ApplicationPools','WorkerProcesses','.NETRuntime','IISLogging','Security','FileSystem',
            'SQLConnectivity','CollectorCompatibility')){
            @($results|Where-Object Category -eq $category).Count -gt 0|Should Be $true
        }
        ($results|Where-Object CheckId -eq 'T001.COMPATIBILITY.RESULT').Status|Should Be PASS
        $script:ClosedSession|Should Be $true
    }

    It 'warns when Basic Authentication is enabled without changing it' {
        $results=@(Invoke-IisTargetChecks $configuration 'iis01.example.invalid' 1 (New-TestIisOperations -BasicEnabled $true))
        ($results|Where-Object CheckId -eq 'T001.SECURITY.BASIC').Status|Should Be WARNING
    }

    It 'isolates Kerberos failure and skips dependent remote checks' {
        $results=@(Invoke-IisTargetChecks $configuration 'iis01.example.invalid' 1 (New-TestIisOperations -SessionSucceeds $false))
        ($results|Where-Object CheckId -eq 'T001.CONNECTIVITY.KERBEROS.SESSION').Status|Should Be FAIL
        @($results|Where-Object Status -eq SKIPPED).Count|Should Be 9
        ($results|Where-Object CheckId -eq 'T001.COMPATIBILITY.RESULT').Status|Should Be FAIL
    }

    It 'uses stable target-prefixed identifiers' {
        $one=@(Invoke-IisTargetChecks $configuration 'iis01.example.invalid' 1 (New-TestIisOperations))
        $two=@(Invoke-IisTargetChecks $configuration 'iis02.example.invalid' 2 (New-TestIisOperations))
        @($one|Where-Object CheckId -notmatch '^T001\.').Count|Should Be 0
        @($two|Where-Object CheckId -notmatch '^T002\.').Count|Should Be 0
        {Assert-OperationalResults @($one+$two)}|Should Not Throw
    }

    It 'writes IIS-specific JSON Markdown and log files through the common reporter' {
        $results=@(Invoke-IisTargetChecks $configuration 'iis01.example.invalid' 1 (New-TestIisOperations))
        $report=New-OperationalReport $configuration ('A'*64) source 'iis01.example.invalid' ([datetime]'2026-07-31') $results 'IIS Target Validation Report' 'IISTargetValidation'
        $paths=Write-OperationalReports $report $TestDrive
        Split-Path $paths.Json -Leaf|Should Be 'IISTargetValidationReport.json'
        Split-Path $paths.Markdown -Leaf|Should Be 'IISTargetValidationReport.md'
        Split-Path $paths.Log -Leaf|Should Be 'IISTargetValidation.log'
        Get-Content -Raw $paths.Markdown|Should Match '# IIS Target Validation Report'
    }

    It 'uses the exact command parameter and console contract' {
        $text=Get-Content -Raw (Join-Path $iisRoot 'Invoke-IISTargetValidation.ps1')
        foreach($term in @('ConfigurationPath','ComputerName','OutputPath','IIS TARGET VALIDATION:','exit 3','exit 4')){$text|Should Match ([regex]::Escape($term))}
    }

    It 'uses Kerberos with the configured port and a port-qualified SPN' {
        $text=Get-Content -Raw (Join-Path $iisRoot 'IISTarget.Checks.ps1')
        $text|Should Match 'Authentication Kerberos'
        $text|Should Match 'IncludePortInSPN'
        $text|Should Match 'Security.WinRMPort'
        $text|Should Not Match '(?i)-Authentication\s+(Basic|CredSSP|Default)'
        $text|Should Not Match '(?i)SkipCACheck|SkipCNCheck|SkipRevocationCheck|TrustedHosts'
    }

    It 'contains no prohibited IIS or environment mutation commands' {
        $prohibited=@('Start-Service','Stop-Service','Restart-Service','Set-Service','Enable-PSRemoting',
            'Disable-PSRemoting','Set-ItemProperty','Remove-Item','Restart-Computer','Set-ExecutionPolicy',
            'Restart-WebAppPool','Start-WebAppPool','Stop-WebAppPool','Start-Website','Stop-Website',
            'Stop-Process','Set-WebConfiguration','Add-WebConfiguration','Remove-WebConfiguration')
        $found=@()
        foreach($file in Get-ChildItem $iisRoot -Filter *.ps1){
            $tokens=$null;$errors=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
            $errors.Count|Should Be 0
            $found+=@($ast.FindAll({param($node)$node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -in $prohibited},$true))
        }
        $found.Count|Should Be 0
        (Get-Content -Raw (Join-Path $iisRoot 'IISTarget.Checks.ps1'))|Should Not Match 'CommitChanges'
    }

    It 'does not contain secret-bearing configuration or output fields' {
        $text=(Get-Content -Raw (Join-Path $iisRoot 'IISTarget.Checks.ps1'))+(Get-Content -Raw (Join-Path $iisRoot 'Invoke-IISTargetValidation.ps1'))
        $text|Should Not Match '(?i)\b(password|connectionstring|privatekey)\b'
    }
}
