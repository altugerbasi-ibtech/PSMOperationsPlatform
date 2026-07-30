#requires -Version 5.1
Set-StrictMode -Version Latest

function Test-PSMIntegratedConnectionString {
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [Parameter(Mandatory)][string]$ExpectedSqlServer,
        [Parameter(Mandatory)][string]$ExpectedDatabase
    )
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
    if(-not $builder.IntegratedSecurity -or
        -not [string]::IsNullOrWhiteSpace([string]$builder.UserID) -or
        -not [string]::IsNullOrWhiteSpace([string]$builder.Password)){
        throw 'Only Windows Integrated Authentication is supported.'
    }
    if(-not [string]::Equals($builder.DataSource,$ExpectedSqlServer,[StringComparison]::OrdinalIgnoreCase)){
        throw 'Configured SQL Server does not match the approved server.'
    }
    if(-not [string]::Equals($builder.InitialCatalog,$ExpectedDatabase,[StringComparison]::OrdinalIgnoreCase)){
        throw 'Configured database does not match the approved database.'
    }
    [pscustomobject]@{SqlServer=$builder.DataSource;DatabaseName=$builder.InitialCatalog}
}

function New-PSMDatabaseCheck {
    param(
        [string]$CheckId,[string]$Category,[string]$Name,
        [ValidateSet('PASS','WARNING','FAIL','NOT_APPLICABLE')][string]$Status,
        [ValidateSet('INFO','MEDIUM','HIGH','CRITICAL')][string]$Severity,
        [string]$Summary,[AllowNull()][object]$Evidence,
        [AllowNull()][string]$Recommendation,[bool]$IsBlocking,[bool]$IsMandatory,
        [long]$DurationMilliseconds=0
    )
    [pscustomobject][ordered]@{
        CheckId=$CheckId;Category=$Category;Name=$Name;Status=$Status;Severity=$Severity
        Summary=$Summary;Evidence=$Evidence;Recommendation=$Recommendation
        IsBlocking=$IsBlocking;IsMandatory=$IsMandatory;DurationMilliseconds=$DurationMilliseconds
    }
}

function Get-PSMOperationsDatabaseRequirements {
    param([string]$RepositoryRoot)
    $manifestPath=Join-Path $RepositoryRoot 'tools\deployment\PSMOperationsDatabaseSchemaExpectation.json'
    if(-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)){
        throw 'Database schema expectation manifest is missing.'
    }
    $manifest=Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $migrationRoot=Join-Path $RepositoryRoot 'src\PSMOperationsPlatform.Infrastructure\Persistence\Migrations'
    $discoveredMigrationIds=@(Get-ChildItem -LiteralPath $migrationRoot -Filter '*.cs' -File |
        Where-Object{$_.Name -notlike '*.Designer.cs' -and $_.Name -ne 'OperationsDbContextModelSnapshot.cs'} |
        ForEach-Object{
            $id=[IO.Path]::GetFileNameWithoutExtension($_.Name)
            $text=Get-Content -Raw -LiteralPath $_.FullName
            if($id -match '^\d{14}_.+$' -and $text -match '\bclass\s+\w+\s*:\s*Migration\b'){$id}
        } | Sort-Object)
    if(-not $discoveredMigrationIds.Count){throw 'No repository migrations were found.'}
    $migrationIds=@($manifest.expectedMigrations|ForEach-Object{[string]$_})
    $uniqueMigrationIds=@($migrationIds|Select-Object -Unique)
    if($migrationIds.Count -ne $uniqueMigrationIds.Count){
        throw 'Database schema expectation contains duplicate migration identifiers.'
    }
    if(($migrationIds -join "`n") -ne ((@($migrationIds|Sort-Object)) -join "`n")){
        throw 'Database schema expectation migration ordering is not deterministic.'
    }
    if(($migrationIds -join "`n") -ne ($discoveredMigrationIds -join "`n")){
        throw 'Database schema expectation is stale relative to repository migrations.'
    }
    if([string]$manifest.latestMigration -ne $migrationIds[-1]){
        throw 'Database schema expectation latest migration is inconsistent.'
    }
    [pscustomobject][ordered]@{
        ValidationSchemaVersion=[string]$manifest.validationSchemaVersion
        MigrationPolicy=[string]$manifest.migrationPolicy
        MigrationIds=$migrationIds;LatestMigrationId=[string]$manifest.latestMigration
        Schemas=@($manifest.schemas);Tables=@($manifest.tables)
        PrimaryKeys=@($manifest.primaryKeys);ForeignKeys=@($manifest.foreignKeys)
        UniqueConstraints=@($manifest.uniqueConstraints)
        Indexes=@($manifest.criticalIndexes)
        CheckConstraints=@($manifest.criticalCheckConstraints)
        RuntimePermissionTables=@($manifest.runtimePermissionTables)
    }
}

