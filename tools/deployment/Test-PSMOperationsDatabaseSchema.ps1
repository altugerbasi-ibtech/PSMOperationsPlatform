#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][Security.SecureString]$OperationsDatabaseConnectionString,
    [Parameter(Mandatory)][string]$SqlServer,
    [Parameter(Mandatory)][string]$DatabaseName,
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$RepositoryRoot,
    [Parameter(DontShow)][hashtable]$Operations
)
. (Join-Path $PSScriptRoot 'PSMOperationsDatabaseValidation.Common.ps1')
function ConvertFrom-PSMSecureString {
    param([Parameter(Mandatory)][Security.SecureString]$Value)
    $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}
if(-not $RepositoryRoot){$RepositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path}
if(-not $Operations){$Operations=Get-PSMOperationsDatabaseValidationOperations}
$plain=$null
try{
    $plain=ConvertFrom-PSMSecureString $OperationsDatabaseConnectionString
    $parameters=@{OperationsDatabaseConnectionString=$plain;SqlServer=$SqlServer;DatabaseName=$DatabaseName}
    $requirements=Get-PSMOperationsDatabaseRequirements $RepositoryRoot
    $result=Test-PSMOperationsDatabaseSchemaCore $parameters $Operations $requirements
    $paths=Write-PSMOperationsDatabaseValidationReports $result $ReportPath $Operations
    $result|Add-Member Reports $paths
    $result
    if($MyInvocation.InvocationName -ne '.'){exit $result.ExitCode}
}finally{$plain=$null}
