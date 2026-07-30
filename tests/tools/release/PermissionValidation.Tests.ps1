$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$sqlPath=Join-Path $repoRoot 'Release\Database\PermissionValidation.sql'
$guidePath=Join-Path $repoRoot 'Release\Database\PermissionValidation.md'

Describe 'WP-009.4 database permission validation' {
    BeforeAll {
        $sql=Get-Content -Raw -LiteralPath $sqlPath
        $guide=Get-Content -Raw -LiteralPath $guidePath
    }

    It 'requires all three environment-supplied principals' {
        foreach($principal in @('CollectorPrincipal','PortalPrincipal','SqlCollectorPrincipal')){
            $sql|Should Match ([regex]::Escape(":setvar $principal `"__REQUIRED__`""))
            $guide|Should Match ([regex]::Escape($principal))
        }
        foreach($role in @('Collector','Portal','SqlCollector')){
            $sql|Should Match ([regex]::Escape("N'$role'"))
        }
    }

    It 'uses HAS_PERMS_BY_NAME for every requested permission' {
        $sql|Should Match 'HAS_PERMS_BY_NAME'
        foreach($permission in @(
            'CONNECT','SELECT','INSERT','UPDATE','DELETE','EXECUTE',
            'VIEW DATABASE STATE')){
            $sql|Should Match ([regex]::Escape("N'$permission'"))
        }
        $sql|Should Match "N'SCHEMA'"
    }

    It 'uses bounded session impersonation for each principal' {
        ([regex]::Matches($sql,'EXECUTE AS USER=')).Count|Should Be 3
        ([regex]::Matches($sql,'(?m)^\s*REVERT;')).Count|Should Be 3
    }

    It 'returns overall pass or fail with detailed diagnostics' {
        $sql|Should Match "THEN N'FAIL' ELSE N'PASS' END AS OverallResult"
        foreach($field in @(
            'PrincipalRole','PrincipalName','Securable','PermissionName',
            'ExpectedValue','ActualValue','Diagnostic')){
            $sql|Should Match ([regex]::Escape($field))
        }
        $sql|Should Match 'ORDER BY PrincipalRole'
    }

    It 'contains no data schema principal or permission mutation' {
        $sql|Should Not Match '(?im)^\s*(CREATE|ALTER|DROP|TRUNCATE|MERGE|UPDATE|DELETE|GRANT|DENY|REVOKE)\b'
        $sql|Should Not Match '(?im)^\s*INSERT\s+(?!@)'
        $sql|Should Not Match '(?i)\bsp_executesql\b|\bOPENROWSET\b|\bOPENDATASOURCE\b'
    }

    It 'documents profiles execution interpretation and remediation boundaries' {
        foreach($term in @(
            'Collector','Portal','SQL Collector','CONNECT','SELECT','INSERT',
            'UPDATE','DELETE','EXECUTE','VIEW DATABASE STATE',
            'PASS','FAIL','EXECUTE AS USER','REVERT','does not prescribe or execute grants')){
            $guide|Should Match ([regex]::Escape($term))
        }
    }
}
