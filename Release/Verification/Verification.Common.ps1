#requires -Version 5.1
Set-StrictMode -Version Latest

function New-PSMVerificationDiagnostic {
    param(
        [Parameter(Mandatory)][string]$Code,
        [Parameter(Mandatory)][ValidateSet('PASS','FAIL','INFO')][string]$Status,
        [Parameter(Mandatory)][string]$Summary,
        [string]$Evidence
    )
    [pscustomobject][ordered]@{
        Code=$Code
        Status=$Status
        Summary=$Summary
        Evidence=$Evidence
    }
}

function Complete-PSMVerification {
    param(
        [Parameter(Mandatory)][string]$Check,
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][object[]]$Diagnostics
    )
    $status=if(@($Diagnostics|Where-Object{$_.Status -eq 'FAIL'}).Count){'FAIL'}else{'PASS'}
    $result=[pscustomobject][ordered]@{
        Status=$status
        Check=$Check
        Target=$Target
        Diagnostics=@($Diagnostics)
        TimestampUtc=[DateTimeOffset]::UtcNow.ToString('O')
    }
    Write-Output ($result|ConvertTo-Json -Depth 5)
    if($MyInvocation.InvocationName -ne '.'){
        if($status -eq 'PASS'){exit 0}else{exit 1}
    }
}
