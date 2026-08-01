#requires -Version 5.1
Set-StrictMode -Version Latest

$script:OperationalStatuses = @('PASS','WARNING','FAIL','SKIPPED','NOT_APPLICABLE')
$script:OperationalSeverities = @('INFO','LOW','MEDIUM','HIGH','CRITICAL')

function Protect-OperationalText {
    [CmdletBinding()]
    param([AllowNull()][object]$Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [Security.SecureString] -or $Value -is [Management.Automation.PSCredential]) {
        return '[REDACTED]'
    }
    $text = [string]$Value
    if ($text -match '(?i)(password|pwd|secret|token|private\s*key)\s*[:=]' -or
        $text -match '(?i)(server|data source)\s*=.+;(database|initial catalog)\s*=') {
        return '[REDACTED]'
    }
    return ($text -replace '(?i)(password|pwd|secret|token)\s*=\s*[^;,\s]+','$1=[REDACTED]')
}

function New-OperationalValidationResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[A-Z0-9]+(\.[A-Z0-9]+)+$')][string]$CheckId,
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][ValidateSet('PASS','WARNING','FAIL','SKIPPED','NOT_APPLICABLE')][string]$Status,
        [Parameter(Mandatory)][ValidateSet('INFO','LOW','MEDIUM','HIGH','CRITICAL')][string]$Severity,
        [Parameter(Mandatory)][datetime]$StartedAt,
        [Parameter(Mandatory)][datetime]$CompletedAt,
        [AllowNull()][object]$Expected,
        [AllowNull()][object]$Actual,
        [Parameter(Mandatory)][string]$Message,
        [AllowNull()][string]$Recommendation,
        [AllowNull()][object]$Evidence,
        [AllowNull()][string]$ExceptionType,
        [AllowNull()][string]$ExceptionMessage,
        [bool]$Mandatory = $false
    )
    [pscustomobject][ordered]@{
        CheckId = $CheckId
        Category = $Category
        Name = $Name
        Description = $Description
        Target = Protect-OperationalText $Target
        Status = $Status
        Severity = $Severity
        StartedAt = $StartedAt.ToUniversalTime().ToString('o')
        CompletedAt = $CompletedAt.ToUniversalTime().ToString('o')
        DurationMilliseconds = [math]::Max(0,[long]($CompletedAt - $StartedAt).TotalMilliseconds)
        Expected = Protect-OperationalText $Expected
        Actual = Protect-OperationalText $Actual
        Message = Protect-OperationalText $Message
        Recommendation = Protect-OperationalText $Recommendation
        Evidence = Protect-OperationalText $Evidence
        ExceptionType = Protect-OperationalText $ExceptionType
        ExceptionMessage = Protect-OperationalText $ExceptionMessage
        Mandatory = $Mandatory
    }
}

function New-OperationalObservation {
    param(
        [string]$CheckId,[string]$Category,[string]$Name,[string]$Description,
        [string]$Target,[string]$Status,[string]$Severity,[object]$Expected,
        [object]$Actual,[string]$Message,[string]$Recommendation,
        [object]$Evidence,[bool]$Mandatory = $false
    )
    $now = [datetime]::UtcNow
    New-OperationalValidationResult -CheckId $CheckId -Category $Category `
        -Name $Name -Description $Description -Target $Target -Status $Status `
        -Severity $Severity -StartedAt $now -CompletedAt $now -Expected $Expected `
        -Actual $Actual -Message $Message -Recommendation $Recommendation `
        -Evidence $Evidence -Mandatory $Mandatory
}

function New-OperationalExceptionResult {
    param(
        [string]$CheckId,[string]$Category,[string]$Name,[string]$Description,
        [string]$Target,[System.Exception]$Exception,[string]$Severity = 'HIGH',
        [bool]$Mandatory = $true
    )
    $now = [datetime]::UtcNow
    New-OperationalValidationResult -CheckId $CheckId -Category $Category `
        -Name $Name -Description $Description -Target $Target -Status FAIL `
        -Severity $Severity -StartedAt $now -CompletedAt $now -Expected 'check completes' `
        -Actual 'exception' -Message 'The read-only validation check failed.' `
        -Recommendation 'Review sanitized diagnostics and rerun after approved remediation.' `
        -Evidence $null -ExceptionType $Exception.GetType().Name `
        -ExceptionMessage (Protect-OperationalText $Exception.Message) -Mandatory $Mandatory
}

function Assert-OperationalResults {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Results)
    $required = @('CheckId','Category','Name','Description','Target','Status','Severity',
        'StartedAt','CompletedAt','DurationMilliseconds','Expected','Actual','Message',
        'Recommendation','Evidence','ExceptionType','ExceptionMessage')
    $ids = @{}
    foreach ($result in $Results) {
        foreach ($property in $required) {
            if ($null -eq $result.PSObject.Properties[$property]) {
                throw "Malformed result object: missing $property."
            }
        }
        if ($result.Status -notin $script:OperationalStatuses) { throw "Invalid status: $($result.Status)." }
        if ($result.Severity -notin $script:OperationalSeverities) { throw "Invalid severity: $($result.Severity)." }
        if ($ids.ContainsKey($result.CheckId)) { throw "Duplicate check identifier: $($result.CheckId)." }
        $ids[$result.CheckId] = $true
    }
}

function Get-OperationalOverallStatus {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Results)
    Assert-OperationalResults $Results
    if ($Results | Where-Object Status -eq 'FAIL') { return 'FAIL' }
    if ($Results | Where-Object Status -eq 'WARNING') { return 'WARNING' }
    if ($Results | Where-Object { $_.Mandatory -and $_.Status -eq 'SKIPPED' }) { return 'WARNING' }
    return 'PASS'
}

