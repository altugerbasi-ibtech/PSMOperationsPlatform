#requires -Version 5.1
<#
.SYNOPSIS
Initializes the database security principals required by a PSM smoke test.

.DESCRIPTION
Creates a missing smoke-test database, Windows login, mapped database user,
and db_datareader/db_datawriter memberships. Existing objects are verified and
left unchanged. SQL Server is accessed with Windows Integrated Authentication.

.PARAMETER Server
SQL Server host name or fully qualified domain name.

.PARAMETER Database
Non-production smoke-test database name.

.PARAMETER ServiceAccount
Windows account in DOMAIN\Account format.

.PARAMETER ReportPath
Optional Markdown report file. Its parent directory must already exist.

.EXAMPLE
.\Initialize-PSMSmokeTestDatabase.ps1 -WhatIf

.EXAMPLE
.\Initialize-PSMSmokeTestDatabase.ps1 -ReportPath C:\Evidence\Initialize-PSMSmokeTestDatabase-Report.md

.NOTES
This deployment utility does not apply migrations or create application schema.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $Server = 'mydb01.ae.local',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $Database = 'PSMOperationsPlatform_SmokeTest',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $ServiceAccount = 'AE\gmsaSPWorker$',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $ReportPath
)

Set-StrictMode -Version Latest

function New-DeploymentResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Connectivity','Database','Login','User','db_datareader','db_datawriter','HigherPrivileges')][string]$Name,
        [Parameter(Mandatory)][ValidateSet('PASS','WARNING','FAIL','PLANNED','SKIPPED')][string]$Status,
        [Parameter(Mandatory)][string]$Summary,
        [Parameter(Mandatory)][bool]$Changed,
        [AllowNull()][string]$Recommendation
    )
    [pscustomobject][ordered]@{
        Name = $Name
        Status = $Status
        Summary = $Summary
        Changed = $Changed
        Recommendation = $Recommendation
    }
}

function Test-DeploymentInputs {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$ServiceAccount,
        [AllowNull()][string]$ReportPath
    )

    if ([string]::IsNullOrWhiteSpace($Server) -or $Server.Length -gt 253 -or
        $Server -notmatch '^(?=.{1,253}$)(?:(?!-)[A-Za-z0-9-]{1,63}(?<!-)\.)*(?!-)[A-Za-z0-9-]{1,63}(?<!-)$' -or
        $Server -match '^(?i:SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|EXEC|EXECUTE|MERGE|TRUNCATE)$') {
        throw 'Server must be a host name or FQDN without protocol, port, path, connection-string, or query syntax.'
    }
    if ([string]::IsNullOrWhiteSpace($Database) -or $Database.Length -gt 128 -or
        $Database -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$') {
        throw 'Database must be a safe SQL identifier of 1-128 letters, digits, dots, underscores, or hyphens.'
    }
    if ([string]::IsNullOrWhiteSpace($ServiceAccount) -or $ServiceAccount.Length -gt 128 -or
        $ServiceAccount -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,62}\\[A-Za-z0-9][A-Za-z0-9._-]{0,62}\$?$') {
        throw 'ServiceAccount must use the DOMAIN\Account form and contain only supported Windows account-name characters.'
    }
    if ($ReportPath) {
        $parent = Split-Path -Parent $ReportPath
        if ([string]::IsNullOrWhiteSpace($parent)) {
            $parent = (Get-Location).Path
        }
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            throw 'ReportPath parent directory must already exist.'
        }
        if (Test-Path -LiteralPath $ReportPath -PathType Container) {
            throw 'ReportPath must identify a file, not a directory.'
        }
    }
}

function ConvertTo-SqlIdentifier {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateLength(1,128)][string]$Value)
    '[' + $Value.Replace(']', ']]') + ']'
}

function ConvertTo-SqlUnicodeLiteral {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateLength(1,128)][string]$Value)
    $escapedValue = $Value.Replace("'", "''")
    "N'$escapedValue'"
}

