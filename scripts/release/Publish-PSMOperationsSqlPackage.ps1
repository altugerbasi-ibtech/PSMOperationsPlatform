#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [string]$RepositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$OutputDirectory=(Join-Path $RepositoryRoot 'Release\Database')
)

$builder=Join-Path $PSScriptRoot 'Build-PSMOperationsSqlPackage.ps1'
& $builder -Version $Version -RepositoryRoot $RepositoryRoot -OutputDirectory $OutputDirectory
exit $LASTEXITCODE