function Get-OperationalExitCode {
    param([Parameter(Mandatory)][ValidateSet('PASS','WARNING','FAIL','INVALID_CONFIGURATION','EXECUTION_ERROR')][string]$Status)
    switch ($Status) {
        PASS { 0 }
        WARNING { 1 }
        FAIL { 2 }
        INVALID_CONFIGURATION { 3 }
        default { 4 }
    }
}

function Get-OperationalConfiguration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ConfigurationPath,[Parameter(Mandatory)][string]$RepositoryRoot)
    $validator = Join-Path $RepositoryRoot 'Release\Deployment\Test-DeploymentConfiguration.ps1'
    $schema = Join-Path $RepositoryRoot 'Release\Deployment\DeploymentConfiguration.schema.json'
    if (-not (Test-Path -LiteralPath $schema -PathType Leaf)) { throw 'Deployment configuration schema is missing.' }
    if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw 'Deployment configuration validator is missing.' }
    $validationOutput = & $validator -Path $ConfigurationPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Invalid deployment configuration: $($validationOutput -join ' ')" }
    Get-Content -LiteralPath $ConfigurationPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
}

function Get-OperationalConfigurationHash {
    param([Parameter(Mandatory)][string]$ConfigurationPath)
    (Get-FileHash -LiteralPath $ConfigurationPath -Algorithm SHA256).Hash
}

function Get-PortalAuthenticationCompositionFacts {
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    $program = Get-Content -Raw (Join-Path $RepositoryRoot 'src\PSMOperationsPlatform.Web\Program.cs')
    $composition = Get-Content -Raw (Join-Path $RepositoryRoot 'src\PSMOperationsPlatform.Web\Security\PortalAuthenticationComposition.cs')
    $authenticationIndex = $composition.IndexOf('UseAuthentication()', [StringComparison]::Ordinal)
    $authorizationIndex = $composition.IndexOf('UseAuthorization()', [StringComparison]::Ordinal)

    $facts = [pscustomobject]@{
        IisWindowsScheme = $composition -match 'AddAuthentication\(IISDefaults\.AuthenticationScheme\)'
        AuthenticationMiddleware = $authenticationIndex -ge 0
        AuthorizationMiddleware = $authorizationIndex -gt $authenticationIndex
        AuthenticatedFallbackPolicy = $composition -match 'FallbackPolicy' -and $composition -match 'RequireAuthenticatedUser\(\)'
        AnonymousHealth = $program -match 'MapHealthChecks\("/health"\)\.AllowAnonymous\(\)'
    }
    $facts | Add-Member Composed ([bool]($facts.IisWindowsScheme -and $facts.AuthenticationMiddleware -and `
        $facts.AuthorizationMiddleware -and $facts.AuthenticatedFallbackPolicy -and $facts.AnonymousHealth))
    $facts
}

function Test-OperationalPort {
    param([object]$Port)
    return ($Port -is [int] -and $Port -ge 1 -and $Port -le 65535)
}

function Test-OperationalPathFormat {
    param([AllowNull()][string]$Path,[bool]$AllowUnc = $false)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if (-not $AllowUnc -and $Path.StartsWith('\\')) { return $false }
    try { return [IO.Path]::IsPathRooted($Path) } catch { return $false }
}

function Resolve-OperationalDns {
    param([Parameter(Mandatory)][string]$Name)
    [Net.Dns]::GetHostAddresses($Name) | ForEach-Object IPAddressToString
}

function Test-OperationalTcpPort {
    param([Parameter(Mandatory)][string]$HostName,[Parameter(Mandatory)][int]$Port,[int]$TimeoutMilliseconds = 3000)
    $client = [Net.Sockets.TcpClient]::new()
    try {
        $async = $client.BeginConnect($HostName,$Port,$null,$null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMilliseconds,$false)) { return $false }
        $client.EndConnect($async)
        return $true
    } finally { $client.Dispose() }
}

function Get-OperationalService {
    param([Parameter(Mandatory)][string]$Name)
    Get-Service -Name $Name -ErrorAction Stop
}

function Get-OperationalRegistryValue {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Name)
    (Get-ItemProperty -LiteralPath $Path -Name $Name -ErrorAction Stop).$Name
}

function Invoke-OperationalSqlMetadata {
    param([Parameter(Mandatory)][object]$Configuration)
    $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = "$($Configuration.SqlServer.Server),$($Configuration.SqlServer.Port)"
    $builder['Initial Catalog'] = $Configuration.SqlServer.Database
    $builder['Integrated Security'] = $true
    $builder['Encrypt'] = $true
    $builder['TrustServerCertificate'] = $false
    $builder['Connect Timeout'] = 10
    $connection = [Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = @'
SELECT SUSER_SNAME() AS LoginName,
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
       CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
       DB_NAME() AS DatabaseName, d.compatibility_level, d.collation_name,
       d.recovery_model_desc, c.encrypt_option
FROM sys.databases d
CROSS JOIN sys.dm_exec_connections c
WHERE d.database_id=DB_ID() AND c.session_id=@@SPID;
'@
        $reader = $command.ExecuteReader()
        if (-not $reader.Read()) { throw 'SQL metadata row was not returned.' }
        [pscustomobject]@{
            LoginName=[string]$reader['LoginName']; ProductVersion=[string]$reader['ProductVersion']
            Edition=[string]$reader['Edition']; DatabaseName=[string]$reader['DatabaseName']
            CompatibilityLevel=[int]$reader['compatibility_level']; Collation=[string]$reader['collation_name']
            RecoveryModel=[string]$reader['recovery_model_desc']; Encryption=[string]$reader['encrypt_option']
        }
        $reader.Dispose()
    } finally { $connection.Dispose() }
}
