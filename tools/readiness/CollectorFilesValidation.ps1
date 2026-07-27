Set-StrictMode -Version Latest

function Test-CollectorFilesReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    if (-not $Operations) {
        $Operations = @{
            TestPath = { param($path, $type) Test-Path -LiteralPath $path -PathType $type }
            GetVersion = { param($path) [Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion }
        }
    }
    $results = New-Object System.Collections.Generic.List[object]
    $rootExists = & $Operations.TestPath $Parameters.CollectorInstallPath 'Container'
    $results.Add((New-ReadinessCheck -CheckId 'FILES.INSTALLPATH' -Category CollectorFiles -Name 'Collector install path' `
        -Status $(if ($rootExists) {'PASS'} else {'FAIL'}) -Severity $(if ($rootExists) {'INFO'} else {'HIGH'}) `
        -Summary $(if ($rootExists) {'Collector install path exists.'} else {'Collector install path does not exist.'}) `
        -Evidence $Parameters.CollectorInstallPath -Recommendation $(if ($rootExists) {$null} else {'Provide the existing deployed collector directory.'}) `
        -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    if (-not $rootExists) { return $results.ToArray() }
    $required = @(
        'PSMOperationsPlatform.WindowsCollector.exe',
        'PSMOperationsPlatform.WindowsCollector.dll',
        'PSMOperationsPlatform.WindowsCollector.deps.json'
    )
    foreach ($file in $required) {
        $path = Join-Path $Parameters.CollectorInstallPath $file
        $exists = & $Operations.TestPath $path 'Leaf'
        $id = 'FILES.REQUIRED.' + ($file.Split('.')[1]).ToUpperInvariant()
        $results.Add((New-ReadinessCheck -CheckId $id -Category CollectorFiles -Name "Required file $file" `
            -Status $(if ($exists) {'PASS'} else {'FAIL'}) -Severity $(if ($exists) {'INFO'} else {'HIGH'}) `
            -Summary $(if ($exists) {'Required collector file exists.'} else {'Required collector file is missing.'}) `
            -Evidence $file -Recommendation $(if ($exists) {$null} else {'Redeploy the approved collector artifact through the controlled deployment process.'}) `
            -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
    }
    $exe = Join-Path $Parameters.CollectorInstallPath 'PSMOperationsPlatform.WindowsCollector.exe'
    try { $version = & $Operations.GetVersion $exe } catch { $version = $null }
    $results.Add((New-ReadinessCheck -CheckId 'FILES.VERSION' -Category CollectorFiles -Name 'Collector version' `
        -Status $(if ($version) {'PASS'} else {'WARNING'}) -Severity $(if ($version) {'INFO'} else {'LOW'}) `
        -Summary $(if ($version) {'Collector file version is available.'} else {'Collector file version is unavailable.'}) `
        -Evidence $version -Recommendation $(if ($version) {$null} else {'Verify the deployed artifact provenance manually.'}) `
        -IsBlocking $false -IsMandatory $false -DurationMilliseconds 0))
    $results.ToArray()
}
