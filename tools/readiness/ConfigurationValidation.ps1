Set-StrictMode -Version Latest

function Test-ConfigurationReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    if (-not $Operations) {
        $Operations = @{
            TestPath = { param($path) Test-Path -LiteralPath $path -PathType Leaf }
            GetContent = { param($path) Get-Content -Raw -LiteralPath $path }
            GetEnvironment = { param($name) [Environment]::GetEnvironmentVariable($name) }
        }
    }
    $results = New-Object System.Collections.Generic.List[object]
    $environmentName = & $Operations.GetEnvironment 'DOTNET_ENVIRONMENT'
    if ([string]::IsNullOrWhiteSpace($environmentName)) { $environmentName = 'Production' }
    $files = @(
        (Join-Path $Parameters.CollectorInstallPath 'appsettings.json'),
        (Join-Path $Parameters.CollectorInstallPath "appsettings.$environmentName.json")
    )
    $connectionValues = New-Object System.Collections.Generic.List[string]
    $unsafeMaterial = $false
    foreach ($file in $files) {
        if (& $Operations.TestPath $file) {
            try {
                $raw = & $Operations.GetContent $file
                if ($raw -match '(?i)"(Password|Pwd|AccessToken|Secret|ApiKey)"\s*:') {
                    $unsafeMaterial = $true
                }
                $json = $raw | ConvertFrom-Json
                if ($json.ConnectionStrings -and
                    $json.ConnectionStrings.OperationsDatabase) {
                    $connectionValues.Add([string]$json.ConnectionStrings.OperationsDatabase)
                }
            } catch {
                $results.Add((New-ReadinessCheck -CheckId 'CONFIG.JSON.INVALID' -Category Configuration `
                    -Name 'Configuration JSON' -Status FAIL -Severity HIGH `
                    -Summary 'A collector configuration file is not valid JSON.' -Evidence ([IO.Path]::GetFileName($file)) `
                    -Recommendation 'Correct the configuration through the approved deployment process.' `
                    -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
            }
        }
    }
    $envConnection = & $Operations.GetEnvironment 'PSM__ConnectionStrings__OperationsDatabase'
    if (-not [string]::IsNullOrWhiteSpace($envConnection)) { $connectionValues.Add($envConnection) }
    $hasConnection = $connectionValues.Count -gt 0 -and
        -not [string]::IsNullOrWhiteSpace($connectionValues[$connectionValues.Count - 1])
    $results.Add((New-ReadinessCheck -CheckId 'CONFIG.OPERATIONSDATABASE' -Category Configuration `
        -Name 'OperationsDatabase configuration' -Status $(if ($hasConnection) {'PASS'} else {'FAIL'}) `
        -Severity $(if ($hasConnection) {'INFO'} else {'HIGH'}) `
        -Summary $(if ($hasConnection) {'OperationsDatabase is configured through an authoritative source.'} else {'OperationsDatabase configuration is missing or empty.'}) `
        -Evidence $(if ($hasConnection) {'Value present and redacted.'} else {'No non-empty authoritative value found.'}) `
        -Recommendation $(if ($hasConnection) {$null} else {'Configure ConnectionStrings:OperationsDatabase through the approved PSM__ or JSON source.'}) `
        -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    if ($connectionValues.Count -gt 1) {
        $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SOURCE.CONFLICT' -Category Configuration `
            -Name 'Configuration source precedence' -Status WARNING -Severity MEDIUM `
            -Summary 'OperationsDatabase is present in multiple configuration sources.' `
            -Evidence "$($connectionValues.Count) sources; values redacted." `
            -Recommendation 'Confirm that standard provider precedence selects the intended test database.' `
            -IsBlocking $false -IsMandatory $false -DurationMilliseconds 0))
    }
    if ($hasConnection) {
        try {
            $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($connectionValues[$connectionValues.Count - 1])
            $hasCredentialKey = $connectionValues[$connectionValues.Count - 1] -match '(?i)(User\s*ID|UID|Password|Pwd)\s*='
            $integrated = $builder.IntegratedSecurity -and -not $hasCredentialKey
            $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SQL.AUTHENTICATION' -Category Configuration `
                -Name 'SQL authentication mode' -Status $(if ($integrated) {'PASS'} else {'FAIL'}) `
                -Severity $(if ($integrated) {'INFO'} else {'CRITICAL'}) `
                -Summary $(if ($integrated) {'Windows Integrated Authentication is configured.'} else {'SQL Authentication or explicit SQL credential material is configured.'}) `
                -Evidence $(if ($integrated) {"Server=$($builder.DataSource); Database=$($builder.InitialCatalog); IntegratedSecurity=True; TrustServerCertificate=$($builder.TrustServerCertificate)"} else {'Connection value redacted.'}) `
                -Recommendation $(if ($integrated) {$null} else {'Remove SQL credentials and configure Windows Integrated Authentication.'}) `
                -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        } catch {
            $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SQL.MALFORMED' -Category Configuration `
                -Name 'SQL configuration syntax' -Status FAIL -Severity HIGH `
                -Summary 'OperationsDatabase is malformed.' -Evidence 'Connection value redacted.' `
                -Recommendation 'Correct the named connection string through the approved configuration process.' `
                -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        }
    }
    $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SECRETS.UNSAFE' -Category Configuration `
        -Name 'Unsafe credential material' -Status $(if ($unsafeMaterial) {'FAIL'} else {'PASS'}) `
        -Severity $(if ($unsafeMaterial) {'CRITICAL'} else {'INFO'}) `
        -Summary $(if ($unsafeMaterial) {'Unsafe credential-like keys were found in collector JSON configuration.'} else {'No prohibited credential-like JSON keys were found.'}) `
        -Evidence $(if ($unsafeMaterial) {'Values suppressed.'} else {'Password, token, secret, and API key names absent.'}) `
        -Recommendation $(if ($unsafeMaterial) {'Remove embedded credentials through the approved configuration process.'} else {$null}) `
        -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    if ($Parameters.Mode -eq 'SmokeTest') {
        $complete = -not [string]::IsNullOrWhiteSpace($Parameters.TargetFqdn) -and
            -not [string]::IsNullOrWhiteSpace($Parameters.TransportPolicy) -and
            -not [string]::IsNullOrWhiteSpace($Parameters.SqlServer) -and
            -not [string]::IsNullOrWhiteSpace($Parameters.DatabaseName)
        $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SMOKETEST.INPUTS' -Category Configuration `
            -Name 'Smoke-test inputs' -Status $(if ($complete) {'PASS'} else {'FAIL'}) `
            -Severity $(if ($complete) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($complete) {'All mandatory smoke-test inputs are explicit.'} else {'One or more mandatory smoke-test inputs are missing.'}) `
            -Evidence $(if ($complete) {'Target, transport, SQL server, and database supplied.'} else {'No value was inferred.'}) `
            -Recommendation $(if ($complete) {$null} else {'Supply all mandatory SmokeTest parameters explicitly.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    }
    $results.ToArray()
}
