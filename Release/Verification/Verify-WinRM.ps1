#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ComputerName,
    [ValidateRange(1,65535)][int]$Port=5986,
    [switch]$UseHttp
)
. (Join-Path $PSScriptRoot 'Verification.Common.ps1')
$diagnostics=New-Object System.Collections.Generic.List[object]
$protocol=if($UseHttp){'HTTP'}else{'HTTPS'}
try{
    $parameters=@{
        ComputerName=$ComputerName
        Port=$Port
        Authentication='Kerberos'
        ErrorAction='Stop'
    }
    if(-not $UseHttp){$parameters.UseSSL=$true}
    $response=Test-WSMan @parameters
    $diagnostics.Add((New-PSMVerificationDiagnostic 'WINRM.IDENTIFY' 'PASS' `
        'Authenticated WinRM identification succeeded.' `
        "Protocol=$protocol; Port=$Port; ProductVersion=$($response.ProductVersion)"))
}catch{
    $diagnostics.Add((New-PSMVerificationDiagnostic 'WINRM.IDENTIFY' 'FAIL' `
        'Authenticated WinRM identification failed.' `
        "Protocol=$protocol; Port=$Port; ErrorType=$($_.Exception.GetType().Name)"))
}
Complete-PSMVerification WinRM "$ComputerName`:$Port" $diagnostics.ToArray()
