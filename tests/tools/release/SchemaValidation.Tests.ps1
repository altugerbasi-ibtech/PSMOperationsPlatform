$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$sqlPath=Join-Path $repoRoot 'Release\Database\SchemaValidation.sql'
$guidePath=Join-Path $repoRoot 'Release\Database\SchemaValidation.md'
$expectationPath=Join-Path $repoRoot 'tools\deployment\PSMOperationsDatabaseSchemaExpectation.json'

Describe 'WP-009.3 schema validation package' {
    BeforeAll {
        $sql=Get-Content -Raw -LiteralPath $sqlPath
        $guide=Get-Content -Raw -LiteralPath $guidePath
        $expectation=Get-Content -Raw -LiteralPath $expectationPath|ConvertFrom-Json
    }

    It 'contains the complete overall result and diagnostic contract' {
        $sql|Should Match "THEN N'FAIL' ELSE N'PASS' END AS OverallResult"
        $sql|Should Match 'Category, ObjectName, ExpectedValue, ActualValue, Diagnostic'
        $sql|Should Match 'ORDER BY Category'
    }

    It 'validates database configuration and requires deployment-defined values' {
        $sql|Should Match 'DB_ID\(@ExpectedDatabaseName\)'
        $sql|Should Match 'DB_NAME\(\) <> @ExpectedDatabaseName'
        $sql|Should Match 'compatibility_level'
        $sql|Should Match 'collation_name'
        $sql|Should Match 'recovery_model_desc'
        $sql|Should Match 'ExpectedCollation "__REQUIRED__"'
        $sql|Should Match 'ExpectedRecoveryModel "__REQUIRED__"'
    }

    It 'validates the exact migration contract and latest schema version' {
        $sql|Should Match '__EFMigrationsHistory'
        $sql|Should Match ([regex]::Escape([string]$expectation.latestMigration))
        foreach($migration in @($expectation.expectedMigrations)){
            $sql|Should Match ([regex]::Escape([string]$migration))
        }
        ([regex]::Matches($sql,"(?m)^\([0-9]+,N'20[0-9]+_[^']+'\)[,;]$")).Count|
            Should Be @($expectation.expectedMigrations).Count
    }

    It 'contains every repository-required table' {
        foreach($table in @($expectation.tables)){
            $parts=[string]$table -split '\.'
            $sql|Should Match ([regex]::Escape("(N'$($parts[0])',N'$($parts[1])')"))
        }
        $tableSection=[regex]::Match($sql,
            '(?s)DECLARE @ExpectedTables table.*?INSERT @Diagnostics').Value
        ([regex]::Matches($tableSection,"\((N'[^']+'),(N'[^']+')\)")).Count|
            Should Be @($expectation.tables).Count
    }

    It 'contains all critical indexes foreign keys primary keys and unique constraints' {
        foreach($name in @($expectation.criticalIndexes)+@($expectation.foreignKeys)+
            @($expectation.primaryKeys)+@($expectation.uniqueConstraints)){
            $sql|Should Match ([regex]::Escape([string]$name))
        }
        $sql|Should Match 'sys\.indexes'
        $sql|Should Match 'sys\.foreign_keys'
        $sql|Should Match 'sys\.key_constraints'
        $sql|Should Match "k\.type=N'PK'"
        $sql|Should Match "k\.type=N'UQ'"
    }

    It 'validates the four persistent default constraints by table and column' {
        $sql|Should Match 'sys\.default_constraints'
        foreach($column in @('ConsecutiveInventoryFailures','InventoryVersion','Id','ModuleKey')){
            $sql|Should Match ([regex]::Escape("N'$column'"))
        }
        $defaultSection=[regex]::Match($sql,
            '(?s)DECLARE @ExpectedDefaults table.*?INSERT @Diagnostics').Value
        ([regex]::Matches($defaultSection,
            "\(N'(?:configuration|inventory)',N'(?:ManagedServer|WindowsMemoryInventory)',N'[^']+'\)")).Count|
            Should Be 4
    }

    It 'is read-only for target database objects' {
        $sql|Should Not Match '(?im)^\s*(CREATE|ALTER|DROP|TRUNCATE|MERGE|UPDATE|DELETE|GRANT|DENY|REVOKE)\b'
        $sql|Should Not Match '(?im)^\s*INSERT\s+(?!@)'
        $sql|Should Not Match '(?i)\bEXEC(?:UTE)?\b|\bDatabase\.Migrate\b|\bdatabase\s+update\b'
        $sql|Should Not Match '(?i)\bBEGIN\s+TRAN(?:SACTION)?\b|\bCOMMIT\b|\bROLLBACK\b'
    }

    It 'documents execution permissions variables results and limitations' {
        foreach($term in @('SQL Server 2022','Windows Integrated Authentication',
            'ExpectedDatabaseName','ExpectedCollation','ExpectedRecoveryModel',
            'PASS','FAIL','read-only','WP-007.Z')){
            $guide|Should Match ([regex]::Escape($term))
        }
        $guide|Should Not Match '(?i)password\s*[:=]|User ID\s*='
    }
}