function Get-PSMOperationsDatabaseValidationOperations {
    @{
        OpenConnection={
            param($connectionString)
            $connection=[System.Data.SqlClient.SqlConnection]::new($connectionString)
            $connection.Open()
            $connection
        }
        CloseConnection={param($connection)if($connection){$connection.Dispose()}}
        QueryRows={
            param($connection,$query)
            $command=$connection.CreateCommand()
            try{
                $command.CommandText=$query
                $reader=$command.ExecuteReader()
                try{
                    $rows=New-Object System.Collections.Generic.List[object]
                    while($reader.Read()){
                        $row=[ordered]@{}
                        for($index=0;$index -lt $reader.FieldCount;$index++){
                            $value=$reader.GetValue($index)
                            $row[$reader.GetName($index)]=if($value -is [DBNull]){$null}else{$value}
                        }
                        $rows.Add([pscustomobject]$row)
                    }
                    $rows.ToArray()
                }finally{$reader.Dispose()}
            }finally{$command.Dispose()}
        }
    }
}

function Get-PSMDatabaseFailureCode {
    param($Exception)
    if($Exception.Data -and $Exception.Data.Contains('PSMCode')){return [string]$Exception.Data['PSMCode']}
    $number=if($Exception.PSObject.Properties['Number']){[int]$Exception.Number}else{0}
    if($number -eq 18456){return 'DATABASE_AUTHENTICATION_FAILED'}
    if($number -in @(4060,911)){return 'DATABASE_NOT_FOUND'}
    if($number -in @(229,916)){return 'DATABASE_ACCESS_DENIED'}
    if($number -eq 11001){return 'DATABASE_NAME_RESOLUTION_FAILED'}
    if($number -in @(-1,2,53)){return 'DATABASE_CONNECTION_FAILED'}
    'DATABASE_VALIDATION_ERROR'
}

function Add-PSMDatabaseRequirementChecks {
    param(
        [System.Collections.Generic.List[object]]$Checks,[string]$Category,
        [string]$Prefix,[string[]]$Required,[string[]]$Actual,[string]$ItemName
    )
    foreach($item in $Required){
        $present=$item -in $Actual
        $safeId=($item -replace '[^A-Za-z0-9]+','_').Trim('_').ToUpperInvariant()
        $Checks.Add((New-PSMDatabaseCheck "$Prefix.$safeId" $Category $item `
            $(if($present){'PASS'}else{'FAIL'}) $(if($present){'INFO'}else{'HIGH'}) `
            $(if($present){"Required $ItemName is present."}else{"Required $ItemName is missing."}) `
            $item $(if($present){$null}else{"Have an authorized DBA apply the approved repository migrations, then rerun validation."}) `
            (-not $present) $true))
    }
}

