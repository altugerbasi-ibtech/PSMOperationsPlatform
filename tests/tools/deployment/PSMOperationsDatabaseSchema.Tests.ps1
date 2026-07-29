$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$toolRoot=Join-Path $repoRoot 'tools\deployment'
. (Join-Path $toolRoot 'PSMOperationsDatabaseValidation.Common.ps1')

function New-DatabaseRequirements {
    [pscustomobject]@{
        MigrationIds=@(
            '20260726133749_InitialCreate',
            '20260727120000_AddManagedServerConnectivityEligibility',
            '20260727160000_AddManagedServerWinRmConfiguration',
            '20260727190000_AddManagedServerConnectivityState',
            '20260727230000_AddWindowsInventoryCurrentState',
            '20260728093000_WP0071CoreInventoryReliability')
        LatestMigrationId='20260728093000_WP0071CoreInventoryReliability'
        Schemas=@('configuration','inventory')
        Tables=@(
            'configuration.ManagedServer','inventory.WindowsComputerInventory',
            'inventory.WindowsOperatingSystemInventory','inventory.WindowsMemoryInventory',
            'inventory.WindowsProcessorInventory','inventory.WindowsDiskInventory',
            'inventory.WindowsVolumeInventory','inventory.WindowsNetworkAdapterInventory',
            'inventory.WindowsIpv4AddressInventory')
        ForeignKeys=@(
            'FK_WindowsComputerInventory_ManagedServer_ManagedServerId',
            'FK_WindowsOperatingSystemInventory_ManagedServer_ManagedServerId',
            'FK_WindowsMemoryInventory_ManagedServer_ManagedServerId',
            'FK_WindowsProcessorInventory_ManagedServer_ManagedServerId',
            'FK_WindowsDiskInventory_ManagedServer_ManagedServerId',
            'FK_WindowsVolumeInventory_ManagedServer_ManagedServerId',
            'FK_WindowsNetworkAdapterInventory_ManagedServer_ManagedServerId',
            'FK_WindowsIpv4AddressInventory_ManagedServer_ManagedServerId',
            'FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId_ManagedServerId')
        Indexes=@(
            'UX_ManagedServer_Fqdn','IX_ManagedServer_Eligibility',
            'IX_ManagedServer_InventoryEligibility',
            'UX_WindowsMemoryInventory_ManagedServer_ModuleKey',
            'UX_WindowsProcessorInventory_ManagedServer_ProcessorKey',
            'UX_WindowsDiskInventory_ManagedServer_StableSourceKey',
            'UX_WindowsVolumeInventory_ManagedServer_StableSourceKey',
            'UX_WindowsNetworkAdapterInventory_ManagedServer_StableSourceKey',
            'UX_WindowsIpv4AddressInventory_ManagedServer_StableSourceKey',
            'IX_WindowsIpv4AddressInventory_NetworkAdapterInventoryId',
            'IX_WindowsIpv4AddressInventory_NetworkAdapterInventoryId_ManagedServerId')
    }
}

function New-DatabaseState {
    $requirements=New-DatabaseRequirements
    @{
        HistoryExists=$true;AppliedMigrations=@($requirements.MigrationIds)
        Schemas=@($requirements.Schemas);Tables=@($requirements.Tables)
        ForeignKeys=@($requirements.ForeignKeys);Indexes=@($requirements.Indexes)
        PermissionMode='Sufficient';ConnectionFailure=$null
        Queries=(New-Object System.Collections.Generic.List[string])
        Reports=@{}
    }
}

function New-DatabaseOperations {
    param($State)
    @{
        OpenConnection={
            param($connectionString)
            if($State.ConnectionFailure){
                $exception=[InvalidOperationException]::new('unsafe-secret-must-not-escape')
                $exception.Data['PSMCode']=$State.ConnectionFailure
                throw $exception
            }
            [pscustomobject]@{Open=$true}
        }
        CloseConnection={param($connection)}
        QueryRows={
            param($connection,$query)
            $State.Queries.Add($query)
            if($query -match 'HistoryExists'){return @([pscustomobject]@{HistoryExists=$State.HistoryExists})}
            if($query -match 'SELECT MigrationId'){return @($State.AppliedMigrations|ForEach-Object{[pscustomobject]@{MigrationId=$_}})}
            if($query -match 'FROM sys\.schemas'){return @($State.Schemas|ForEach-Object{[pscustomobject]@{SchemaName=$_}})}
            if($query -match 'FROM sys\.tables t JOIN sys\.schemas' -and $query -notmatch 'HAS_PERMS'){
                return @($State.Tables|ForEach-Object{[pscustomobject]@{TableName=$_}})
            }
            if($query -match 'FROM sys\.foreign_keys'){return @($State.ForeignKeys|ForEach-Object{[pscustomobject]@{ConstraintName=$_}})}
            if($query -match 'FROM sys\.indexes'){return @($State.Indexes|ForEach-Object{[pscustomobject]@{IndexName=$_}})}
            if($query -match 'HAS_PERMS_BY_NAME'){
                return @($State.Tables|ForEach-Object{
                    $value=if($State.PermissionMode -eq 'Inconclusive'){$null}
                        elseif($State.PermissionMode -eq 'Insufficient'){0}else{1}
                    [pscustomobject]@{TableName=$_;CanSelect=$value;CanInsert=$value
                        CanUpdate=$value;CanDelete=$value}
                })
            }
            throw 'Unexpected database query.'
        }
        WriteText={param($path,$content)$State.Reports[$path]=$content}
    }
}