function Get-DefaultDeploymentOperations {
    [CmdletBinding()]
    param()
    @{
        ClientAvailable = {
            $command = Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue
            if (-not $command) { return $false }
            return $command.ModuleName -eq 'SqlServer'
        }
        Query = {
            param($QueryServer, $QueryDatabase, $QueryText)
            Invoke-Sqlcmd -ServerInstance $QueryServer -Database $QueryDatabase `
                -Query $QueryText -QueryTimeout 30 -ConnectionTimeout 15 `
                -ErrorAction Stop
        }
        WriteReport = {
            param($Path, $Content)
            Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8 -ErrorAction Stop
        }
        MachineName = { [Environment]::MachineName }
        Identity = { [Security.Principal.WindowsIdentity]::GetCurrent().Name }
        Now = { Get-Date }
    }
}

function Get-FirstSqlRow {
    [CmdletBinding()]
    param([AllowNull()][object]$Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Data.DataTable]) {
        if ($Value.Rows.Count -eq 0) { return $null }
        return $Value.Rows[0]
    }
    @($Value)[0]
}

function Invoke-DeploymentQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][hashtable]$Operations,
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Query
    )
    & $Operations.Query $Server $Database $Query
}

function Get-DatabaseState {
    param($Operations,$Server,$Database,$DatabaseLiteral)
    Get-FirstSqlRow (Invoke-DeploymentQuery $Operations $Server 'master' @"
-- PSM:STATE:DATABASE
SELECT CASE WHEN d.database_id IS NULL THEN 0 ELSE 1 END AS [Exists],
       COALESCE(d.state_desc, N'') AS StateDescription
FROM (VALUES ($DatabaseLiteral)) AS requested(DatabaseName)
LEFT JOIN sys.databases AS d ON d.name = requested.DatabaseName;
"@)
}

function Get-LoginState {
    param($Operations,$Server,$LoginLiteral)
    Get-FirstSqlRow (Invoke-DeploymentQuery $Operations $Server 'master' @"
-- PSM:STATE:LOGIN
SELECT CASE WHEN sp.principal_id IS NULL THEN 0 ELSE 1 END AS [Exists],
       COALESCE(sp.type_desc, N'') AS TypeDescription,
       COALESCE(CONVERT(int, sp.is_disabled), 0) AS IsDisabled
FROM (VALUES ($LoginLiteral)) AS requested(LoginName)
LEFT JOIN sys.server_principals AS sp ON sp.name = requested.LoginName;
"@)
}

function Get-UserState {
    param($Operations,$Server,$Database,$LoginLiteral)
    Get-FirstSqlRow (Invoke-DeploymentQuery $Operations $Server $Database @"
-- PSM:STATE:USER
SELECT CASE WHEN dp.principal_id IS NULL THEN 0 ELSE 1 END AS [Exists],
       COALESCE(dp.type_desc, N'') AS TypeDescription,
       COALESCE(dp.authentication_type_desc, N'') AS AuthenticationTypeDescription,
       CASE WHEN dp.sid IS NOT NULL AND sp.sid = dp.sid THEN 1 ELSE 0 END AS MappingCorrect
FROM (VALUES ($LoginLiteral)) AS requested(LoginName)
LEFT JOIN sys.database_principals AS dp ON dp.name = requested.LoginName
LEFT JOIN master.sys.server_principals AS sp ON sp.name = requested.LoginName;
"@)
}

function Get-RoleState {
    param($Operations,$Server,$Database,$LoginLiteral,$RoleLiteral)
    Get-FirstSqlRow (Invoke-DeploymentQuery $Operations $Server $Database @"
-- PSM:STATE:ROLE
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM sys.database_role_members AS drm
    JOIN sys.database_principals AS rolep ON rolep.principal_id = drm.role_principal_id
    JOIN sys.database_principals AS memberp ON memberp.principal_id = drm.member_principal_id
    WHERE rolep.name = $RoleLiteral AND memberp.name = $LoginLiteral
) THEN 1 ELSE 0 END AS IsMember;
"@)
}

function Get-HigherPrivilegeState {
    param($Operations,$Server,$Database,$LoginLiteral)
    $databaseRoles = @(Invoke-DeploymentQuery $Operations $Server $Database @"
-- PSM:STATE:HIGHER_DATABASE_ROLES
SELECT rolep.name AS RoleName
FROM sys.database_role_members AS drm
JOIN sys.database_principals AS rolep ON rolep.principal_id = drm.role_principal_id
JOIN sys.database_principals AS memberp ON memberp.principal_id = drm.member_principal_id
WHERE memberp.name = $LoginLiteral
  AND rolep.name IN (N'db_owner', N'db_ddladmin', N'db_securityadmin')
ORDER BY rolep.name;
"@)
    $serverRoles = @(Invoke-DeploymentQuery $Operations $Server 'master' @"
-- PSM:STATE:HIGHER_SERVER_ROLES
SELECT rolep.name AS RoleName
FROM sys.server_role_members AS srm
JOIN sys.server_principals AS rolep ON rolep.principal_id = srm.role_principal_id
JOIN sys.server_principals AS memberp ON memberp.principal_id = srm.member_principal_id
WHERE memberp.name = $LoginLiteral
  AND rolep.name IN (N'sysadmin', N'securityadmin', N'serveradmin')
ORDER BY rolep.name;
"@)
    @($databaseRoles + $serverRoles | ForEach-Object { $_.RoleName } | Where-Object { $_ } | Sort-Object -Unique)
}

function Set-Result {
    param([System.Collections.Generic.List[object]]$Results,[object]$Result)
    $existing = $Results.FindIndex([Predicate[object]]{ param($item) $item.Name -eq $Result.Name })
    if ($existing -ge 0) { $Results[$existing] = $Result } else { $Results.Add($Result) }
}

function Get-DeploymentOverall {
    param([object[]]$Results)
    $required = @($Results | Where-Object Name -in @('Connectivity','Database','Login','User','db_datareader','db_datawriter'))
    if ($Results | Where-Object Status -eq 'FAIL') { return 'FAILED' }
    if ($required | Where-Object Status -in @('PLANNED','SKIPPED')) { return 'WHATIF' }
    if ($Results | Where-Object { $_.Name -eq 'HigherPrivileges' -and $_.Status -eq 'WARNING' }) { return 'WARNING' }
    'READY'
}

function Get-DeploymentExitCode {
    param([string]$Overall)
    switch ($Overall) {
        'READY' { 0 }
        'WARNING' { 1 }
        'WHATIF' { 3 }
        default { 2 }
    }
}

function ConvertTo-DeploymentMarkdown {
    param($Manifest)
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# PSM Smoke Test Database Initialization Report')
    $lines.Add('')
    $lines.Add('## Manifest')
    $lines.Add('')
    foreach ($entry in @(
        "GeneratedAt: $($Manifest.GeneratedAt)",
        "ExecutingMachine: $($Manifest.ExecutingMachine)",
        "ExecutingIdentity: $($Manifest.ExecutingIdentity)",
        "Server: $($Manifest.Server)",
        "Database: $($Manifest.Database)",
        "ServiceAccount: $($Manifest.ServiceAccount)",
        "WhatIfMode: $($Manifest.WhatIfMode)"
    )) { $lines.Add("- $entry") }
    $lines.Add('')
    $lines.Add('## Results')
    $lines.Add('')
    $lines.Add('| Name | Status | Summary | Changed |')
    $lines.Add('|---|---|---|---|')
    foreach ($result in $Manifest.Results) {
        $summary = ([string]$result.Summary).Replace('|','\|')
        $lines.Add("| $($result.Name) | $($result.Status) | $summary | $($result.Changed) |")
    }
    $lines.Add('')
    $lines.Add('## Existing Higher Privileges')
    $lines.Add('')
    if ($Manifest.HigherPrivileges.Count) {
        foreach ($role in $Manifest.HigherPrivileges) { $lines.Add("- $role") }
    } else { $lines.Add('None detected through direct SQL Server role membership metadata.') }
    $lines.Add('')
    $lines.Add('## Required Manual Actions')
    $lines.Add('')
    $actions = @($Manifest.Results | Where-Object Recommendation | Select-Object -ExpandProperty Recommendation -Unique)
    if ($actions.Count) { foreach ($action in $actions) { $lines.Add("- $action") } } else { $lines.Add('None') }
    $lines.Add('')
    $lines.Add('## Security Confirmation')
    $lines.Add('')
    $lines.Add('Windows Integrated Authentication was selected. No password, credential, token, raw connection string, migration, destructive rollback, or application schema operation is included.')
    $lines.Add('')
    $lines.Add("Overall: **$($Manifest.Overall)**")
    $lines.Add('')
    $lines.Add("Exit Code: **$($Manifest.ExitCode)**")
    $lines -join [Environment]::NewLine
}

function Write-DeploymentConsole {
    param($Manifest)
    foreach ($result in $Manifest.Results) {
        Write-Host "[$($result.Status)] $($result.Name) - $($result.Summary)"
    }
    Write-Host '========================================'
    Write-Host ("Server:         {0}" -f $Manifest.Server)
    Write-Host ("Database:       {0}" -f $Manifest.Database)
    Write-Host ("ServiceAccount: {0}" -f $Manifest.ServiceAccount)
    Write-Host '----------------------------------------'
    foreach ($name in @('Database','Login','User','db_datareader','db_datawriter','HigherPrivileges')) {
        $item = $Manifest.Results | Where-Object Name -eq $name
        Write-Host ("{0,-16}{1}" -f (($name -replace 'HigherPrivileges','Privileges') + ':'), $item.Status)
    }
    Write-Host '----------------------------------------'
    Write-Host ("Overall:        {0}" -f $Manifest.Overall)
    Write-Host ("Exit Code:      {0}" -f $Manifest.ExitCode)
    Write-Host '========================================'
}

function Invoke-PSMSmokeTestDatabaseInitialization {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$ServiceAccount,
        [AllowNull()][string]$ReportPath,
        [Parameter(Mandatory)][hashtable]$Operations,
        [Parameter(Mandatory)][scriptblock]$ShouldProcess,
        [Parameter(Mandatory)][bool]$WhatIfMode
    )

    $results = New-Object System.Collections.Generic.List[object]
    $higherPrivileges = @()
    try {
        Test-DeploymentInputs $Server $Database $ServiceAccount $ReportPath
    } catch {
        Set-Result $results (New-DeploymentResult Connectivity FAIL $_.Exception.Message $false 'Correct the input values and rerun; no SQL connection was attempted.')
        foreach ($name in @('Database','Login','User','db_datareader','db_datawriter','HigherPrivileges')) {
            Set-Result $results (New-DeploymentResult $name SKIPPED 'Not evaluated because parameter validation failed.' $false 'Resolve parameter validation first.')
        }
    }

    if ($results.Count -eq 0 -and -not (& $Operations.ClientAvailable)) {
        Set-Result $results (New-DeploymentResult Connectivity FAIL 'SqlServer PowerShell module with Invoke-Sqlcmd is unavailable.' $false 'Install the approved SqlServer module through the controlled software process, then rerun.')
        foreach ($name in @('Database','Login','User','db_datareader','db_datawriter','HigherPrivileges')) {
            Set-Result $results (New-DeploymentResult $name SKIPPED 'Not evaluated because the approved SQL client is unavailable.' $false 'Resolve SQL client availability first.')
        }
    }

    $dbIdentifier = ConvertTo-SqlIdentifier $Database
    $accountIdentifier = ConvertTo-SqlIdentifier $ServiceAccount
    $databaseLiteral = ConvertTo-SqlUnicodeLiteral $Database
    $accountLiteral = ConvertTo-SqlUnicodeLiteral $ServiceAccount

    if ($results.Count -eq 0) {
        try {
            $connectivityQuery = '-- PSM:CONNECTIVITY' + [Environment]::NewLine +
                "SELECT 1 AS Connected, CONVERT(int, SERVERPROPERTY(N'ProductMajorVersion')) AS ProductMajorVersion;"
            $connectivity = Get-FirstSqlRow (Invoke-DeploymentQuery $Operations $Server 'master' $connectivityQuery)
            if ([int]$connectivity.ProductMajorVersion -lt 16) {
                Set-Result $results (New-DeploymentResult Connectivity FAIL "Connected to $Server, but SQL Server 2022 or later is required." $false 'Use a supported non-production SQL Server 2022 or later target.')
                foreach ($name in @('Database','Login','User','db_datareader','db_datawriter','HigherPrivileges')) {
                    Set-Result $results (New-DeploymentResult $name SKIPPED 'Not evaluated because the SQL Server version is unsupported.' $false 'Select a supported SQL Server target.')
                }
            } else {
                Set-Result $results (New-DeploymentResult Connectivity PASS "Connected to $Server with Windows Integrated Authentication; SQL Server version is supported." $false $null)
            }
        } catch {
            Set-Result $results (New-DeploymentResult Connectivity FAIL "Could not connect to $Server with Windows Integrated Authentication." $false 'Verify DNS, TLS, SQL availability, and executing-account permissions. SQL exception details were suppressed.')
            foreach ($name in @('Database','Login','User','db_datareader','db_datawriter','HigherPrivileges')) {
                Set-Result $results (New-DeploymentResult $name SKIPPED 'Not evaluated because SQL connectivity failed.' $false 'Resolve SQL connectivity first.')
            }
        }
    }

    if (($results | Where-Object Name -eq Connectivity).Status -eq 'PASS') {
        try {
            $dbState = Get-DatabaseState $Operations $Server $Database $databaseLiteral
            if ([int]$dbState.Exists -eq 0) {
                if (& $ShouldProcess "$Server/$Database" 'Create smoke-test database') {
                    $createDatabaseQuery = @"
-- PSM:MUTATE:CREATE_DATABASE
IF DB_ID($databaseLiteral) IS NULL CREATE DATABASE $dbIdentifier;
"@
                    $null = Invoke-DeploymentQuery $Operations $Server 'master' $createDatabaseQuery
                    $dbState = Get-DatabaseState $Operations $Server $Database $databaseLiteral
                    if ([int]$dbState.Exists -ne 1) { throw 'Database creation could not be verified.' }
                    Set-Result $results (New-DeploymentResult Database PASS 'Created and verified.' $true $null)
                } else {
                    Set-Result $results (New-DeploymentResult Database PLANNED 'Create database was not applied.' $false 'Rerun without -WhatIf or approve the confirmation prompt.')
                }
            } elseif ($dbState.StateDescription -ne 'ONLINE') {
                Set-Result $results (New-DeploymentResult Database FAIL "Exists but state is $($dbState.StateDescription)." $false 'Restore the database to ONLINE state manually; this utility will not alter database state.')
            } else {
                Set-Result $results (New-DeploymentResult Database PASS 'Already exists and is ONLINE.' $false $null)
            }
        } catch {
            Set-Result $results (New-DeploymentResult Database FAIL 'Database existence, creation, or ONLINE-state verification failed.' $false 'Review SQL permissions/state and rerun safely; created objects are not rolled back.')
        }

        try {
            $loginState = Get-LoginState $Operations $Server $accountLiteral
            if ([int]$loginState.Exists -eq 0) {
                if (& $ShouldProcess "$Server/$ServiceAccount" 'Create Windows login') {
                    $createLoginQuery = @"
-- PSM:MUTATE:CREATE_LOGIN
IF SUSER_ID($accountLiteral) IS NULL CREATE LOGIN $accountIdentifier FROM WINDOWS;
"@
                    $null = Invoke-DeploymentQuery $Operations $Server 'master' $createLoginQuery
                    $loginState = Get-LoginState $Operations $Server $accountLiteral
                    if ([int]$loginState.Exists -ne 1) { throw 'Login creation could not be verified.' }
                    Set-Result $results (New-DeploymentResult Login PASS 'Created Windows login and verified it.' $true $null)
                } else {
                    Set-Result $results (New-DeploymentResult Login PLANNED 'Create Windows login was not applied.' $false 'Rerun without -WhatIf or approve the confirmation prompt.')
                }
            } elseif ($loginState.TypeDescription -notin @('WINDOWS_LOGIN','WINDOWS_GROUP')) {
                Set-Result $results (New-DeploymentResult Login FAIL "Existing principal type is $($loginState.TypeDescription)." $false 'Resolve the conflicting server principal manually; it was not changed.')
            } elseif ([int]$loginState.IsDisabled -eq 1) {
                Set-Result $results (New-DeploymentResult Login FAIL 'Existing Windows login is disabled.' $false 'Review and enable the login manually if approved; this utility does not change login state.')
            } else {
                Set-Result $results (New-DeploymentResult Login PASS 'Already exists as an enabled Windows principal.' $false $null)
            }
        } catch {
            Set-Result $results (New-DeploymentResult Login FAIL 'Windows login existence, creation, or verification failed.' $false 'Review server permissions and rerun safely; completed objects are not rolled back.')
        }

        $databaseUsable = ($results | Where-Object Name -eq Database).Status -eq 'PASS'
        $loginUsable = ($results | Where-Object Name -eq Login).Status -eq 'PASS'
        if ($databaseUsable -and $loginUsable) {
            try {
                $userState = Get-UserState $Operations $Server $Database $accountLiteral
                if ([int]$userState.Exists -eq 0) {
                    if (& $ShouldProcess "$Server/$Database/$ServiceAccount" 'Create mapped database user') {
                        $createUserQuery = @"
-- PSM:MUTATE:CREATE_USER
IF DATABASE_PRINCIPAL_ID($accountLiteral) IS NULL CREATE USER $accountIdentifier FOR LOGIN $accountIdentifier;
"@
                        $null = Invoke-DeploymentQuery $Operations $Server $Database $createUserQuery
                        $userState = Get-UserState $Operations $Server $Database $accountLiteral
                        if ([int]$userState.Exists -ne 1 -or [int]$userState.MappingCorrect -ne 1) { throw 'User creation could not be verified.' }
                        Set-Result $results (New-DeploymentResult User PASS 'Created mapped database user and verified it.' $true $null)
                    } else {
                        Set-Result $results (New-DeploymentResult User PLANNED 'Create database user was not applied.' $false 'Rerun without -WhatIf or approve the confirmation prompt.')
                    }
                } elseif ([int]$userState.MappingCorrect -ne 1 -or $userState.AuthenticationTypeDescription -notin @('WINDOWS','INSTANCE')) {
                    Set-Result $results (New-DeploymentResult User FAIL 'Existing database user is orphaned, incorrectly mapped, or has an unexpected authentication type.' $false 'Repair the user/login mapping manually; this utility will not remap it.')
                } else {
                    Set-Result $results (New-DeploymentResult User PASS 'Already exists with the correct login mapping.' $false $null)
                }
            } catch {
                Set-Result $results (New-DeploymentResult User FAIL 'Database user existence, creation, or mapping verification failed.' $false 'Review database permissions/mapping and rerun safely; completed objects are not rolled back.')
            }
        } else {
            Set-Result $results (New-DeploymentResult User SKIPPED 'Not evaluated because the database or login is not ready.' $false 'Resolve the database and login results, then rerun.')
        }

        foreach ($role in @('db_datareader','db_datawriter')) {
            if (($results | Where-Object Name -eq User).Status -eq 'PASS') {
                try {
                    $roleLiteral = ConvertTo-SqlUnicodeLiteral $role
                    $roleIdentifier = ConvertTo-SqlIdentifier $role
                    $roleState = Get-RoleState $Operations $Server $Database $accountLiteral $roleLiteral
                    if ([int]$roleState.IsMember -eq 0) {
                        if (& $ShouldProcess "$Server/$Database/$ServiceAccount" "Add $role membership") {
                            $addRoleQuery = @"
-- PSM:MUTATE:ADD_ROLE:$role
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members drm
    WHERE drm.role_principal_id = DATABASE_PRINCIPAL_ID($roleLiteral)
      AND drm.member_principal_id = DATABASE_PRINCIPAL_ID($accountLiteral)
) ALTER ROLE $roleIdentifier ADD MEMBER $accountIdentifier;
"@
                            $null = Invoke-DeploymentQuery $Operations $Server $Database $addRoleQuery
                            $roleState = Get-RoleState $Operations $Server $Database $accountLiteral $roleLiteral
                            if ([int]$roleState.IsMember -ne 1) { throw 'Role assignment could not be verified.' }
                            Set-Result $results (New-DeploymentResult $role PASS 'Assigned and verified.' $true $null)
                        } else {
                            Set-Result $results (New-DeploymentResult $role PLANNED 'Role membership was not applied.' $false 'Rerun without -WhatIf or approve the confirmation prompt.')
                        }
                    } else {
                        Set-Result $results (New-DeploymentResult $role PASS 'Already assigned.' $false $null)
                    }
                } catch {
                    Set-Result $results (New-DeploymentResult $role FAIL 'Role membership check, assignment, or verification failed.' $false 'Review database permissions and rerun safely; no automatic rollback is performed.')
                }
            } else {
                Set-Result $results (New-DeploymentResult $role SKIPPED 'Not evaluated because the mapped database user is not ready.' $false 'Resolve the database user result, then rerun.')
            }
        }

        if ($databaseUsable -and $loginUsable) {
            try {
                $higherPrivileges = @(Get-HigherPrivilegeState $Operations $Server $Database $accountLiteral)
                if ($higherPrivileges.Count) {
                    Set-Result $results (New-DeploymentResult HigherPrivileges WARNING ("Direct SQL role memberships detected: " + ($higherPrivileges -join ', ') + '.') $false 'Review whether these existing higher privileges are still required; this utility does not remove them. Indirect AD group permissions may not be fully represented.')
                } else {
                    Set-Result $results (New-DeploymentResult HigherPrivileges PASS 'No selected higher direct SQL role membership was detected.' $false 'Indirect AD group permissions may not be fully represented by this inspection.')
                }
            } catch {
                Set-Result $results (New-DeploymentResult HigherPrivileges FAIL 'Higher-privilege inspection failed.' $false 'Inspect server and database role memberships manually; no privileges were changed.')
            }
        } else {
            Set-Result $results (New-DeploymentResult HigherPrivileges SKIPPED 'Not evaluated because the database or login is not ready.' $false 'Resolve prerequisite failures and inspect existing privileges.')
        }

        $requiredNow = @($results | Where-Object Name -in @('Database','Login','User','db_datareader','db_datawriter'))
        if (-not ($requiredNow | Where-Object Status -in @('FAIL','PLANNED','SKIPPED'))) {
            try {
                $finalDatabase = Get-DatabaseState $Operations $Server $Database $databaseLiteral
                $finalLogin = Get-LoginState $Operations $Server $accountLiteral
                $finalUser = Get-UserState $Operations $Server $Database $accountLiteral
                $finalReader = Get-RoleState $Operations $Server $Database $accountLiteral (ConvertTo-SqlUnicodeLiteral 'db_datareader')
                $finalWriter = Get-RoleState $Operations $Server $Database $accountLiteral (ConvertTo-SqlUnicodeLiteral 'db_datawriter')
                if ([int]$finalDatabase.Exists -ne 1 -or $finalDatabase.StateDescription -ne 'ONLINE') {
                    Set-Result $results (New-DeploymentResult Database FAIL 'Final verification did not find an ONLINE database.' $false 'Review database state and rerun safely.')
                }
                if ([int]$finalLogin.Exists -ne 1 -or $finalLogin.TypeDescription -notin @('WINDOWS_LOGIN','WINDOWS_GROUP') -or [int]$finalLogin.IsDisabled -eq 1) {
                    Set-Result $results (New-DeploymentResult Login FAIL 'Final verification did not find an enabled Windows principal.' $false 'Review the server principal and rerun safely.')
                }
                if ([int]$finalUser.Exists -ne 1 -or [int]$finalUser.MappingCorrect -ne 1 -or $finalUser.AuthenticationTypeDescription -notin @('WINDOWS','INSTANCE')) {
                    Set-Result $results (New-DeploymentResult User FAIL 'Final verification did not find the expected mapped database user.' $false 'Review the database principal mapping and rerun safely.')
                }
                if ([int]$finalReader.IsMember -ne 1) {
                    Set-Result $results (New-DeploymentResult db_datareader FAIL 'Final verification did not find the required membership.' $false 'Review role membership and rerun safely.')
                }
                if ([int]$finalWriter.IsMember -ne 1) {
                    Set-Result $results (New-DeploymentResult db_datawriter FAIL 'Final verification did not find the required membership.' $false 'Review role membership and rerun safely.')
                }
            } catch {
                Set-Result $results (New-DeploymentResult Connectivity FAIL 'Final state verification failed.' $false 'Review SQL availability and permissions, then rerun safely. Exception details were suppressed.')
            }
        }
    }

    $ordered = @('Connectivity','Database','Login','User','db_datareader','db_datawriter','HigherPrivileges') |
        ForEach-Object { $name = $_; $results | Where-Object Name -eq $name }
    $overall = Get-DeploymentOverall $ordered
    $manifest = [pscustomobject][ordered]@{
        GeneratedAt = (& $Operations.Now).ToString('yyyy-MM-ddTHH:mm:ss.fffK')
        ExecutingMachine = & $Operations.MachineName
        ExecutingIdentity = & $Operations.Identity
        Server = $Server
        Database = $Database
        ServiceAccount = $ServiceAccount
        WhatIfMode = $WhatIfMode
        Results = $ordered
        HigherPrivileges = $higherPrivileges
        Overall = $overall
        ExitCode = Get-DeploymentExitCode $overall
        ReportPath = $null
    }

    if ($ReportPath) {
        if (& $ShouldProcess $ReportPath 'Write Markdown initialization report') {
            try {
                & $Operations.WriteReport $ReportPath (ConvertTo-DeploymentMarkdown $manifest)
                $manifest.ReportPath = $ReportPath
            } catch {
                Set-Result $results (New-DeploymentResult Connectivity FAIL 'Markdown report write failed after SQL processing.' $false 'Correct ReportPath access and rerun; SQL state can be verified idempotently.')
                $manifest.Results = @('Connectivity','Database','Login','User','db_datareader','db_datawriter','HigherPrivileges') |
                    ForEach-Object { $name = $_; $results | Where-Object Name -eq $name }
                $manifest.Overall = 'FAILED'
                $manifest.ExitCode = 2
            }
        }
    }
    $manifest
}

if ($MyInvocation.InvocationName -ne '.') {
    $operations = Get-DefaultDeploymentOperations
    $shouldProcessOperation = { param($target,$action) $PSCmdlet.ShouldProcess($target,$action) }
    try {
        $manifest = Invoke-PSMSmokeTestDatabaseInitialization -Server $Server -Database $Database `
            -ServiceAccount $ServiceAccount -ReportPath $ReportPath -Operations $operations `
            -ShouldProcess $shouldProcessOperation -WhatIfMode ([bool]$WhatIfPreference)
    } catch {
        Write-Host '[FAIL] Connectivity - Unexpected internal failure; details were suppressed.'
        exit 2
    }
    Write-DeploymentConsole $manifest
    exit $manifest.ExitCode
}
