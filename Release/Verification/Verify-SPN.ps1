#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9-]*$')][string]$ServiceClass,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9.-]+$')][string]$HostName,
    [ValidatePattern('^[A-Za-z0-9._$\\-]+$')][string]$ExpectedAccount
)
. (Join-Path $PSScriptRoot 'Verification.Common.ps1')
$diagnostics=New-Object System.Collections.Generic.List[object]
$spn="$ServiceClass/$HostName"
try{
    $output=@(& setspn.exe -Q $spn 2>&1|ForEach-Object{[string]$_})
    if($LASTEXITCODE -ne 0){
        throw [InvalidOperationException]::new('Lookup failed.')
    }
    $text=$output -join "`n"
    if($text -match 'No such SPN found'){
        $diagnostics.Add((New-PSMVerificationDiagnostic 'SPN.EXISTS' 'FAIL' `
            'The required SPN was not found.' "SPN=$spn"))
    }else{
        $diagnostics.Add((New-PSMVerificationDiagnostic 'SPN.EXISTS' 'PASS' `
            'The required SPN was found.' "SPN=$spn"))
        if($ExpectedAccount){
            $ownerMatch=$text -match [regex]::Escape($ExpectedAccount)
            $diagnostics.Add((New-PSMVerificationDiagnostic 'SPN.OWNER' `
                $(if($ownerMatch){'PASS'}else{'FAIL'}) `
                $(if($ownerMatch){'SPN owner matches the approved account.'}else{'SPN owner does not match the approved account.'}) `
                "ExpectedAccount=$ExpectedAccount"))
        }
    }
}catch{
    $diagnostics.Add((New-PSMVerificationDiagnostic 'SPN.LOOKUP' 'FAIL' `
        'Read-only SPN lookup failed.' "SPN=$spn; ErrorType=$($_.Exception.GetType().Name)"))
}
Complete-PSMVerification SPN $spn $diagnostics.ToArray()
