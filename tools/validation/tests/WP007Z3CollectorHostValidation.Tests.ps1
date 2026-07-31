$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$commonRoot=Join-Path $repoRoot 'tools\validation\common'
$collectorRoot=Join-Path $repoRoot 'tools\validation\collector'
. (Join-Path $commonRoot 'OperationalValidation.Common.ps1')
. (Join-Path $commonRoot 'OperationalValidation.Reporting.ps1')
. (Join-Path $collectorRoot 'CollectorHost.Checks.ps1')

function New-TestResult([string]$id,[string]$status,[string]$severity='INFO',[bool]$mandatory=$false) {
    $time=[datetime]'2026-07-31T00:00:00Z'
    New-OperationalValidationResult -CheckId $id -Category Test -Name $id `
        -Description test -Target test -Status $status -Severity $severity `
        -StartedAt $time -CompletedAt $time -Expected expected -Actual actual `
        -Message test -Recommendation $null -Evidence evidence -Mandatory $mandatory
}

Describe 'WP-007.Z.3 operational validation contract' {
    It 'executes every category with mocked environment operations' {
        $configuration=Get-Content -Raw (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json')|ConvertFrom-Json
        $ops=@{
            GetComputerSystem={ [pscustomobject]@{Name='collector';Domain='example.invalid';PartOfDomain=$true;TotalPhysicalMemory=16GB;NumberOfLogicalProcessors=8} }
            GetOperatingSystem={ [pscustomobject]@{Caption='Microsoft Windows Server 2022';Version='10.0';BuildNumber=20348;OSArchitecture='64-bit';LastBootUpTime='20260730000000.000000+000';InstallationType='Server';FreePhysicalMemory=4194304} }
            GetLocale={'en-US'};GetTimeZone={[pscustomobject]@{Id='Turkey Standard Time'}}
            GetPendingReboot={$false};GetDisk={param($p)[pscustomobject]@{FreeGigabytes=100}}
            GetWindowsPowerShell={$true};HasCommand={param($n)$true}
            GetDotNetRuntimes={'Microsoft.NETCore.App 10.0.0 [C:\dotnet]'}
            GetService={param($n)[pscustomobject]@{Status='Running'}}
            GetDomain={'example.invalid'};TestSecureChannel={$true};DiscoverDomainController={'dc.example.invalid'}
            GetKerberosTicket={$true};GetTimeSkew={'synchronized'};TestGmsaLocal={$true};TestGmsa={$true}
            GetWinRmListener={[pscustomobject]@{Port=5985;Address='*'}}
            GetWinRmKerberos={$true};GetWinRmBasic={$false};GetWinRmCredSsp={$false}
            GetWinRmAllowUnencrypted={$false};GetTrustedHosts={''};TestWsMan={'Stack: 3.0'}
            ResolveDns={param($n)@('192.0.2.10')};ReverseDns={'host.example.invalid'}
            GetRouteInterface={'interface diagnostics'};TestTcp={$true}
            GetSqlMetadata={param($c)[pscustomobject]@{
                LoginName='EXAMPLE\gmsa-collector$';ProductVersion='16.0';Edition='Developer'
                DatabaseName=$c.SqlServer.Database;CompatibilityLevel=$c.SqlServer.CompatibilityLevel
                Collation=$c.SqlServer.Collation;RecoveryModel=$c.SqlServer.RecoveryModel;Encryption='TRUE'
            }}
            PathExists={$true};CanReadPath={$true};GetEventLog={1}
        }
        $all=New-Object Collections.Generic.List[object]
        foreach($group in @(
            {Invoke-CollectorOperatingSystemChecks $configuration collector $ops},
            {Invoke-CollectorHardwareChecks $configuration collector $ops},
            {Invoke-CollectorPowerShellChecks collector $ops},
            {Invoke-CollectorDotNetChecks collector $ops},
            {Invoke-CollectorServiceChecks collector $ops},
            {Invoke-CollectorIdentityChecks $configuration collector $ops},
            {Invoke-CollectorWinRmChecks $configuration collector $true $ops},
            {Invoke-CollectorNetworkChecks $configuration collector $true $ops},
            {Invoke-CollectorSqlChecks $configuration collector $ops},
            {Invoke-CollectorFileSystemChecks $configuration $TestDrive $ops},
            {Invoke-CollectorLoggingChecks $configuration $TestDrive collector $ops},
            {Invoke-CollectorSecurityChecks $configuration (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json')},
            {Invoke-CollectorReleaseArtifactChecks $configuration $repoRoot (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json')}
        )) {
            foreach($result in @(& $group)){$all.Add($result)}
        }
        $all.Count -gt 60|Should Be $true
        {Assert-OperationalResults $all.ToArray()}|Should Not Throw
        @($all|Select-Object -ExpandProperty Category -Unique).Count|Should Be 13
    }

    It 'loads the valid shared deployment configuration' {
        $configuration=Get-OperationalConfiguration `
            (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json') $repoRoot
        $configuration.SqlServer.Port|Should Be 1433
    }

    It 'rejects invalid configuration' {
        { Get-OperationalConfiguration `
            (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.template.json') $repoRoot } |
            Should Throw
    }

    It 'returns exit code 3 before environment checks for invalid configuration' {
        $entry=Join-Path $collectorRoot 'Invoke-CollectorHostValidation.ps1'
        $template=Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.template.json'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $entry `
            -ConfigurationPath $template -OutputPath $TestDrive 2>$null
        $LASTEXITCODE|Should Be 3
    }

    It 'maps deterministic exit codes' {
        Get-OperationalExitCode PASS|Should Be 0
        Get-OperationalExitCode WARNING|Should Be 1
        Get-OperationalExitCode FAIL|Should Be 2
        Get-OperationalExitCode INVALID_CONFIGURATION|Should Be 3
        Get-OperationalExitCode EXECUTION_ERROR|Should Be 4
    }

    It 'calculates PASS WARNING and FAIL deterministically' {
        Get-OperationalOverallStatus @(New-TestResult TEST.PASS PASS)|Should Be PASS
        Get-OperationalOverallStatus @((New-TestResult TEST.PASS PASS),(New-TestResult TEST.WARNING WARNING LOW))|Should Be WARNING
        Get-OperationalOverallStatus @((New-TestResult TEST.WARNING WARNING),(New-TestResult TEST.FAIL FAIL HIGH))|Should Be FAIL
    }

    It 'does not fail skipped and not-applicable optional checks' {
        Get-OperationalOverallStatus @(
            (New-TestResult TEST.SKIPPED SKIPPED),
            (New-TestResult TEST.NA NOT_APPLICABLE))|Should Be PASS
    }

    It 'warns for a mandatory skipped check' {
        Get-OperationalOverallStatus @(New-TestResult TEST.SKIPPED SKIPPED LOW $true)|Should Be WARNING
    }

    It 'sanitizes exception and connection text' {
        Protect-OperationalText 'Password=private-marker'|Should Be '[REDACTED]'
        Protect-OperationalText 'Server=x;Database=y;Integrated Security=true'|Should Be '[REDACTED]'
        $safe=New-OperationalExceptionResult TEST.ERROR Test Error Description Target `
            ([InvalidOperationException]::new('Password=private-marker'))
        $safe.ExceptionMessage|Should Be '[REDACTED]'
    }

    It 'validates ports and local path formats' {
        Test-OperationalPort 1|Should Be $true
        Test-OperationalPort 65535|Should Be $true
        Test-OperationalPort 0|Should Be $false
        Test-OperationalPort 65536|Should Be $false
        Test-OperationalPathFormat 'C:\PSM\Logs'|Should Be $true
        Test-OperationalPathFormat '\\server\share'|Should Be $false
    }

    It 'rejects duplicate check identifiers' {
        { Get-OperationalOverallStatus @(
            (New-TestResult TEST.DUPLICATE PASS),
            (New-TestResult TEST.DUPLICATE PASS)) }|Should Throw
    }

    It 'rejects malformed result objects' {
        { Get-OperationalOverallStatus @([pscustomobject]@{CheckId='TEST.BAD'}) }|Should Throw
    }

    It 'generates deterministic ordered reports' {
        $configuration=Get-Content -Raw (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json')|ConvertFrom-Json
        $report=New-OperationalReport $configuration ('A'*64) source target `
            ([datetime]'2026-07-31T00:00:00Z') @(
                (New-TestResult TEST.Z PASS),(New-TestResult TEST.A WARNING LOW))
        $report.Results[0].CheckId|Should Be TEST.A
        $markdown=ConvertTo-OperationalMarkdown $report
        $markdown|Should Match 'Overall result: WARNING'
        $markdown.IndexOf('TEST.A') -lt $markdown.IndexOf('TEST.Z')|Should Be $true
    }

    It 'writes all fixed report files' {
        $configuration=Get-Content -Raw (Join-Path $repoRoot 'Release\Deployment\DeploymentConfiguration.sample.json')|ConvertFrom-Json
        $report=New-OperationalReport $configuration ('A'*64) source target `
            ([datetime]'2026-07-31T00:00:00Z') @(New-TestResult TEST.PASS PASS)
        $paths=Write-OperationalReports $report $TestDrive
        Test-Path $paths.Json|Should Be $true
        Test-Path $paths.Markdown|Should Be $true
        Test-Path $paths.Log|Should Be $true
        (Get-Content -Raw $paths.Json)|Should Match '"OverallResult":\s+"PASS"'
    }

    It 'contains all required result properties' {
        $result=New-TestResult TEST.CONTRACT PASS
        foreach($name in @('CheckId','Category','Name','Description','Target','Status','Severity',
            'StartedAt','CompletedAt','DurationMilliseconds','Expected','Actual','Message',
            'Recommendation','Evidence','ExceptionType','ExceptionMessage')){
            $null -ne $result.PSObject.Properties[$name]|Should Be $true
        }
    }

    It 'uses only Kerberos when WSMan is called' {
        $text=Get-Content -Raw (Join-Path $collectorRoot 'CollectorHost.Checks.ps1')
        $text|Should Match "Authentication='Kerberos'"
        $text|Should Not Match "(?i)Authentication\\s*=\\s*'(Negotiate|Basic|CredSSP|Default)'"
        $text|Should Not Match '(?i)SkipCACheck|SkipCNCheck|SkipRevocationCheck'
    }

    It 'contains no prohibited environment mutation commands' {
        $prohibited=@('Start-Service','Stop-Service','Restart-Service','Set-Service',
            'Enable-PSRemoting','Disable-PSRemoting','Install-ADServiceAccount',
            'Set-ADServiceAccount','New-NetFirewallRule','Set-ItemProperty',
            'Remove-Item','Restart-Computer','Set-ExecutionPolicy')
        $found=@()
        foreach($file in Get-ChildItem $commonRoot,$collectorRoot -Filter *.ps1){
            $tokens=$null;$errors=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
            $errors.Count|Should Be 0
            $found+=@($ast.FindAll({param($node)
                $node -is [Management.Automation.Language.CommandAst] -and
                $node.GetCommandName() -in $prohibited
            },$true))
        }
        $found.Count|Should Be 0
    }

    It 'defines all thirteen validation categories' {
        $text=Get-Content -Raw (Join-Path $collectorRoot 'CollectorHost.Checks.ps1')
        foreach($category in @('OperatingSystem','HardwareCapacity','PowerShell','.NET',
            'WindowsServices','ActiveDirectoryKerberos','WinRM','NetworkDNS',
            'SQLConnectivity','FileSystem','LoggingDiagnostics','SecurityConfiguration',
            'ReleaseArtifacts')){
            $text|Should Match ([regex]::Escape($category))
        }
    }

    It 'defines fixed console results and parameter contract' {
        $entry=Get-Content -Raw (Join-Path $collectorRoot 'Invoke-CollectorHostValidation.ps1')
        foreach($term in @('ConfigurationPath','OutputPath','ComputerName','SkipRemoteChecks',
            'COLLECTOR HOST VALIDATION:','exit 3','exit 4')){
            $entry|Should Match ([regex]::Escape($term))
        }
    }
}
