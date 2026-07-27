Set-StrictMode -Version Latest

function Test-IdentityReadiness {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Parameters, [hashtable]$Operations)
    if (-not $Operations) {
        $Operations = @{
            GetAdCommand = { Get-Command Test-ADServiceAccount -ErrorAction SilentlyContinue }
            TestAdAccount = { param($name) Test-ADServiceAccount -Identity $name -ErrorAction Stop }
            GetCurrentIdentity = { [Security.Principal.WindowsIdentity]::GetCurrent().Name }
        }
    }
    $results = New-Object System.Collections.Generic.List[object]
    $account = $Parameters.ExpectedServiceAccount
    $isGmsa = $account -match '\\[^\\]+\$$'
    if (-not $isGmsa) {
        $results.Add((New-ReadinessCheck -CheckId 'IDENTITY.GMSA' -Category Identity -Name 'gMSA validation' `
            -Status NOT_APPLICABLE -Severity INFO -Summary 'Expected identity is not formatted as a gMSA.' `
            -Evidence $account -Recommendation $null -IsBlocking $false -IsMandatory $false -DurationMilliseconds 0))
    } else {
        $command = & $Operations.GetAdCommand
        if (-not $command) {
            $results.Add((New-ReadinessCheck -CheckId 'IDENTITY.GMSA' -Category Identity -Name 'gMSA validation' `
                -Status WARNING -Severity MEDIUM -Summary 'Test-ADServiceAccount is unavailable; gMSA usability was not tested.' `
                -Evidence 'AD PowerShell module unavailable.' `
                -Recommendation 'Have an authorized operator validate the gMSA on the collector host; this tool will not install modules or accounts.' `
                -IsBlocking $false -IsMandatory $true -DurationMilliseconds 0))
        } else {
            try { $valid = [bool](& $Operations.TestAdAccount ($account.Split('\')[-1].TrimEnd('$'))) }
            catch { $valid = $false }
            $results.Add((New-ReadinessCheck -CheckId 'IDENTITY.GMSA' -Category Identity -Name 'gMSA validation' `
                -Status $(if ($valid) {'PASS'} else {'FAIL'}) -Severity $(if ($valid) {'INFO'} else {'HIGH'}) `
                -Summary $(if ($valid) {'gMSA is usable on this host.'} else {'gMSA usability validation failed.'}) `
                -Evidence $account -Recommendation $(if ($valid) {$null} else {'Have an authorized AD administrator correct gMSA host authorization.'}) `
                -IsBlocking $true -IsMandatory $true -DurationMilliseconds 0))
        }
    }
    $current = & $Operations.GetCurrentIdentity
    $matches = [string]::Equals($current, $account, [StringComparison]::OrdinalIgnoreCase)
    $results.Add((New-ReadinessCheck -CheckId 'IDENTITY.EXECUTING' -Category Identity -Name 'Executing identity' `
        -Status $(if ($matches) {'PASS'} else {'WARNING'}) -Severity $(if ($matches) {'INFO'} else {'LOW'}) `
        -Summary $(if ($matches) {'Validation is executing as the expected service identity.'} else {'Validation is not executing as the expected service identity.'}) `
        -Evidence $current -Recommendation $(if ($matches) {$null} else {'For final evidence, run validation under the approved collector identity where operationally supported.'}) `
        -IsBlocking $false -IsMandatory $false -DurationMilliseconds 0))
    $results.Add((New-ReadinessCheck -CheckId 'IDENTITY.LOGONRIGHT' -Category Identity -Name 'Service logon right' `
        -Status WARNING -Severity LOW -Summary 'Service-logon right was not inferred from unreliable local policy parsing.' `
        -Evidence 'Manual verification required.' `
        -Recommendation 'Have the host owner verify SeServiceLogonRight using an approved read-only administrative method.' `
        -IsBlocking $false -IsMandatory $false -DurationMilliseconds 0))
    $results.ToArray()
}
