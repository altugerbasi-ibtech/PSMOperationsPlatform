#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [string]$RepositoryRoot=$PSScriptRoot
)

. (Join-Path $PSScriptRoot 'scripts\release\ReleaseBundle.Common.ps1')

try{
    $result=Invoke-PSMOperationsReleaseBundleBuild `
        -Version $Version `
        -RepositoryRoot $RepositoryRoot
    Write-Host "Release bundle created: $($result.ReleaseRoot)"
    Write-Host "Manifest: $($result.ManifestPath)"
    Write-Host "Checksums: $($result.ChecksumsPath)"
    exit 0
}catch{
    Write-Error $_.Exception.Message
    exit 1
}
