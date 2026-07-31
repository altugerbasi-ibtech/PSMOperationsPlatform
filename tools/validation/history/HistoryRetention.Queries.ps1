#requires -Version 5.1
Set-StrictMode -Version Latest

function Get-HistoryRetentionQueries {
    @{
        Migration=@'
SELECT TOP (1) [MigrationId]
FROM [dbo].[__EFMigrationsHistory]
WHERE [MigrationId] = N'20260729191745_WP0088ExecutionHistory';
'@
        Tables=@'
SELECT s.[name] AS [SchemaName], t.[name] AS [TableName]
FROM [sys].[tables] AS t
INNER JOIN [sys].[schemas] AS s ON s.[schema_id] = t.[schema_id]
WHERE s.[name] IN (N'history', N'audit')
ORDER BY s.[name], t.[name];
'@
        Shape=@'
SELECT t.[name] AS [TableName], c.[name] AS [ColumnName], ty.[name] AS [TypeName],
       c.[max_length], c.[is_nullable], c.[is_identity]
FROM [sys].[tables] AS t
INNER JOIN [sys].[schemas] AS s ON s.[schema_id] = t.[schema_id]
INNER JOIN [sys].[columns] AS c ON c.[object_id] = t.[object_id]
INNER JOIN [sys].[types] AS ty ON ty.[user_type_id] = c.[user_type_id]
WHERE s.[name] = N'history'
ORDER BY t.[name], c.[column_id];
'@
        Indexes=@'
SELECT t.[name] AS [TableName], i.[name] AS [IndexName], i.[is_unique], i.[is_primary_key]
FROM [sys].[tables] AS t
INNER JOIN [sys].[schemas] AS s ON s.[schema_id] = t.[schema_id]
INNER JOIN [sys].[indexes] AS i ON i.[object_id] = t.[object_id]
WHERE s.[name] = N'history' AND i.[index_id] > 0
ORDER BY t.[name], i.[name];
'@
    }
}

function Assert-HistoryRetentionQueriesReadOnly {
    param([Parameter(Mandatory)][hashtable]$Queries)
    foreach($entry in $Queries.GetEnumerator()){
        if($entry.Value -notmatch '^\s*SELECT\b'){throw "History query $($entry.Key) must begin with SELECT."}
        if($entry.Value -match '(?i)\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|DENY|REVOKE|RECONFIGURE|BACKUP|RESTORE|KILL|EXEC(?:UTE)?)\b'){throw "History query $($entry.Key) is not read-only."}
    }
    $true
}
