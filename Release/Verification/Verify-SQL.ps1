#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Server,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_.-]+$')][string]$Database,
    [ValidateRange(1,65535)][int]$Port=1433,
    [string]$ExpectedMigration
)
. (Join-Path $PSScriptRoot 'Verification.Common.ps1')
$diagnostics=New-Object System.Collections.Generic.List[object]
$connection=$null
try{
    $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source']="$Server,$Port"
    $builder['Initial Catalog']=$Database
    $builder['Integrated Security']=$true
    $builder['Encrypt']=$true
    $builder['TrustServerCertificate']=$false
    $builder['Connect Timeout']=15
    $connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    $connection.Open()
    $diagnostics.Add((New-PSMVerificationDiagnostic 'SQL.CONNECT' 'PASS' `
        'Integrated SQL connection succeeded.' "Database=$Database; Port=$Port"))
    $command=$connection.CreateCommand()
    $command.CommandText=@'
SELECT DB_NAME() AS DatabaseName,
       d.state_desc AS DatabaseState,
       d.compatibility_level AS CompatibilityLevel,
       d.recovery_model_desc AS RecoveryModel,
       d.collation_name AS Collation,
       (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory) AS LatestMigration
FROM sys.databases d
WHERE d.database_id=DB_ID();
'@
    $reader=$command.ExecuteReader()
    if(-not $reader.Read()){throw [InvalidOperationException]::new('Metadata row missing.')}
    $actualDatabase=[string]$reader['DatabaseName']
    $state=[string]$reader['DatabaseState']
    $latest=if($reader['LatestMigration'] -eq [DBNull]::Value){$null}else{[string]$reader['LatestMigration']}
    $diagnostics.Add((New-PSMVerificationDiagnostic 'SQL.DATABASE' `
        $(if($actualDatabase -eq $Database -and $state -eq 'ONLINE'){'PASS'}else{'FAIL'}) `
        $(if($actualDatabase -eq $Database -and $state -eq 'ONLINE'){'Expected database is online.'}else{'Database name or state is unexpected.'}) `
        "Database=$actualDatabase; State=$state; Compatibility=$($reader['CompatibilityLevel']); Recovery=$($reader['RecoveryModel']); Collation=$($reader['Collation'])"))
    if($ExpectedMigration){
        $diagnostics.Add((New-PSMVerificationDiagnostic 'SQL.MIGRATION' `
            $(if($latest -eq $ExpectedMigration){'PASS'}else{'FAIL'}) `
            $(if($latest -eq $ExpectedMigration){'Expected schema migration is current.'}else{'Expected schema migration is not current.'}) `
            "Expected=$ExpectedMigration; Actual=$latest"))
    }else{
        $diagnostics.Add((New-PSMVerificationDiagnostic 'SQL.MIGRATION' 'INFO' `
            'Latest schema migration was observed without an expected value.' `
            "Actual=$latest"))
    }
    $reader.Dispose()
}catch{
    $diagnostics.Add((New-PSMVerificationDiagnostic 'SQL.VALIDATION' 'FAIL' `
        'Read-only SQL verification failed.' `
        "Database=$Database; Port=$Port; ErrorType=$($_.Exception.GetType().Name)"))
}finally{
    if($connection){$connection.Dispose()}
}
Complete-PSMVerification SQL "$Server`:$Port/$Database" $diagnostics.ToArray()
