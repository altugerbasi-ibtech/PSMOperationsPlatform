$repositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path

Describe 'WP-008.9 repository governance remediation' {
    $approvedPackages=@('WP-008.4','WP-008.5','WP-008.6','WP-008.7','WP-008.7.Q','WP-008.8')

    foreach($package in $approvedPackages){
        foreach($review in @('Architecture','Implementation','Repository')){
            It "$package $review Review is approved" {
                $path=Join-Path $repositoryRoot "docs\tasks\$package-$review-Review.md"
                (Test-Path -LiteralPath $path -PathType Leaf) | Should Be $true
                $text=Get-Content -Raw -LiteralPath $path
                $text | Should Match '(?im)^(?:## Status\r?\n\r?\n|Status:\s*\*\*)Approved'
            }
        }
    }

    foreach($package in $approvedPackages){
        It "$package approval records preserve integration or production limitations" {
            $combined=@('Architecture','Implementation','Repository')|ForEach-Object{
                Get-Content -Raw -LiteralPath (
                    Join-Path $repositoryRoot "docs\tasks\$package-$_-Review.md")
            }
            ($combined -join "`n") | Should Match '(?i)integration|production|WP-007\.Z'
        }
    }

    It 'authoritative specifications have approved status' {
        foreach($package in @('WP-008.5','WP-008.6','WP-008.7','WP-008.7.Q','WP-008.8')){
            $text=Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "workpackages\$package.md")
            $text | Should Match '(?m)^status:\s*Approved\s*$'
            $text | Should Match '(?m)^\| Status \| Approved \|$'
        }
    }

    It 'WP-007.Z remains deferred and not started' {
        $spec=Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'workpackages\WP-008.9.md')
        $spec | Should Match 'Deferred To.*WP-007\.Z'
        $spec | Should Match 'WP-007\.Z remains deferred and not started'
    }

    It 'WP-001 artifact and index consistently record completion' {
        $artifact=Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'docs\tasks\WP-001-Solution-Skeleton.md')
        $index=Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'docs\tasks\README.md')
        $artifact | Should Match '(?m)^status:\s*Completed\s*$'
        $index | Should Match 'WP-001-Solution-Skeleton\.md.*Completed'
        $artifact | Should Not Match '(?i)production validation (succeeded|completed)'
    }

    It 'ADR catalog contains a source for every accepted catalog entry' {
        $catalog=Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'docs\adr\README.md')
        $matches=[regex]::Matches($catalog,'\[`([^`]+\.md)`\]\([^)]+\)\s+—\s+Accepted')
        foreach($match in $matches){
            (Test-Path -LiteralPath (Join-Path $repositoryRoot "docs\adr\$($match.Groups[1].Value)") -PathType Leaf) |
                Should Be $true
        }
        (Test-Path -LiteralPath (Join-Path $repositoryRoot 'docs\adr\ADR-005-Turkiye-Local-Time-Standard.md')) |
            Should Be $true
    }

    It 'does not invent an approval identity' {
        foreach($package in $approvedPackages){
            foreach($review in @('Architecture','Implementation','Repository')){
                $text=Get-Content -Raw -LiteralPath (
                    Join-Path $repositoryRoot "docs\tasks\$package-$review-Review.md")
                $text | Should Not Match '(?im)^Approved by:'
            }
        }
    }

    It 'records explicit human approval of WP-008.9 without starting WP-007.Z' {
        $spec=Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'workpackages\WP-008.9.md')
        $spec | Should Match '(?m)^status:\s*Approved\s*$'
        $spec | Should Match '(?m)^\| Status \| Approved \|$'
        foreach($review in @('Architecture','Implementation','Repository')){
            $text=Get-Content -Raw -LiteralPath (
                Join-Path $repositoryRoot "docs\tasks\WP-008.9-$review-Review.md")
            $text | Should Match '(?m)^Approved\.$'
            $text | Should Not Match '(?im)^Approved by:'
        }
        $spec | Should Match 'WP-007\.Z remains deferred and not started'
    }
}
