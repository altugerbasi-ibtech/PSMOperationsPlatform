$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$guidePath=Join-Path $repoRoot 'Release\Database\DeploymentGuide.md'
$queriesPath=Join-Path $repoRoot 'Release\Database\ValidationQueries.sql'

Describe 'WP-009.2 database deployment guide' {
    It 'contains every required DBA topic' {
        $guide=Get-Content -Raw -LiteralPath $guidePath
        @(
            'Supported SQL Server Versions',
            'Required SQL Server Configuration',
            'Required Permissions',
            'Collation',
            'Compatibility Level',
            'Recovery Model',
            'Deployment Sequence',
            'Pre-Deployment Validation',
            'Post-Deployment Validation',
            'Rollback Considerations',
            'Version Verification',
            'Release Artifact Verification',
            'Common Troubleshooting',
            'Best Practices'
        )|ForEach-Object{$guide|Should Match ([regex]::Escape($_))}
    }

    It 'documents the approved SQL baseline and explicit environment values' {
        $guide=Get-Content -Raw -LiteralPath $guidePath
        $guide|Should Match 'SQL Server 2022 or later'
        $guide|Should Match 'compatibility level `160`'
        $guide|Should Match 'environment collation'
        $guide|Should Match 'FULL'
        $guide|Should Match 'SIMPLE'
        $guide|Should Match 'BULK_LOGGED'
    }

    It 'preserves DBA ownership and prohibits automatic runtime migration' {
        $guide=Get-Content -Raw -LiteralPath $guidePath
        $guide|Should Match 'authorized DBA'
        $guide|Should Match 'must not\s+receive DDL permission'
        $guide|Should Match 'Never enable application-startup migration'
        $guide|Should Match 'Database\.Migrate\(\)'
    }

    It 'documents version and checksum verification' {
        $guide=Get-Content -Raw -LiteralPath $guidePath
        $guide|Should Match 'Manifest\.json'
        $guide|Should Match 'Checksums\.sha256'
        $guide|Should Match 'Get-FileHash'
        $guide|Should Match 'dbo\.__EFMigrationsHistory'
    }

    It 'requires sqlcmd quoted identifier mode for deployment' {
        $guide=Get-Content -Raw -LiteralPath $guidePath
        $guide|Should Match '(?s)sqlcmd -S.+?-E -I -b -V 16'
    }
}

Describe 'WP-009.2 validation queries' {
    It 'contains configuration version object and permission queries' {
        $sql=Get-Content -Raw -LiteralPath $queriesPath
        $sql|Should Match 'SERVERPROPERTY'
        $sql|Should Match 'sys\.databases'
        $sql|Should Match 'dbo\.__EFMigrationsHistory'
        $sql|Should Match 'sys\.objects'
        $sql|Should Match 'sys\.foreign_keys'
        $sql|Should Match 'sys\.indexes'
        $sql|Should Match 'sys\.fn_my_permissions'
    }

    It 'contains no database mutation or execution statement' {
        $sql=Get-Content -Raw -LiteralPath $queriesPath
        $sql|Should Not Match '(?im)^\s*(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|DENY|REVOKE|EXEC(?:UTE)?|BACKUP|RESTORE|DBCC)\b'
        $sql|Should Not Match '(?i)\bsp_executesql\b|\bOPENROWSET\b|\bOPENDATASOURCE\b'
    }
}
