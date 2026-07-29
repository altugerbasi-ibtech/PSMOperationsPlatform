$scriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Initialize-PSMSmokeTestDatabase.ps1'
. $scriptPath

function New-TestState {
    @{
        DatabaseExists = $true
        DatabaseState = 'ONLINE'
        LoginExists = $true
        LoginType = 'WINDOWS_LOGIN'
        LoginDisabled = $false
        UserExists = $true
        UserMappingCorrect = $true
        UserAuthenticationType = 'WINDOWS'
        Reader = $true
        Writer = $true
        DatabaseHigherRoles = @()
        ServerHigherRoles = @()
        Mutations = New-Object System.Collections.Generic.List[string]
        Queries = New-Object System.Collections.Generic.List[string]
        FailMutation = $null
        Reports = New-Object System.Collections.Generic.List[object]
    }
}

function New-TestOperations {
    param([hashtable]$State)
    @{
        ClientAvailable = { $true }
        Query = {
            param($Server,$Database,$Query)
            $State.Queries.Add($Query)
            if ($Query -match 'PSM:CONNECTIVITY') { return [pscustomobject]@{Connected=1;ProductMajorVersion=16} }
            if ($Query -match 'PSM:MUTATE:CREATE_DATABASE') {
                $State.Mutations.Add('CreateDatabase')
                if ($State.FailMutation -eq 'CreateDatabase') { throw 'sensitive sql error' }
                $State.DatabaseExists = $true; $State.DatabaseState = 'ONLINE'; return
            }
            if ($Query -match 'PSM:MUTATE:CREATE_LOGIN') {
                $State.Mutations.Add('CreateLogin')
                if ($State.FailMutation -eq 'CreateLogin') { throw 'sensitive sql error' }
                $State.LoginExists = $true; $State.LoginType = 'WINDOWS_LOGIN'; return
            }
            if ($Query -match 'PSM:MUTATE:CREATE_USER') {
                $State.Mutations.Add('CreateUser')
                if ($State.FailMutation -eq 'CreateUser') { throw 'sensitive sql error' }
                $State.UserExists = $true; $State.UserMappingCorrect = $true
                $State.UserAuthenticationType = 'WINDOWS'; return
            }
            if ($Query -match 'PSM:MUTATE:ADD_ROLE:db_datareader') {
                $State.Mutations.Add('AddReader')
                if ($State.FailMutation -eq 'AddReader') { throw 'sensitive sql error' }
                $State.Reader = $true; return
            }
            if ($Query -match 'PSM:MUTATE:ADD_ROLE:db_datawriter') {
                $State.Mutations.Add('AddWriter')
                if ($State.FailMutation -eq 'AddWriter') { throw 'sensitive sql error' }
                $State.Writer = $true; return
            }
            if ($Query -match 'PSM:STATE:DATABASE') {
                return [pscustomobject]@{Exists=[int]$State.DatabaseExists;StateDescription=$(if($State.DatabaseExists){$State.DatabaseState}else{''})}
            }
            if ($Query -match 'PSM:STATE:LOGIN') {
                return [pscustomobject]@{Exists=[int]$State.LoginExists;TypeDescription=$(if($State.LoginExists){$State.LoginType}else{''});IsDisabled=[int]$State.LoginDisabled}
            }
            if ($Query -match 'PSM:STATE:USER') {
                return [pscustomobject]@{Exists=[int]$State.UserExists;TypeDescription='WINDOWS_USER';AuthenticationTypeDescription=$State.UserAuthenticationType;MappingCorrect=[int]$State.UserMappingCorrect}
            }
            if ($Query -match 'PSM:STATE:ROLE') {
                $membership = if ($Query -match "N'db_datareader'") {$State.Reader} else {$State.Writer}
                return [pscustomobject]@{IsMember=[int]$membership}
            }
            if ($Query -match 'PSM:STATE:HIGHER_DATABASE_ROLES') {
                return @($State.DatabaseHigherRoles | ForEach-Object {[pscustomobject]@{RoleName=$_}})
            }
            if ($Query -match 'PSM:STATE:HIGHER_SERVER_ROLES') {
                return @($State.ServerHigherRoles | ForEach-Object {[pscustomobject]@{RoleName=$_}})
            }
            throw 'unexpected query'
        }
        WriteReport = { param($Path,$Content) $State.Reports.Add([pscustomobject]@{Path=$Path;Content=$Content}) }
        MachineName = { 'TESTHOST' }
        Identity = { 'TEST\operator' }
        Now = { [datetimeoffset]'2026-07-27T12:00:00+03:00' }
    }
}

