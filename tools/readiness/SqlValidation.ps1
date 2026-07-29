Set-StrictMode -Version Latest

function Invoke-ReadinessSqlQuery {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ConnectionString,[Parameter(Mandatory)][string]$Query)
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
        $table = New-Object System.Data.DataTable
        $null = $adapter.Fill($table)
        return $table
    } finally {
        $connection.Dispose()
    }
}

function Get-ReadinessSqlFirstRow {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Result)
    if ($Result -is [System.Data.DataTable]) { return $Result.Rows[0] }
    return @($Result)[0]
}

function Test-SqlReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    if (-not $Operations) {
        $Operations = @{ Query = { param($connection,$query) Invoke-ReadinessSqlQuery -ConnectionString $connection -Query $query } }
    }
    $results = New-Object System.Collections.Generic.List[object]
    if ($Parameters.SkipSqlAuthenticationTest) {
        $results.Add((New-ReadinessCheck -CheckId 'SQL.AUTHENTICATION' -Category SQL -Name 'SQL authentication' `
            -Status SKIPPED -Severity HIGH -Summary 'Mandatory SQL authentication was explicitly skipped.' `
            -Evidence 'No SQL connection made.' -Recommendation 'Rerun without -SkipSqlAuthenticationTest for smoke-test readiness.' `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    if ([string]::IsNullOrWhiteSpace($Parameters.SqlServer) -or
        [string]::IsNullOrWhiteSpace($Parameters.DatabaseName)) {
        $results.Add((New-ReadinessCheck -CheckId 'SQL.INPUTS' -Category SQL -Name 'SQL inputs' `
            -Status FAIL -Severity HIGH -Summary 'SQL server or database input is missing.' -Evidence 'No value inferred.' `
            -Recommendation 'Supply -SqlServer and -DatabaseName explicitly.' -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = "$($Parameters.SqlServer),$($Parameters.SqlPort)"
    $builder['Initial Catalog'] = $Parameters.DatabaseName
    $builder['Integrated Security'] = $true
    $builder['Encrypt'] = $true
    $builder['TrustServerCertificate'] = $false
    try {
        $identityResult = & $Operations.Query $builder.ConnectionString 'SELECT DB_NAME() AS DatabaseName, SUSER_SNAME() AS IntegratedIdentity;'
        $identity = Get-ReadinessSqlFirstRow $identityResult
        $results.Add((New-ReadinessCheck -CheckId 'SQL.AUTHENTICATION' -Category SQL -Name 'SQL Integrated Authentication' `
            -Status PASS -Severity INFO -Summary 'Database opened with Windows Integrated Authentication.' `
            -Evidence "Database=$($Parameters.DatabaseName); Identity=$($identity.IntegratedIdentity)" `
            -Recommendation $null -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    } catch {
        $results.Add((New-ReadinessCheck -CheckId 'SQL.AUTHENTICATION' -Category SQL -Name 'SQL Integrated Authentication' `
            -Status FAIL -Severity HIGH -Summary 'Database could not be opened with Windows Integrated Authentication.' `
            -Evidence 'SQL exception text and connection string suppressed.' `
            -Recommendation 'Verify DNS, TLS, database existence, and Windows permissions manually.' `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        $results.Add((New-ReadinessCheck -CheckId 'SQL.SCHEMA' -Category SQL -Name 'WP-005 schema' `
            -Status SKIPPED -Severity INFO -Summary 'Schema validation was skipped because authentication failed.' `
            -Evidence 'Dependency: SQL authentication.' -Recommendation 'Resolve SQL access first.' `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    $schemaQuery = @'
SELECT
  CASE WHEN EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260728093000_WP0071CoreInventoryReliability') THEN 1 ELSE 0 END AS MigrationPresent,
  CASE WHEN OBJECT_ID(N'inventory.WindowsComputerInventory', N'U') IS NOT NULL
        AND OBJECT_ID(N'inventory.WindowsOperatingSystemInventory', N'U') IS NOT NULL
        AND OBJECT_ID(N'inventory.WindowsMemoryInventory', N'U') IS NOT NULL
        AND OBJECT_ID(N'inventory.WindowsProcessorInventory', N'U') IS NOT NULL
        AND OBJECT_ID(N'inventory.WindowsDiskInventory', N'U') IS NOT NULL
        AND OBJECT_ID(N'inventory.WindowsVolumeInventory', N'U') IS NOT NULL
        AND OBJECT_ID(N'inventory.WindowsNetworkAdapterInventory', N'U') IS NOT NULL
        AND OBJECT_ID(N'inventory.WindowsIpv4AddressInventory', N'U') IS NOT NULL THEN 1 ELSE 0 END AS TablesPresent;
'@
    try {
        $schemaResult = & $Operations.Query $builder.ConnectionString $schemaQuery
        $schema = Get-ReadinessSqlFirstRow $schemaResult
        $schemaOk = [int]$schema.MigrationPresent -eq 1 -and
            [int]$schema.TablesPresent -eq 1
        $results.Add((New-ReadinessCheck -CheckId 'SQL.SCHEMA' -Category SQL -Name 'WP-007.1 schema and migration' `
            -Status $(if ($schemaOk) {'PASS'} else {'FAIL'}) -Severity $(if ($schemaOk) {'INFO'} else {'CRITICAL'}) `
            -Summary $(if ($schemaOk) {'Expected WP-007.1 migration and core inventory tables are present.'} else {'Expected WP-007.1 migration or required core inventory tables are missing.'}) `
            -Evidence "MigrationPresent=$($schema.MigrationPresent); TablesPresent=$($schema.TablesPresent)" `
            -Recommendation $(if ($schemaOk) {$null} else {'Review the controlled migration plan; this framework will not apply migrations.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        $permissionQuery = @'
SELECT
  HAS_PERMS_BY_NAME(N'inventory.WindowsComputerInventory', N'OBJECT', N'SELECT') AS CanSelect,
  HAS_PERMS_BY_NAME(N'inventory.WindowsComputerInventory', N'OBJECT', N'INSERT') AS CanInsert,
  HAS_PERMS_BY_NAME(N'inventory.WindowsComputerInventory', N'OBJECT', N'UPDATE') AS CanUpdate,
  HAS_PERMS_BY_NAME(N'inventory.WindowsProcessorInventory', N'OBJECT', N'DELETE') AS CanDelete;
'@
        $permissionResult = & $Operations.Query $builder.ConnectionString $permissionQuery
        $permissions = Get-ReadinessSqlFirstRow $permissionResult
        $readOk = [int]$permissions.CanSelect -eq 1
        $writeMetadataOk = [int]$permissions.CanInsert -eq 1 -and
            [int]$permissions.CanUpdate -eq 1 -and
            [int]$permissions.CanDelete -eq 1
        $results.Add((New-ReadinessCheck -CheckId 'SQL.PERMISSION.READ' -Category SQL -Name 'Inventory read permission' `
            -Status $(if ($readOk) {'PASS'} else {'FAIL'}) -Severity $(if ($readOk) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($readOk) {'Required read permission is effective.'} else {'Required read permission is missing.'}) `
            -Evidence "HAS_PERMS_BY_NAME SELECT=$([int]$readOk)" -Recommendation $(if ($readOk) {$null} else {'Have the database owner grant the approved least-privilege read permission.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        $results.Add((New-ReadinessCheck -CheckId 'SQL.PERMISSION.WRITE.METADATA' -Category SQL -Name 'Collector write permission metadata' `
            -Status $(if ($writeMetadataOk) {'PASS'} else {'FAIL'}) -Severity $(if ($writeMetadataOk) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($writeMetadataOk) {'Required collector write capabilities are reported by permission metadata.'} else {'One or more collector write capabilities are absent in permission metadata.'}) `
            -Evidence "INSERT=$([int][bool]$permissions.CanInsert); UPDATE=$([int][bool]$permissions.CanUpdate); DELETE=$([int][bool]$permissions.CanDelete)" `
            -Recommendation $(if ($writeMetadataOk) {$null} else {'Have the database owner review the approved least-privilege collector permissions.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    } catch {
        $results.Add((New-InternalErrorCheck -CheckId 'SQL.INTERNAL.ERROR' -Category SQL -Name 'SQL metadata checks'))
    }
    $results.ToArray()
}
