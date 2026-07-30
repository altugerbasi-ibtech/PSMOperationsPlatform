#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._-]+\$?$')][string]$Identity
)
. (Join-Path $PSScriptRoot 'Verification.Common.ps1')
$diagnostics=New-Object System.Collections.Generic.List[object]
try{
    Import-Module ActiveDirectory -ErrorAction Stop
    $account=Get-ADServiceAccount -Identity $Identity -Properties Enabled -ErrorAction Stop
    $diagnostics.Add((New-PSMVerificationDiagnostic 'GMSA.EXISTS' 'PASS' `
        'The gMSA exists and is visible.' "SamAccountName=$($account.SamAccountName)"))
    $diagnostics.Add((New-PSMVerificationDiagnostic 'GMSA.ENABLED' `
        $(if($account.Enabled){'PASS'}else{'FAIL'}) `
        $(if($account.Enabled){'The gMSA is enabled.'}else{'The gMSA is disabled.'}) `
        "Enabled=$([bool]$account.Enabled)"))
    $ready=Test-ADServiceAccount -Identity $Identity -ErrorAction Stop
    $diagnostics.Add((New-PSMVerificationDiagnostic 'GMSA.LOCAL_READINESS' `
        $(if($ready){'PASS'}else{'FAIL'}) `
        $(if($ready){'The current host can use the gMSA.'}else{'The current host cannot use the gMSA.'}) `
        "Ready=$([bool]$ready)"))
}catch{
    $diagnostics.Add((New-PSMVerificationDiagnostic 'GMSA.VALIDATION' 'FAIL' `
        'Read-only gMSA validation failed.' `
        "Identity=$Identity; ErrorType=$($_.Exception.GetType().Name)"))
}
Complete-PSMVerification gMSA $Identity $diagnostics.ToArray()
