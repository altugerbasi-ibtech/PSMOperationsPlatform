$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$ratRoot=Join-Path $repoRoot 'Release\Acceptance'
. (Join-Path $ratRoot 'RAT.Common.ps1')

function New-RATTestReport {
    param([Parameter(Mandatory)][ValidateSet('PASS','WARNING','FAIL')][string]$Result)
    New-RATReport `
        -Checks @([pscustomobject]@{Name='Test Check';Result=$Result;Diagnostics='test'}) `
        -ProductVersion '1.0.0' `
        -GitCommit '0123456789abcdef0123456789abcdef01234567' `
        -ExecutionTime ([TimeSpan]::FromSeconds(102)) `
        -ReadOnlyValidation $true
}

Describe 'WP-009.7 production readiness mapping' {
    It 'maps PASS to READY_FOR_PRODUCTION' {
        $report=New-RATTestReport PASS
        $report.OverallResult|Should Be PASS
        $report.ProductionReadinessStatus|Should Be READY_FOR_PRODUCTION
        $report.ProductionReadinessMessage|Should Be 'PSM Release Status: READY FOR PRODUCTION'
        $report.ExitCode|Should Be 0
    }

    It 'maps WARNING to READY_WITH_WARNINGS' {
        $report=New-RATTestReport WARNING
        $report.OverallResult|Should Be WARNING
        $report.ProductionReadinessStatus|Should Be READY_WITH_WARNINGS
        $report.ProductionReadinessMessage|Should Be 'PSM Release Status: READY WITH WARNINGS'
        $report.ExitCode|Should Be 1
    }

    It 'maps FAIL to NOT_READY_FOR_PRODUCTION' {
        $report=New-RATTestReport FAIL
        $report.OverallResult|Should Be FAIL
        $report.ProductionReadinessStatus|Should Be NOT_READY_FOR_PRODUCTION
        $report.ProductionReadinessMessage|Should Be 'PSM Release Status: NOT READY FOR PRODUCTION'
        $report.ProductionReadinessStatus|Should Not Match '^READY'
        $report.ExitCode|Should Be 2
    }

    It 'forces FAIL when mandatory read-only validation is absent' {
        $report=New-RATReport `
            -Checks @([pscustomobject]@{Name='Test Check';Result='PASS'}) `
            -ProductVersion '1.0.0' -GitCommit 'abc' `
            -ExecutionTime ([TimeSpan]::Zero) -ReadOnlyValidation $false
        $report.OverallResult|Should Be FAIL
        $report.ProductionReadinessStatus|Should Be NOT_READY_FOR_PRODUCTION
        @($report.Checks|Where-Object Name -eq 'Read-only Validation').Count|Should Be 1
    }
}

Describe 'WP-009.7 RAT report outputs' {
    It 'includes the decision in required JSON fields' {
        $json=ConvertTo-RATJson (New-RATTestReport PASS)|ConvertFrom-Json
        $json.OverallResult|Should Be PASS
        $json.ProductionReadinessStatus|Should Be READY_FOR_PRODUCTION
        $json.ProductionReadinessMessage|Should Be 'PSM Release Status: READY FOR PRODUCTION'
    }

    It 'places explicit decision text at the top and final summary of HTML' {
        $html=ConvertTo-RATHtml (New-RATTestReport WARNING)
        $firstDecision=$html.IndexOf('PSM Release Status: READY WITH WARNINGS')
        $summary=$html.IndexOf('<h2>Final Summary</h2>')
        $lastDecision=$html.LastIndexOf('PSM Release Status: READY WITH WARNINGS')
        $firstDecision|Should BeGreaterThan -1
        $firstDecision|Should BeLessThan $summary
        $lastDecision|Should BeGreaterThan $summary
        ([regex]::Matches($html,'PSM Release Status: READY WITH WARNINGS')).Count|Should Be 2
    }

    It 'adds the prominent Markdown production decision section' {
        $markdown=ConvertTo-RATMarkdown (New-RATTestReport FAIL)
        $markdown|Should Match '^# Production Readiness Decision'
        $markdown|Should Match '\*\*PSM Release Status: NOT READY FOR PRODUCTION\*\*'
        $markdown|Should Match '## Final Summary'
    }

    It 'prints the required final console decision block' {
        $console=(Write-RATConsole (New-RATTestReport PASS)) -join "`n"
        $console|Should Match '====================================================\nPSM Release Status: READY FOR PRODUCTION\n====================================================$'
    }

    It 'keeps scripts syntactically valid and free of environment operations' {
        foreach($name in @('RAT.Common.ps1','Invoke-ReleaseAcceptanceTest.ps1')){
            $path=Join-Path $ratRoot $name
            $tokens=$null
            $errors=$null
            [void][Management.Automation.Language.Parser]::ParseFile(
                $path,[ref]$tokens,[ref]$errors)
            @($errors).Count|Should Be 0
            $content=Get-Content -Raw $path
            $content|Should Not Match '(?i)\b(Start|Stop|Restart)-Service\b|\bInvoke-Command\b|\bsqlcmd\b'
        }
    }
}