function Invoke-TestInitialization {
    param(
        [hashtable]$State,
        [scriptblock]$ShouldProcess = { param($target,$action) $true },
        [bool]$WhatIfMode = $false,
        [string]$Server = 'sql01.example.test',
        [string]$Database = 'PSM_Smoke',
        [string]$ServiceAccount = 'EXAMPLE\gmsaCollector$',
        [string]$ReportPath
    )
    Invoke-PSMSmokeTestDatabaseInitialization $Server $Database $ServiceAccount `
        $ReportPath (New-TestOperations $State) $ShouldProcess $WhatIfMode
}

Describe 'Parameter model and validation' {
    It 'declares the approved defaults only in the parameter block' {
        $text = Get-Content -Raw $scriptPath
        ([regex]::Matches($text, [regex]::Escape('mydb01.ae.local'))).Count | Should Be 1
        ([regex]::Matches($text, [regex]::Escape('PSMOperationsPlatform_SmokeTest'))).Count | Should Be 1
        ([regex]::Matches($text, [regex]::Escape('AE\gmsaSPWorker$'))).Count | Should Be 1
    }
    It 'accepts valid custom values and a gMSA' {
        { Test-DeploymentInputs 'sql02.example.test' 'PSM_Test-02' 'EXAMPLE\gmsaSql$' $null } | Should Not Throw
    }
    It 'rejects empty or invalid servers before client access' {
        { Test-DeploymentInputs ' ' 'PSM_Test' 'EXAMPLE\svc$' $null } | Should Throw
        foreach ($value in @('tcp:sql01','sql01,1433','sql01\instance','Server=sql01','sql01/path','SELECT')) {
            { Test-DeploymentInputs $value 'PSM_Test' 'EXAMPLE\svc$' $null } | Should Throw
        }
    }
    It 'rejects empty and unsafe database names' {
        foreach ($value in @(' ','unsafe;DROP DATABASE x','bad]name','select database','a/b')) {
            { Test-DeploymentInputs 'sql01' $value 'EXAMPLE\svc$' $null } | Should Throw
        }
        { Test-DeploymentInputs 'sql01' ('a' * 129) 'EXAMPLE\svc$' $null } | Should Throw
    }
    It 'rejects invalid service accounts' {
        foreach ($value in @('svc','DOMAIN\','DOMAIN\name;DROP','DOMAIN/name','DOMAIN\name''x')) {
            { Test-DeploymentInputs 'sql01' 'PSM_Test' $value $null } | Should Throw
        }
    }
    It 'does not touch client or SQL when validation fails' {
        $calls = 0
        $ops = New-TestOperations (New-TestState)
        $ops.ClientAvailable = { $script:calls++ ; $true }
        $ops.Query = { throw 'must not execute' }
        $result = Invoke-PSMSmokeTestDatabaseInitialization 'tcp:bad' 'PSM_Test' 'EXAMPLE\svc$' $null $ops { $true } $false
        $result.Overall | Should Be FAILED
        $calls | Should Be 0
        $result.ExitCode | Should Be 2
    }
    It 'fails safely when the approved SQL client is unavailable' {
        $state = New-TestState
        $ops = New-TestOperations $state
        $ops.ClientAvailable = { $false }
        $result = Invoke-PSMSmokeTestDatabaseInitialization 'sql01' 'PSM_Test' 'EXAMPLE\svc$' $null $ops { $true } $false
        $result.Overall | Should Be FAILED
        $state.Queries.Count | Should Be 0
    }
}

Describe 'Ready state and idempotency' {
    It 'reports an already-ready state without mutation' {
        $state = New-TestState
        $result = Invoke-TestInitialization $state
        $result.Overall | Should Be READY
        $result.ExitCode | Should Be 0
        $state.Mutations.Count | Should Be 0
        @($result.Results | Where-Object Name -in @('Database','Login','User','db_datareader','db_datawriter') | Where-Object Status -ne PASS).Count | Should Be 0
    }
    It 'performs no mutation on a second run' {
        $state = New-TestState
        $state.DatabaseExists=$false; $state.LoginExists=$false; $state.UserExists=$false
        $state.Reader=$false; $state.Writer=$false
        (Invoke-TestInitialization $state).Overall | Should Be READY
        $state.Mutations.Count | Should Be 5
        $state.Mutations.Clear()
        (Invoke-TestInitialization $state).Overall | Should Be READY
        $state.Mutations.Count | Should Be 0
    }
}

Describe 'Creation, ShouldProcess, and WhatIf' {
    It 'creates exactly the five missing objects or memberships in order' {
        $state = New-TestState
        $state.DatabaseExists=$false; $state.LoginExists=$false; $state.UserExists=$false
        $state.Reader=$false; $state.Writer=$false
        $result = Invoke-TestInitialization $state
        $state.Mutations.ToArray() | Should Be @('CreateDatabase','CreateLogin','CreateUser','AddReader','AddWriter')
        @($result.Results | Where-Object Changed).Count | Should Be 5
    }
    It 'independently guards every mutation' {
        $state = New-TestState
        $state.DatabaseExists=$false; $state.LoginExists=$false; $state.UserExists=$false
        $state.Reader=$false; $state.Writer=$false
        $actions = New-Object System.Collections.Generic.List[string]
        $null = Invoke-TestInitialization $state { param($target,$action) $actions.Add($action); $true }
        $actions.ToArray() | Should Be @(
            'Create smoke-test database','Create Windows login','Create mapped database user',
            'Add db_datareader membership','Add db_datawriter membership'
        )
    }
    It 'executes no mutation SQL and returns WHATIF when changes are declined' {
        $state = New-TestState
        $state.DatabaseExists=$false; $state.LoginExists=$false
        $state.UserExists=$false; $state.Reader=$false; $state.Writer=$false
        $result = Invoke-TestInitialization $state { $false } $true
        $state.Mutations.Count | Should Be 0
        @($state.Queries | Where-Object { $_ -match 'PSM:MUTATE' }).Count | Should Be 0
        $result.Overall | Should Be WHATIF
        $result.ExitCode | Should Be 3
    }
    It 'keeps an already-ready WhatIf run READY' {
        $state = New-TestState
        $result = Invoke-TestInitialization $state { $false } $true
        $result.Overall | Should Be READY
        $result.ExitCode | Should Be 0
    }
    It 'reports an independently declined role as planned' {
        $state = New-TestState
        $state.Writer = $false
        $result = Invoke-TestInitialization $state { param($target,$action) $action -ne 'Add db_datawriter membership' }
        ($result.Results | Where-Object Name -eq db_datawriter).Status | Should Be PLANNED
        $result.ExitCode | Should Be 3
    }
}

Describe 'Principal verification and higher privileges' {
    It 'fails a conflicting login type without changing it' {
        $state = New-TestState
        $state.LoginType = 'SQL_LOGIN'
        $result = Invoke-TestInitialization $state
        ($result.Results | Where-Object Name -eq Login).Status | Should Be FAIL
        $state.Mutations.Count | Should Be 0
    }
    It 'fails an orphan or incorrectly mapped user without remapping it' {
        $state = New-TestState
        $state.UserMappingCorrect = $false
        $result = Invoke-TestInitialization $state
        ($result.Results | Where-Object Name -eq User).Status | Should Be FAIL
        @($state.Mutations | Where-Object { $_ -eq 'CreateUser' }).Count | Should Be 0
    }
    It 'warns for existing db_owner and sysadmin without removing them' {
        $state = New-TestState
        $state.DatabaseHigherRoles = @('db_owner')
        $state.ServerHigherRoles = @('sysadmin')
        $result = Invoke-TestInitialization $state
        $result.Overall | Should Be WARNING
        $result.ExitCode | Should Be 1
        ($result.Results | Where-Object Name -eq HigherPrivileges).Summary | Should Match 'db_owner'
        ($result.Results | Where-Object Name -eq HigherPrivileges).Summary | Should Match 'sysadmin'
        $state.Mutations.Count | Should Be 0
    }
}

Describe 'Partial failure, reports, and security' {
    It 'preserves completed database creation when login creation fails' {
        $state = New-TestState
        $state.DatabaseExists=$false; $state.LoginExists=$false; $state.UserExists=$false
        $state.Reader=$false; $state.Writer=$false; $state.FailMutation='CreateLogin'
        $first = Invoke-TestInitialization $state
        $first.Overall | Should Be FAILED
        ($first.Results | Where-Object Name -eq Database).Status | Should Be PASS
        $state.DatabaseExists | Should Be $true
        @($state.Queries | Where-Object { $_ -match '\bDROP\b' }).Count | Should Be 0
        $state.FailMutation=$null
        (Invoke-TestInitialization $state).Overall | Should Be READY
    }
    It 'generates Markdown only when requested' {
        $state = New-TestState
        $null = Invoke-TestInitialization $state
        $state.Reports.Count | Should Be 0
        $path = Join-Path $TestDrive 'Initialize-PSMSmokeTestDatabase-Report.md'
        $result = Invoke-TestInitialization $state -ReportPath $path
        $state.Reports.Count | Should Be 1
        $state.Reports[0].Content | Should Match '# PSM Smoke Test Database Initialization Report'
        $result.ReportPath | Should Be $path
    }
    It 'rejects a report path with a missing parent before SQL access' {
        $state = New-TestState
        $path = Join-Path (Join-Path $TestDrive 'missing') 'report.md'
        $result = Invoke-TestInitialization $state -ReportPath $path
        $result.Overall | Should Be FAILED
        $state.Queries.Count | Should Be 0
    }
    It 'does not expose suppressed exception text, passwords, or connection strings' {
        $state = New-TestState
        $state.LoginExists=$false; $state.FailMutation='CreateLogin'
        $result = Invoke-TestInitialization $state
        $serialized = $result | ConvertTo-Json -Depth 6
        $serialized | Should Not Match 'sensitive sql error'
        $serialized | Should Not Match '(?i)password\s*='
        $serialized | Should Not Match '(?i)integrated security\s*='
    }
}

Describe 'Static safety contract' {
    $text = Get-Content -Raw $scriptPath
    It 'contains no prohibited environment or package mutation' {
        $text | Should Not Match '(?i)\b(Install-Module|Install-Package|Save-Module|Invoke-Command|Start-Service|Stop-Service|Restart-Service|New-NetFirewallRule|Set-AD|New-AD|Install-ADServiceAccount)\b'
    }
    It 'contains no destructive SQL, migration, schema, or table operation' {
        $text | Should Not Match '(?im)^\s*(DROP|TRUNCATE)\b'
        $text | Should Not Match '(?i)\b(dotnet\s+ef|Migrate\s*\(|CREATE\s+(TABLE|SCHEMA|PROCEDURE|TRIGGER))\b'
    }
    It 'never grants prohibited roles' {
        $mutationSql = @([regex]::Matches($text, '(?ms)-- PSM:MUTATE:.*?(?="(?:@|\r?\n\s*\$))') | ForEach-Object Value) -join "`n"
        $mutationSql | Should Not Match '(?i)ALTER\s+(SERVER\s+)?ROLE\s+\[(db_owner|db_ddladmin|db_securityadmin|sysadmin|securityadmin|serveradmin|setupadmin|processadmin|diskadmin|bulkadmin)\]\s+ADD'
    }
    It 'uses Windows Integrated invocation and no SQL credential parameter' {
        $text | Should Match 'Invoke-Sqlcmd'
        $text | Should Not Match '(?i)\-(Username|Password|Credential)\b'
        $text | Should Match 'FROM WINDOWS'
        $text | Should Not Match '(?i)CREATE LOGIN.+PASSWORD'
    }
    It 'has no DROP statement and only approved ALTER ROLE mutations' {
        $text | Should Not Match '(?i)\bDROP\s+(DATABASE|LOGIN|USER|ROLE)\b'
        $text | Should Match 'ALTER ROLE \$roleIdentifier ADD MEMBER'
    }
    It 'parses without syntax errors' {
        $tokens=$null; $errors=$null
        $null=[Management.Automation.Language.Parser]::ParseFile($scriptPath,[ref]$tokens,[ref]$errors)
        $errors.Count | Should Be 0
    }
}
