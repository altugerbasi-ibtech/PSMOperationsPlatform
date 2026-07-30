$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$verificationRoot=Join-Path $repoRoot 'Release\Verification'
$requiredScripts=@(
    'Verify-WinRM.ps1',
    'Verify-SPN.ps1',
    'Verify-gMSA.ps1',
    'Verify-Network.ps1',
    'Verify-SQL.ps1'
)

Describe 'WP-009.5 release verification package' {
    It 'contains every required script and guide' {
        foreach($name in $requiredScripts+@('VerificationGuide.md','Verification.Common.ps1')){
            Test-Path -LiteralPath (Join-Path $verificationRoot $name) -PathType Leaf|Should Be $true
        }
    }

    It 'contains syntactically valid PowerShell' {
        foreach($name in $requiredScripts+@('Verification.Common.ps1')){
            $tokens=$null
            $errors=$null
            [void][Management.Automation.Language.Parser]::ParseFile(
                (Join-Path $verificationRoot $name),[ref]$tokens,[ref]$errors)
            @($errors).Count|Should Be 0
        }
    }

    It 'uses the shared structured PASS FAIL diagnostic contract' {
        $common=Get-Content -Raw (Join-Path $verificationRoot 'Verification.Common.ps1')
        $common|Should Match "ValidateSet\('PASS','FAIL','INFO'\)"
        $common|Should Match 'Status=\$status'
        $common|Should Match 'Diagnostics=@\(\$Diagnostics\)'
        $common|Should Match 'ConvertTo-Json'
        $common|Should Match 'exit 0'
        $common|Should Match 'exit 1'
        foreach($name in $requiredScripts){
            $content=Get-Content -Raw (Join-Path $verificationRoot $name)
            $content|Should Match 'Verification\.Common\.ps1'
            $content|Should Match 'Complete-PSMVerification'
            $content|Should Match 'New-PSMVerificationDiagnostic'
        }
    }

    It 'uses only bounded read-only verification commands' {
        (Get-Content -Raw (Join-Path $verificationRoot 'Verify-WinRM.ps1'))|
            Should Match 'Test-WSMan'
        (Get-Content -Raw (Join-Path $verificationRoot 'Verify-SPN.ps1'))|
            Should Match 'setspn\.exe -Q'
        $gmsa=Get-Content -Raw (Join-Path $verificationRoot 'Verify-gMSA.ps1')
        $gmsa|Should Match 'Get-ADServiceAccount'
        $gmsa|Should Match 'Test-ADServiceAccount'
        (Get-Content -Raw (Join-Path $verificationRoot 'Verify-Network.ps1'))|
            Should Match 'Test-NetConnection'
        $sql=Get-Content -Raw (Join-Path $verificationRoot 'Verify-SQL.ps1')
        $sql|Should Match 'Integrated Security'
        $sql|Should Match 'SELECT DB_NAME\(\)'
    }

    It 'contains no environment mutation operation' {
        $all=$requiredScripts|ForEach-Object{
            Get-Content -Raw (Join-Path $verificationRoot $_)
        }
        $content=$all -join "`n"
        $content|Should Not Match '(?i)\b(Start|Stop|Restart)-Service\b'
        $content|Should Not Match '(?i)\bInvoke-Command\b|\bEnter-PSSession\b|\bNew-PSSession\b'
        $content|Should Not Match '(?i)\bEnable-PSRemoting\b|\bDisable-PSRemoting\b'
        $content|Should Not Match '(?i)\bInstall-ADServiceAccount\b|\bSet-ADServiceAccount\b'
        $content|Should Not Match '(?i)setspn(?:\.exe)?\s+-(?:A|S|D|R)\b'
        $content|Should Not Match '(?i)\bDatabase\.Migrate\b|\bdatabase\s+update\b'
        $sql=Get-Content -Raw (Join-Path $verificationRoot 'Verify-SQL.ps1')
        $sql|Should Not Match '(?im)^\s*(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|DENY|REVOKE|EXEC(?:UTE)?)\b'
    }

    It 'documents execution order inputs outputs and remediation boundary' {
        $guide=Get-Content -Raw (Join-Path $verificationRoot 'VerificationGuide.md')
        foreach($term in $requiredScripts+@(
            'Execution Order','Status','PASS','FAIL','Diagnostics','exit',
            'do not deploy','separately approved remediation','WP-007.Z')){
            $guide|Should Match ([regex]::Escape($term))
        }
        $networkIndex=$guide.IndexOf('Verify-Network.ps1')
        $spnIndex=$guide.IndexOf('Verify-SPN.ps1')
        $gmsaIndex=$guide.IndexOf('Verify-gMSA.ps1')
        $winrmIndex=$guide.IndexOf('Verify-WinRM.ps1')
        $sqlIndex=$guide.IndexOf('Verify-SQL.ps1')
        ($networkIndex -lt $spnIndex -and $spnIndex -lt $gmsaIndex -and
         $gmsaIndex -lt $winrmIndex -and $winrmIndex -lt $sqlIndex)|Should Be $true
    }
}
