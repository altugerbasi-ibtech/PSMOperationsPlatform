#requires -Version 5.1
Set-StrictMode -Version Latest

function Read-ProductionEvidence {
    param([Parameter(Mandatory)][string]$EvidenceRoot,[Parameter(Mandatory)][string]$RelativePath,[string]$ReleaseVersion,[string]$GitCommit,[string]$ConfigurationHash)
    $path=Join-Path $EvidenceRoot $RelativePath
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)){return [pscustomobject]@{Present=$false;Valid=$false;Path=$RelativePath;Reason='EvidenceMissing';Data=$null}}
    try{$data=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json}catch{return [pscustomobject]@{Present=$true;Valid=$false;Path=$RelativePath;Reason='EvidenceInvalidJson';Data=$null}}
    foreach($name in @('ReleaseVersion','GitCommit','ConfigurationHash','Timestamp','OverallResult')){if(-not $data.PSObject.Properties[$name]){return [pscustomobject]@{Present=$true;Valid=$false;Path=$RelativePath;Reason="EvidenceFieldMissing:$name";Data=$data}}}
    $aligned=$data.ReleaseVersion -eq $ReleaseVersion -and $data.GitCommit -eq $GitCommit -and $data.ConfigurationHash -eq $ConfigurationHash
    try{$timestamp=[datetime]$data.Timestamp;$timestampOk=$true}catch{$timestampOk=$false}
    [pscustomobject]@{Present=$true;Valid=($aligned -and $timestampOk);Path=$RelativePath;Reason=$(if(-not $aligned){'EvidenceAlignmentMismatch'}elseif(-not $timestampOk){'EvidenceTimestampInvalid'}else{'EvidenceValid'});Data=$data}
}

function Test-ProductionEvidenceRoot {
    param([string]$EvidenceRoot,[string]$RepositoryRoot)
    if(-not (Test-Path -LiteralPath $EvidenceRoot -PathType Container)){throw 'EvidenceRoot must be an existing explicit directory.'}
    $resolved=(Resolve-Path -LiteralPath $EvidenceRoot).Path;$repository=(Resolve-Path -LiteralPath $RepositoryRoot).Path
    if([string]::Equals($resolved,$repository,[StringComparison]::OrdinalIgnoreCase)){throw 'EvidenceRoot cannot be the repository root.'}
    $resolved
}