function Test-PSMOperationsDatabaseSchemaCore {
    param(
        [hashtable]$Parameters,[hashtable]$Operations,
        [pscustomobject]$Requirements
    )
    $checks=New-Object System.Collections.Generic.List[object]
    $connection=$null
    $appliedMigrations=@();$missingMigrations=@();$unexpectedMigrations=@()
    $missingSchemas=@();$missingTables=@();$missingPrimaryKeys=@()
    $missingConstraints=@();$missingUniqueConstraints=@()
    $missingIndexes=@();$missingCheckConstraints=@();$permissionStatus='NOT_CHECKED'
    try{
        if([string]::IsNullOrWhiteSpace([string]$Parameters.SqlServer)){throw 'SQL_SERVER_MISSING'}
        if([string]::IsNullOrWhiteSpace([string]$Parameters.DatabaseName)){throw 'DATABASE_NAME_MISSING'}
        if([string]::IsNullOrWhiteSpace([string]$Parameters.OperationsDatabaseConnectionString)){
            throw 'DATABASE_CONFIGURATION_MISSING'
        }
        try{
            $metadata=Test-PSMIntegratedConnectionString $Parameters.OperationsDatabaseConnectionString `
                $Parameters.SqlServer $Parameters.DatabaseName
        }catch{throw 'DATABASE_CONFIGURATION_INVALID'}
        $checks.Add((New-PSMDatabaseCheck 'DATABASE.CONFIGURATION' 'DatabaseConnection' `
            'Approved database configuration' 'PASS' 'INFO' `
            'Windows Integrated Authentication configuration is valid.' `
            "Server=$($metadata.SqlServer); Database=$($metadata.DatabaseName); IntegratedSecurity=True" `
            $null $false $true))
        try{$connection=& $Operations.OpenConnection $Parameters.OperationsDatabaseConnectionString}
        catch{
            $code=Get-PSMDatabaseFailureCode $_.Exception
            $summary=switch($code){
                'DATABASE_AUTHENTICATION_FAILED' {'Windows Integrated Authentication failed.'}
                'DATABASE_NOT_FOUND' {'The target database does not exist or is unavailable.'}
                'DATABASE_ACCESS_DENIED' {'The executing identity cannot access the target database.'}
                'DATABASE_NAME_RESOLUTION_FAILED' {'SQL Server name resolution failed.'}
                'DATABASE_CONNECTION_FAILED' {'SQL Server name resolution or connection failed.'}
                default {'Database validation encountered an unexpected connection error.'}
            }
            $checks.Add((New-PSMDatabaseCheck $code 'DatabaseConnection' 'SQL connection' `
                'FAIL' 'CRITICAL' $summary 'Sensitive exception details suppressed.' `
                'Verify the approved SQL endpoint, database, Windows identity, and access; do not start the service.' `
                $true $true))
            throw 'STOP_VALIDATION'
        }
        $checks.Add((New-PSMDatabaseCheck 'DATABASE.CONNECTION' 'DatabaseConnection' `
            'SQL connection' 'PASS' 'INFO' 'The target database is accessible using Windows Integrated Authentication.' `
            "Server=$($metadata.SqlServer); Database=$($metadata.DatabaseName)" $null $false $true))

        $history=@(& $Operations.QueryRows $connection @'
SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NULL THEN 0 ELSE 1 END AS HistoryExists;
'@)
        $historyExists=$history.Count -gt 0 -and [bool]$history[0].HistoryExists
        $checks.Add((New-PSMDatabaseCheck 'MIGRATION.HISTORY' 'MigrationHistory' `
            'dbo.__EFMigrationsHistory' $(if($historyExists){'PASS'}else{'FAIL'}) `
            $(if($historyExists){'INFO'}else{'CRITICAL'}) `
            $(if($historyExists){'Migration history exists.'}else{'Migration history is missing.'}) `
            'dbo.__EFMigrationsHistory' $(if($historyExists){$null}else{'Have an authorized DBA apply the approved migrations and rerun validation.'}) `
            (-not $historyExists) $true))
        if($historyExists){
            $appliedMigrations=@(& $Operations.QueryRows $connection @'
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
'@ | ForEach-Object{[string]$_.MigrationId})
            $missingMigrations=@($Requirements.MigrationIds|Where-Object{$_ -notin $appliedMigrations})
            $unexpectedMigrations=@($appliedMigrations|Where-Object{$_ -notin $Requirements.MigrationIds})
            Add-PSMDatabaseRequirementChecks $checks 'MigrationHistory' 'MIGRATION.REQUIRED' `
                $Requirements.MigrationIds $appliedMigrations 'migration'
            Add-PSMDatabaseRequirementChecks $checks 'MigrationHistory' 'MIGRATION.ALLOWED' `
                $appliedMigrations $Requirements.MigrationIds 'approved migration'
        }else{
            $missingMigrations=@($Requirements.MigrationIds)
            Add-PSMDatabaseRequirementChecks $checks 'MigrationHistory' 'MIGRATION.REQUIRED' `
                $Requirements.MigrationIds @() 'migration'
        }

        $schemas=@(& $Operations.QueryRows $connection @'
SELECT name AS SchemaName FROM sys.schemas ORDER BY name;
'@ | ForEach-Object{[string]$_.SchemaName})
        $missingSchemas=@($Requirements.Schemas|Where-Object{$_ -notin $schemas})
        Add-PSMDatabaseRequirementChecks $checks 'Schema' 'SCHEMA.REQUIRED' `
            $Requirements.Schemas $schemas 'schema'

        $tables=@(& $Operations.QueryRows $connection @'
SELECT s.name + N'.' + t.name AS TableName
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
ORDER BY s.name,t.name;
'@ | ForEach-Object{[string]$_.TableName})
        $missingTables=@($Requirements.Tables|Where-Object{$_ -notin $tables})
        Add-PSMDatabaseRequirementChecks $checks 'Tables' 'TABLE.REQUIRED' `
            $Requirements.Tables $tables 'table'

        $primaryKeys=@(& $Operations.QueryRows $connection @'
SELECT name AS ConstraintName FROM sys.key_constraints WHERE type=N'PK' ORDER BY name;
'@ | ForEach-Object{[string]$_.ConstraintName})
        $missingPrimaryKeys=@($Requirements.PrimaryKeys|Where-Object{$_ -notin $primaryKeys})
        Add-PSMDatabaseRequirementChecks $checks 'Constraints' 'PRIMARYKEY.REQUIRED' `
            $Requirements.PrimaryKeys $primaryKeys 'primary key'

        $foreignKeys=@(& $Operations.QueryRows $connection @'
SELECT name AS ConstraintName FROM sys.foreign_keys ORDER BY name;
'@ | ForEach-Object{[string]$_.ConstraintName})
        $missingConstraints=@($Requirements.ForeignKeys|Where-Object{$_ -notin $foreignKeys})
        Add-PSMDatabaseRequirementChecks $checks 'Constraints' 'CONSTRAINT.REQUIRED' `
            $Requirements.ForeignKeys $foreignKeys 'foreign key'

        $uniqueConstraints=@(& $Operations.QueryRows $connection @'
SELECT name AS ConstraintName FROM sys.key_constraints WHERE type=N'UQ' ORDER BY name;
'@ | ForEach-Object{[string]$_.ConstraintName})
        $missingUniqueConstraints=@($Requirements.UniqueConstraints|Where-Object{$_ -notin $uniqueConstraints})
        Add-PSMDatabaseRequirementChecks $checks 'Constraints' 'UNIQUE.REQUIRED' `
            $Requirements.UniqueConstraints $uniqueConstraints 'unique constraint'

        $indexes=@(& $Operations.QueryRows $connection @'
SELECT i.name AS IndexName
FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id
JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE i.name IS NOT NULL
ORDER BY i.name;
'@ | ForEach-Object{[string]$_.IndexName})
        $missingIndexes=@($Requirements.Indexes|Where-Object{$_ -notin $indexes})
        Add-PSMDatabaseRequirementChecks $checks 'Constraints' 'INDEX.REQUIRED' `
            $Requirements.Indexes $indexes 'index'

        $checkConstraints=@(& $Operations.QueryRows $connection @'
SELECT name AS ConstraintName FROM sys.check_constraints ORDER BY name;
'@ | ForEach-Object{[string]$_.ConstraintName})
        $missingCheckConstraints=@($Requirements.CheckConstraints|Where-Object{$_ -notin $checkConstraints})
        Add-PSMDatabaseRequirementChecks $checks 'Constraints' 'CHECK.REQUIRED' `
            $Requirements.CheckConstraints $checkConstraints 'check constraint'

        $permissions=@(& $Operations.QueryRows $connection @'
SELECT N'configuration.ManagedServer' AS TableName,
 HAS_PERMS_BY_NAME(N'configuration.ManagedServer',N'OBJECT',N'SELECT') AS CanSelect,
 CAST(NULL AS int) AS CanInsert,
 HAS_PERMS_BY_NAME(N'configuration.ManagedServer',N'OBJECT',N'UPDATE') AS CanUpdate,
 CAST(NULL AS int) AS CanDelete
UNION ALL
SELECT s.name+N'.'+t.name,
 HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'SELECT'),
 HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'INSERT'),
 HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'UPDATE'),
 HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'DELETE')
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name=N'inventory' AND t.name IN
 (N'WindowsComputerInventory',N'WindowsOperatingSystemInventory',N'WindowsMemoryInventory',
  N'WindowsProcessorInventory',N'WindowsDiskInventory',N'WindowsVolumeInventory',
  N'WindowsNetworkAdapterInventory',N'WindowsIpv4AddressInventory');
'@)
        $permissionFailures=New-Object System.Collections.Generic.List[string]
        $permissionUnknown=New-Object System.Collections.Generic.List[string]
        foreach($table in $Requirements.RuntimePermissionTables){
            $row=$permissions|Where-Object TableName -eq $table|Select-Object -First 1
            $required=if($table -eq 'configuration.ManagedServer'){@('CanSelect','CanUpdate')}else{@('CanSelect','CanInsert','CanUpdate','CanDelete')}
            foreach($name in $required){
                if(-not $row -or $null -eq $row.$name){$permissionUnknown.Add("$table/$name")}
                elseif(-not [bool]$row.$name){$permissionFailures.Add("$table/$name")}
            }
        }
        if($permissionFailures.Count){
            $permissionStatus='INSUFFICIENT'
            $checks.Add((New-PSMDatabaseCheck 'PERMISSION.RUNTIME' 'Permissions' `
                'Collector runtime permissions' 'FAIL' 'CRITICAL' `
                'The executing identity lacks required runtime data permissions.' `
                @($permissionFailures) 'Have the authorized DBA grant the approved least-privilege runtime permissions, then rerun validation.' `
                $true $true))
        }elseif($permissionUnknown.Count){
            $permissionStatus='INCONCLUSIVE'
            $checks.Add((New-PSMDatabaseCheck 'PERMISSION.RUNTIME' 'Permissions' `
                'Collector runtime permissions' 'WARNING' 'MEDIUM' `
                'Runtime permission introspection was inconclusive.' @($permissionUnknown) `
                'Have the authorized DBA verify effective table permissions before approving service start.' `
                $false $true))
        }else{
            $permissionStatus='SUFFICIENT'
            $checks.Add((New-PSMDatabaseCheck 'PERMISSION.RUNTIME' 'Permissions' `
                'Collector runtime permissions' 'PASS' 'INFO' `
                'Effective runtime data permissions are sufficient.' `
                'SELECT/UPDATE on ManagedServer; SELECT/INSERT/UPDATE/DELETE on Windows inventory tables.' `
                $null $false $true))
        }
    }catch{
        if($_.Exception.Message -eq 'SQL_SERVER_MISSING'){
            $checks.Add((New-PSMDatabaseCheck 'DATABASE.SQL_SERVER_MISSING' 'DatabaseConnection' `
                'SQL Server name' 'FAIL' 'CRITICAL' 'SQL Server name is required.' $null `
                'Supply the approved SQL Server name.' $true $true))
        }elseif($_.Exception.Message -eq 'DATABASE_NAME_MISSING'){
            $checks.Add((New-PSMDatabaseCheck 'DATABASE.NAME_MISSING' 'DatabaseConnection' `
                'Database name' 'FAIL' 'CRITICAL' 'Database name is required.' $null `
                'Supply the approved database name.' $true $true))
        }elseif($_.Exception.Message -eq 'DATABASE_CONFIGURATION_MISSING'){
            $checks.Add((New-PSMDatabaseCheck 'DATABASE.CONFIGURATION_MISSING' 'DatabaseConnection' `
                'Database configuration' 'FAIL' 'CRITICAL' `
                'Operations Database configuration is unavailable.' $null `
                'Supply the approved Windows Integrated Authentication configuration through the supported source.' `
                $true $true))
        }elseif($_.Exception.Message -eq 'DATABASE_CONFIGURATION_INVALID'){
            $checks.Add((New-PSMDatabaseCheck 'DATABASE.CONFIGURATION_INVALID' 'DatabaseConnection' `
                'Database configuration' 'FAIL' 'CRITICAL' `
                'Operations Database configuration is invalid or does not use approved Windows Integrated Authentication.' `
                'Connection value redacted.' `
                'Correct the approved configuration without exposing its value, then rerun validation.' `
                $true $true))
        }elseif($_.Exception.Message -ne 'STOP_VALIDATION'){
            $checks.Add((New-PSMDatabaseCheck 'DATABASE.VALIDATION_ERROR' 'DatabaseConnection' `
                'Database validation' 'FAIL' 'CRITICAL' `
                'Database validation failed safely; sensitive exception details were suppressed.' `
                'Sensitive exception details suppressed.' `
                'Review local validation tooling and rerun without starting the service.' $true $true))
        }
    }finally{if($connection){& $Operations.CloseConnection $connection}}
    $failures=@($checks|Where-Object Status -eq 'FAIL')
    $warnings=@($checks|Where-Object Status -eq 'WARNING')
    [pscustomobject][ordered]@{
        SchemaVersion=$Requirements.ValidationSchemaVersion;OverallStatus=if($failures){'NOT_READY'}elseif($warnings){'WARNING'}else{'READY'}
        ExitCode=if($failures){2}elseif($warnings){1}else{0};Checks=$checks.ToArray()
        ExpectedMigrationIds=@($Requirements.MigrationIds);AppliedMigrationIds=@($appliedMigrations)
        MissingMigrationIds=@($missingMigrations);UnexpectedMigrationIds=@($unexpectedMigrations)
        MissingSchemas=@($missingSchemas);MissingTables=@($missingTables)
        MissingPrimaryKeys=@($missingPrimaryKeys);MissingConstraints=@($missingConstraints)
        MissingUniqueConstraints=@($missingUniqueConstraints)
        MissingIndexes=@($missingIndexes);MissingCheckConstraints=@($missingCheckConstraints)
        PermissionValidationStatus=$permissionStatus
    }
}

function ConvertTo-PSMOperationsDatabaseValidationMarkdown {
    param($Result)
    $lines=@('# PSM Operations Database Schema Validation','',
        "Overall: **$($Result.OverallStatus)**","Exit code: $($Result.ExitCode)",'',
        '| Check | Category | Status | Summary |','|---|---|---|---|')
    foreach($check in $Result.Checks){
        $lines+='| {0} | {1} | {2} | {3} |' -f $check.CheckId,$check.Category,$check.Status,($check.Summary-replace '\|','/')
    }
    $lines+=@('','Connection string: **REDACTED / NOT STORED**')
    $lines -join [Environment]::NewLine
}

function Write-PSMOperationsDatabaseValidationReports {
    param($Result,[string]$Path,[hashtable]$Operations)
    $jsonPath=[IO.Path]::ChangeExtension($Path,'.json')
    $markdownPath=[IO.Path]::ChangeExtension($Path,'.md')
    & $Operations.WriteText $jsonPath ($Result|ConvertTo-Json -Depth 10)
    & $Operations.WriteText $markdownPath (ConvertTo-PSMOperationsDatabaseValidationMarkdown $Result)
    [pscustomobject]@{JsonPath=$jsonPath;MarkdownPath=$markdownPath}
}
