#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ComputerName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateRange(1,65535)][int[]]$Port
)
. (Join-Path $PSScriptRoot 'Verification.Common.ps1')
$diagnostics=New-Object System.Collections.Generic.List[object]
foreach($currentPort in @($Port|Sort-Object -Unique)){
    try{
        $result=Test-NetConnection -ComputerName $ComputerName -Port $currentPort `
            -InformationLevel Detailed -WarningAction SilentlyContinue -ErrorAction Stop
        $diagnostics.Add((New-PSMVerificationDiagnostic "NETWORK.TCP.$currentPort" `
            $(if($result.TcpTestSucceeded){'PASS'}else{'FAIL'}) `
            $(if($result.TcpTestSucceeded){'TCP endpoint is reachable.'}else{'TCP endpoint is not reachable.'}) `
            "Port=$currentPort; RemoteAddress=$($result.RemoteAddress); TcpTestSucceeded=$([bool]$result.TcpTestSucceeded)"))
    }catch{
        $diagnostics.Add((New-PSMVerificationDiagnostic "NETWORK.TCP.$currentPort" 'FAIL' `
            'TCP reachability check failed.' `
            "Port=$currentPort; ErrorType=$($_.Exception.GetType().Name)"))
    }
}
Complete-PSMVerification Network $ComputerName $diagnostics.ToArray()
