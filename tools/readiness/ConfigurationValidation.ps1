Set-StrictMode -Version Latest

function Test-ConfigurationReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    if (-not $Operations) {
        $Operations = @{
            TestPath = { param($path) Test-Path -LiteralPath $path -PathType Leaf }
            GetContent = { param($path) Get-Content -Raw -LiteralPath $path -ErrorAction Stop }
            GetEnvironment = { param($name) [Environment]::GetEnvironmentVariable($name) }
            GetMachineEnvironment = {
                param($name)
                [Environment]::GetEnvironmentVariable(
                    $name, [EnvironmentVariableTarget]::Machine)
            }
        }
    }

    $results = New-Object System.Collections.Generic.List[object]
    $sources = New-Object System.Collections.Generic.List[object]
    $unsafeMaterial = $false
    $environmentName = & $Operations.GetEnvironment 'DOTNET_ENVIRONMENT'
    if ([string]::IsNullOrWhiteSpace($environmentName)) {
        $environmentName = 'Production'
    }
    $fileDefinitions = @(
        [pscustomobject]@{
            CheckId = 'CONFIG.FILE.BASE'
            Provider = 'appsettings.json'
            Path = Join-Path $Parameters.CollectorInstallPath 'appsettings.json'
        },
        [pscustomobject]@{
            CheckId = 'CONFIG.FILE.ENVIRONMENT'
            Provider = "appsettings.$environmentName.json"
            Path = Join-Path $Parameters.CollectorInstallPath "appsettings.$environmentName.json"
        }
    )

    foreach ($definition in $fileDefinitions) {
        $file = $definition.Path
        try {
            $exists = & $Operations.TestPath $file
        } catch {
            $results.Add((New-ReadinessCheck -CheckId $definition.CheckId `
                -Category Configuration -Name 'Configuration file' `
                -Status FAIL -Severity HIGH `
                -Summary 'A configuration file could not be inspected.' `
                -Evidence "File: $file; Status: Access failed; Provider: $($definition.Provider)" `
                -Recommendation 'Verify the deployed file path and read access through the approved deployment process.' `
                -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
            continue
        }

        if (-not $exists) {
            $results.Add((New-ReadinessCheck -CheckId $definition.CheckId `
                -Category Configuration -Name 'Configuration file' `
                -Status NOT_APPLICABLE -Severity INFO `
                -Summary 'An optional configuration file was not found.' `
                -Evidence "File: $file; Status: Not found; Provider: $($definition.Provider)" `
                -Recommendation $null -IsBlocking $false -IsMandatory $false `
                -DurationMilliseconds 0))
            continue
        }

        try {
            $raw = & $Operations.GetContent $file
        } catch {
            $results.Add((New-ReadinessCheck -CheckId $definition.CheckId `
                -Category Configuration -Name 'Configuration file' `
                -Status FAIL -Severity HIGH `
                -Summary 'A configuration file exists but cannot be opened.' `
                -Evidence "File: $file; Status: Unreadable; Provider: $($definition.Provider)" `
                -Recommendation 'Verify file read access through the approved deployment process.' `
                -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
            continue
        }

        if ($raw -match '(?i)"(Password|Pwd|AccessToken|Secret|ApiKey)"\s*:') {
            $unsafeMaterial = $true
        }
        try {
            $json = $raw | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $results.Add((New-ReadinessCheck -CheckId $definition.CheckId `
                -Category Configuration -Name 'Configuration file' `
                -Status FAIL -Severity HIGH `
                -Summary 'A configuration file contains invalid JSON.' `
                -Evidence "File: $file; Status: Invalid JSON; Provider: $($definition.Provider)" `
                -Recommendation 'Correct the JSON through the approved deployment process.' `
                -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
            continue
        }

        $results.Add((New-ReadinessCheck -CheckId $definition.CheckId `
            -Category Configuration -Name 'Configuration file' `
            -Status PASS -Severity INFO `
            -Summary 'A configuration file contains valid JSON.' `
            -Evidence "File: $file; Status: Valid JSON; Provider: $($definition.Provider)" `
            -Recommendation $null -IsBlocking $false -IsMandatory $false `
            -DurationMilliseconds 0))

        $connectionStringsProperty = $json.PSObject.Properties['ConnectionStrings']
        if ($null -ne $connectionStringsProperty -and
            $null -ne $connectionStringsProperty.Value) {
            $operationsDatabaseProperty =
                $connectionStringsProperty.Value.PSObject.Properties['OperationsDatabase']
            if ($null -ne $operationsDatabaseProperty -and
                -not [string]::IsNullOrWhiteSpace(
                    [string]$operationsDatabaseProperty.Value)) {
                $sources.Add([pscustomobject]@{
                    Source = $definition.Provider
                    Provider = 'JSON'
                    Key = 'ConnectionStrings:OperationsDatabase'
                    Value = [string]$operationsDatabaseProperty.Value
                })
            }
        }
    }

    $environmentKey = 'PSM__ConnectionStrings__OperationsDatabase'
    if ($Operations.ContainsKey('GetMachineEnvironment')) {
        $envConnection = & $Operations.GetMachineEnvironment $environmentKey
        $environmentProvider = 'Machine'
    } else {
        $envConnection = & $Operations.GetEnvironment $environmentKey
        $environmentProvider = 'Effective process environment'
    }
    if (-not [string]::IsNullOrWhiteSpace($envConnection)) {
        $sources.Add([pscustomobject]@{
            Source = 'Environment Variable'
            Provider = $environmentProvider
            Key = $environmentKey
            Value = [string]$envConnection
        })
    }

    $hasConnection = $sources.Count -gt 0
    $selectedSource = if ($hasConnection) {
        $sources[$sources.Count - 1]
    } else {
        $null
    }
    $results.Add((New-ReadinessCheck -CheckId 'CONFIG.OPERATIONSDATABASE' `
        -Category Configuration -Name 'OperationsDatabase configuration' `
        -Status $(if ($hasConnection) {'PASS'} else {'FAIL'}) `
        -Severity $(if ($hasConnection) {'INFO'} else {'HIGH'}) `
        -Summary $(if ($hasConnection) {
            "OperationsDatabase is supplied by $($selectedSource.Source)."
        } else {
            'OperationsDatabase configuration is missing or empty.'
        }) `
        -Evidence $(if ($hasConnection) {
            "Configuration Source: $($selectedSource.Source); Provider: $($selectedSource.Provider); Key: $($selectedSource.Key); Value: [REDACTED]"
        } else {
            'No non-empty value was found in the inspected supported providers.'
        }) `
        -Recommendation $(if ($hasConnection) {$null} else {
            'Configure ConnectionStrings:OperationsDatabase through the approved PSM__ or JSON source.'
        }) -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))

    if ($sources.Count -gt 1) {
        $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SOURCE.PRECEDENCE' `
            -Category Configuration -Name 'Configuration source precedence' `
            -Status PASS -Severity INFO `
            -Summary 'Standard configuration precedence selected the authoritative OperationsDatabase source.' `
            -Evidence "$($sources.Count) sources found; selected $($selectedSource.Source) ($($selectedSource.Provider)); values redacted." `
            -Recommendation $null -IsBlocking $false -IsMandatory $false `
            -DurationMilliseconds 0))
    }

    if ($hasConnection) {
        try {
            $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder(
                $selectedSource.Value)
            $hasCredentialKey = $selectedSource.Value -match
                '(?i)(User\s*ID|UID|Password|Pwd)\s*='
            $integrated = $builder.IntegratedSecurity -and -not $hasCredentialKey
            $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SQL.AUTHENTICATION' `
                -Category Configuration -Name 'SQL authentication mode' `
                -Status $(if ($integrated) {'PASS'} else {'FAIL'}) `
                -Severity $(if ($integrated) {'INFO'} else {'CRITICAL'}) `
                -Summary $(if ($integrated) {
                    'Windows Integrated Authentication is configured.'
                } else {
                    'SQL Authentication or explicit SQL credential material is configured.'
                }) -Evidence $(if ($integrated) {
                    "Server=$($builder.DataSource); Database=$($builder.InitialCatalog); IntegratedSecurity=True; TrustServerCertificate=$($builder.TrustServerCertificate)"
                } else {
                    'Connection value redacted.'
                }) -Recommendation $(if ($integrated) {$null} else {
                    'Remove SQL credentials and configure Windows Integrated Authentication.'
                }) -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        } catch {
            $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SQL.MALFORMED' `
                -Category Configuration -Name 'SQL configuration syntax' `
                -Status FAIL -Severity HIGH `
                -Summary 'OperationsDatabase is malformed.' `
                -Evidence 'Connection value redacted.' `
                -Recommendation 'Correct the named connection string through the approved configuration process.' `
                -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        }
    }

    $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SECRETS.UNSAFE' `
        -Category Configuration -Name 'Unsafe credential material' `
        -Status $(if ($unsafeMaterial) {'FAIL'} else {'PASS'}) `
        -Severity $(if ($unsafeMaterial) {'CRITICAL'} else {'INFO'}) `
        -Summary $(if ($unsafeMaterial) {
            'Unsafe credential-like keys were found in collector JSON configuration.'
        } else {
            'No prohibited credential-like JSON keys were found.'
        }) -Evidence $(if ($unsafeMaterial) {
            'Values suppressed.'
        } else {
            'Password, token, secret, and API key names absent.'
        }) -Recommendation $(if ($unsafeMaterial) {
            'Remove embedded credentials through the approved configuration process.'
        } else {$null}) -IsBlocking $true -IsMandatory $true `
        -DurationMilliseconds 0))

    if ($Parameters.Mode -eq 'SmokeTest') {
        $complete = -not [string]::IsNullOrWhiteSpace($Parameters.TargetFqdn) -and
            -not [string]::IsNullOrWhiteSpace($Parameters.TransportPolicy) -and
            -not [string]::IsNullOrWhiteSpace($Parameters.SqlServer) -and
            -not [string]::IsNullOrWhiteSpace($Parameters.DatabaseName)
        $results.Add((New-ReadinessCheck -CheckId 'CONFIG.SMOKETEST.INPUTS' `
            -Category Configuration -Name 'Smoke-test inputs' `
            -Status $(if ($complete) {'PASS'} else {'FAIL'}) `
            -Severity $(if ($complete) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($complete) {
                'All mandatory smoke-test inputs are explicit.'
            } else {
                'One or more mandatory smoke-test inputs are missing.'
            }) -Evidence $(if ($complete) {
                'Target, transport, SQL server, and database supplied.'
            } else {
                'No value was inferred.'
            }) -Recommendation $(if ($complete) {$null} else {
                'Supply all mandatory SmokeTest parameters explicitly.'
            }) -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    }
    $results.ToArray()
}
