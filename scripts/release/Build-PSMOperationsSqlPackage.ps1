#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [string]$RepositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$OutputDirectory=(Join-Path $RepositoryRoot 'Release\Database')
)

. (Join-Path $PSScriptRoot 'PSMOperationsSqlPackage.Common.ps1')

try{
    $result=Invoke-PSMOperationsSqlPackageBuild $Version $RepositoryRoot $OutputDirectory
    Write-Host "SQL package created: $($result.SqlPath)"
    Write-Host "SHA256: $($result.Sha256)"
    Write-Host "Manifest: $($result.ManifestPath)"
    exit 0
}catch{
    Write-Error $_.Exception.Message
    exit 1
}
