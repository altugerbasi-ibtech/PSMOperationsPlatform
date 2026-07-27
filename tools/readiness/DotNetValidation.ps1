Set-StrictMode -Version Latest

function Test-DotNetReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    if (-not $Operations) {
        $Operations = @{
            GetCommand = { Get-Command dotnet -ErrorAction SilentlyContinue }
            GetRuntimes = { & dotnet --list-runtimes 2>$null }
            GetInfo = { & dotnet --info 2>$null }
            TestPath = { param($path) Test-Path -LiteralPath $path }
            GetContent = { param($path) Get-Content -Raw -LiteralPath $path }
        }
    }
    $results = New-Object System.Collections.Generic.List[object]
    $runtimeConfig = Join-Path $Parameters.CollectorInstallPath 'PSMOperationsPlatform.WindowsCollector.runtimeconfig.json'
    $isFrameworkDependent = & $Operations.TestPath $runtimeConfig
    $dotnet = & $Operations.GetCommand
    if (-not $dotnet -and $isFrameworkDependent) {
        $results.Add((New-ReadinessCheck -CheckId 'DOTNET.EXECUTABLE' -Category Runtime -Name 'dotnet executable' `
            -Status FAIL -Severity HIGH -Summary 'dotnet executable is unavailable for a framework-dependent deployment.' `
            -Evidence 'Command not found.' -Recommendation 'Install the repository-required .NET 10 runtime through the approved host process.' `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    if (-not $dotnet) {
        $results.Add((New-ReadinessCheck -CheckId 'DOTNET.SELFCONTAINED' -Category Runtime -Name 'Self-contained deployment' `
            -Status PASS -Severity INFO -Summary 'No dotnet host is required because runtimeconfig is absent.' `
            -Evidence 'Deployment classified as self-contained.' -Recommendation $null `
            -IsBlocking $false -IsMandatory $true -DurationMilliseconds 0))
        return $results.ToArray()
    }
    $results.Add((New-ReadinessCheck -CheckId 'DOTNET.EXECUTABLE' -Category Runtime -Name 'dotnet executable' `
        -Status PASS -Severity INFO -Summary 'dotnet executable is available.' -Evidence $dotnet.Source `
        -Recommendation $null -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    try {
        $runtimes = @(& $Operations.GetRuntimes)
        $match = @($runtimes | Where-Object { $_ -match '^Microsoft\.NETCore\.App\s+10\.' })
        $present = $match.Count -gt 0
        $results.Add((New-ReadinessCheck -CheckId 'DOTNET.RUNTIME.REQUIRED' -Category Runtime -Name 'Required .NET runtime' `
            -Status $(if ($present) {'PASS'} else {'FAIL'}) -Severity $(if ($present) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($present) {'.NET 10 Microsoft.NETCore.App runtime is present.'} else {'.NET 10 Microsoft.NETCore.App runtime is missing.'}) `
            -Evidence $(if ($present) {$match -join ', '} else {'Required major version not listed.'}) `
            -Recommendation $(if ($present) {$null} else {'Install the approved .NET 10 runtime before running the collector.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        $info = @(& $Operations.GetInfo) -join [Environment]::NewLine
        $architectureOk = $info -match '(?im)Architecture:\s*x64'
        $results.Add((New-ReadinessCheck -CheckId 'DOTNET.ARCHITECTURE' -Category Runtime -Name 'dotnet architecture' `
            -Status $(if ($architectureOk) {'PASS'} else {'FAIL'}) -Severity $(if ($architectureOk) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($architectureOk) {'64-bit dotnet architecture detected.'} else {'64-bit dotnet architecture was not detected.'}) `
            -Evidence $(if ($architectureOk) {'x64'} else {'Architecture unavailable or not x64.'}) `
            -Recommendation $(if ($architectureOk) {$null} else {'Install/use the approved x64 runtime.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    } catch {
        $results.Add((New-InternalErrorCheck -CheckId 'DOTNET.INTERNAL.ERROR' -Category Runtime -Name 'Runtime checks'))
    }
    $results.ToArray()
}