function Invoke-DatabaseValidation {
    param($State,[string]$ConnectionString='Server=sql.example.test;Database=PSM_Lab;Integrated Security=true')
    $parameters=@{OperationsDatabaseConnectionString=$ConnectionString
        SqlServer='sql.example.test';DatabaseName='PSM_Lab'}
    Test-PSMOperationsDatabaseSchemaCore $parameters (New-DatabaseOperations $State) (New-DatabaseRequirements)
}

Describe 'Operations Database schema validation' {
    It 'accepts a fully migrated database and derives every migration from the repository' {
        $derived=Get-PSMOperationsDatabaseRequirements $repoRoot
        ($derived.MigrationIds -join ',') | Should Be ((New-DatabaseRequirements).MigrationIds -join ',')
        $derived.LatestMigrationId | Should Be '20260728093000_WP0071CoreInventoryReliability'
        $result=Invoke-DatabaseValidation (New-DatabaseState)
        $result.OverallStatus | Should Be READY
        $result.ExitCode | Should Be 0
        $result.MissingMigrationIds.Count | Should Be 0
        $result.MissingTables.Count | Should Be 0
    }

    It 'classifies database missing, connection, authentication, and access failures safely' {
        foreach($case in @(
            @{Code='DATABASE_NOT_FOUND';Check='DATABASE_NOT_FOUND'},
            @{Code='DATABASE_CONNECTION_FAILED';Check='DATABASE_CONNECTION_FAILED'},
            @{Code='DATABASE_NAME_RESOLUTION_FAILED';Check='DATABASE_NAME_RESOLUTION_FAILED'},
            @{Code='DATABASE_AUTHENTICATION_FAILED';Check='DATABASE_AUTHENTICATION_FAILED'},
            @{Code='DATABASE_ACCESS_DENIED';Check='DATABASE_ACCESS_DENIED'})){
            $state=New-DatabaseState;$state.ConnectionFailure=$case.Code
            $result=Invoke-DatabaseValidation $state
            $result.OverallStatus | Should Be NOT_READY
            @($result.Checks|Where-Object CheckId -eq $case.Check).Count | Should Be 1
            ($result|ConvertTo-Json -Depth 8) | Should Not Match 'unsafe-secret|Integrated Security=true'
        }
    }

    It 'rejects SQL Authentication before opening a connection' {
        $state=New-DatabaseState
        $result=Invoke-DatabaseValidation $state 'Server=sql.example.test;Database=PSM_Lab;User ID=x;Password=secret'
        $result.OverallStatus | Should Be NOT_READY
        $state.Queries.Count | Should Be 0
        ($result|ConvertTo-Json -Depth 8) | Should Not Match 'Password=secret'
    }

    It 'requires SQL Server, database, and approved connection configuration' {
        foreach($case in @(
            @{Sql='';Database='PSM_Lab';Connection='x';Check='DATABASE.SQL_SERVER_MISSING'},
            @{Sql='sql.example.test';Database='';Connection='x';Check='DATABASE.NAME_MISSING'},
            @{Sql='sql.example.test';Database='PSM_Lab';Connection='';Check='DATABASE.CONFIGURATION_MISSING'})){
            $state=New-DatabaseState
            $parameters=@{OperationsDatabaseConnectionString=$case.Connection
                SqlServer=$case.Sql;DatabaseName=$case.Database}
            $result=Test-PSMOperationsDatabaseSchemaCore $parameters `
                (New-DatabaseOperations $state) (New-DatabaseRequirements)
            $result.OverallStatus | Should Be NOT_READY
            @($result.Checks|Where-Object CheckId -eq $case.Check).Count | Should Be 1
            $state.Queries.Count | Should Be 0
        }
    }

    It 'reports missing migration history and all required migrations' {
        $state=New-DatabaseState;$state.HistoryExists=$false
        $result=Invoke-DatabaseValidation $state
        ($result.Checks|Where-Object CheckId -eq MIGRATION.HISTORY).Status | Should Be FAIL
        $result.MissingMigrationIds.Count | Should Be 6
        @($result.Checks|Where-Object{$_.CheckId -like 'MIGRATION.REQUIRED.*' -and $_.Status -eq 'FAIL'}).Count | Should Be 6
    }

    It 'reports one and multiple missing migrations individually' {
        foreach($count in @(1,2)){
            $state=New-DatabaseState
            $state.AppliedMigrations=@($state.AppliedMigrations|Select-Object -First (6-$count))
            $result=Invoke-DatabaseValidation $state
            $result.MissingMigrationIds.Count | Should Be $count
            @($result.Checks|Where-Object{$_.CheckId -like 'MIGRATION.REQUIRED.*' -and $_.Status -eq 'FAIL'}).Count | Should Be $count
        }
    }

    It 'reports each missing schema and table independently' {
        $state=New-DatabaseState;$state.Schemas=@()
        $state.Tables=@('inventory.WindowsProcessorInventory')
        $result=Invoke-DatabaseValidation $state
        $result.MissingSchemas.Count | Should Be 2
        $result.MissingTables.Count | Should Be 8
        @($result.Checks|Where-Object{$_.Category -eq 'Tables' -and $_.Status -eq 'FAIL'}).Count | Should Be 8
    }

    It 'reports missing ManagedServer, singular, plural, and IPv4 tables' {
        foreach($table in @(
            'configuration.ManagedServer',
            'inventory.WindowsComputerInventory',
            'inventory.WindowsProcessorInventory',
            'inventory.WindowsIpv4AddressInventory')){
            $state=New-DatabaseState;$state.Tables=@($state.Tables|Where-Object{$_ -ne $table})
            $result=Invoke-DatabaseValidation $state
            $result.MissingTables.Count | Should Be 1
            $result.MissingTables[0] | Should Be $table
        }
    }

    It 'reports every missing foreign key and correctness index individually' {
        $state=New-DatabaseState
        $state.ForeignKeys=@($state.ForeignKeys|Select-Object -Skip 2)
        $state.Indexes=@($state.Indexes|Select-Object -Skip 2)
        $result=Invoke-DatabaseValidation $state
        $result.MissingConstraints.Count | Should Be 2
        $result.MissingIndexes.Count | Should Be 2
        @($result.Checks|Where-Object{$_.Category -eq 'Constraints' -and $_.Status -eq 'FAIL'}).Count | Should Be 4
    }

    It 'fails insufficient runtime permissions and warns when introspection is inconclusive' {
        $state=New-DatabaseState;$state.PermissionMode='Insufficient'
        $failed=Invoke-DatabaseValidation $state
        $failed.OverallStatus | Should Be NOT_READY
        $failed.PermissionValidationStatus | Should Be INSUFFICIENT
        $state=New-DatabaseState;$state.PermissionMode='Inconclusive'
        $warning=Invoke-DatabaseValidation $state
        $warning.OverallStatus | Should Be WARNING
        $warning.ExitCode | Should Be 1
        $warning.PermissionValidationStatus | Should Be INCONCLUSIVE
    }

    It 'uses only read-only SQL and contains no automatic migration path' {
        $state=New-DatabaseState;$null=Invoke-DatabaseValidation $state
        foreach($query in $state.Queries){
            $query.TrimStart() | Should Match '^SELECT\b'
            $query | Should Not Match '(?im)^\s*(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|DBCC|EXEC(?:UTE)?)\b'
        }
        $text=Get-Content -Raw (Join-Path $toolRoot 'PSMOperationsDatabaseValidation.Common.ps1')
        $text | Should Not Match '(?i)\b(Database\.Migrate|dotnet\s+ef\s+database\s+update|MigrationBuilder)\b'
    }

    It 'writes stable secret-free JSON and Markdown reports' {
        $state=New-DatabaseState;$result=Invoke-DatabaseValidation $state
        $paths=Write-PSMOperationsDatabaseValidationReports $result 'C:\evidence\database.json' (New-DatabaseOperations $state)
        $json=$state.Reports[$paths.JsonPath]|ConvertFrom-Json
        $json.SchemaVersion | Should Be '1.0'
        $json.Checks[0].PSObject.Properties.Name -join ',' | Should Be `
            'CheckId,Category,Name,Status,Severity,Summary,Evidence,Recommendation,IsBlocking,IsMandatory,DurationMilliseconds'
        $state.Reports[$paths.MarkdownPath] | Should Match '# PSM Operations Database Schema Validation'
        ($state.Reports.Values -join "`n") | Should Not Match 'Integrated Security=true|Password='
    }
}
