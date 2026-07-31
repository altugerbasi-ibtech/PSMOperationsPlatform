$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$deploymentRoot=Join-Path $repoRoot 'Release\Deployment'
$schemaPath=Join-Path $deploymentRoot 'DeploymentConfiguration.schema.json'
$samplePath=Join-Path $deploymentRoot 'DeploymentConfiguration.sample.json'
$templatePath=Join-Path $deploymentRoot 'DeploymentConfiguration.template.json'
$validatorPath=Join-Path $deploymentRoot 'Test-DeploymentConfiguration.ps1'

Describe 'WP-007.Z.2.A deployment configuration' {
    It 'contains parseable template sample and schema JSON' {
        foreach($path in @($templatePath,$samplePath,$schemaPath)){
            { Get-Content -Raw $path | ConvertFrom-Json } | Should Not Throw
        }
    }

    It 'defines every required section and rejects unknown properties' {
        $schema=Get-Content -Raw $schemaPath | ConvertFrom-Json
        $schema.additionalProperties|Should Be $false
        foreach($name in @('Deployment','SqlServer','Collector','Portal','SqlCollector','Security','Validation')){
            ($schema.required -contains $name)|Should Be $true
            $schema.properties.$name.additionalProperties|Should Be $false
        }
    }

    It 'defines port ranges booleans versions recovery and compatibility constraints' {
        $schema=Get-Content -Raw $schemaPath | ConvertFrom-Json
        $schema.properties.SqlServer.properties.Port.minimum|Should Be 1
        $schema.properties.SqlServer.properties.Port.maximum|Should Be 65535
        $schema.properties.Security.properties.WinRMPort.minimum|Should Be 1
        $schema.properties.Security.properties.WinRMPort.maximum|Should Be 65535
        $schema.properties.SqlServer.properties.CompatibilityLevel.const|Should Be 160
        ($schema.properties.SqlServer.properties.RecoveryModel.enum -contains 'BULK_LOGGED')|Should Be $true
        $schema.properties.Security.properties.UseTLS.type|Should Be 'boolean'
        $schema.properties.Validation.properties.RunReleaseAcceptanceTest.type|Should Be 'boolean'
    }

    It 'accepts the generic sample' {
        $output=& $validatorPath -Path $samplePath 2>&1
        $LASTEXITCODE|Should Be 0
        $output|Should Match 'PASS'
    }

    It 'rejects the unpopulated template' {
        & $validatorPath -Path $templatePath 2>$null
        $LASTEXITCODE|Should Be 1
    }

    It 'detects invalid JSON missing values ports duplicate servers and empty accounts' {
        $sample=Get-Content -Raw $samplePath | ConvertFrom-Json
        $cases=@(
            @{ Mutate={param($c) $c.SqlServer.Port=0} },
            @{ Mutate={param($c) $c.Security.WinRMPort=70000} },
            @{ Mutate={param($c) $c.Portal.Server=$c.Collector.Server} },
            @{ Mutate={param($c) $c.SqlCollector.ServiceAccount=''} },
            @{ Mutate={param($c) $c.Deployment.ProductVersion='invalid'} },
            @{ Mutate={param($c) $c.Deployment|Add-Member UnknownValue 'not-allowed'} }
        )
        foreach($case in $cases){
            $copy=Get-Content -Raw $samplePath|ConvertFrom-Json
            & $case.Mutate $copy
            $path=Join-Path $TestDrive ([guid]::NewGuid().ToString()+'.json')
            $copy|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $path
            & $validatorPath -Path $path 2>$null
            $LASTEXITCODE|Should Be 1
        }
        $invalidPath=Join-Path $TestDrive 'invalid.json'
        Set-Content -LiteralPath $invalidPath -Value '{'
        & $validatorPath -Path $invalidPath 2>$null
        $LASTEXITCODE|Should Be 1
    }

    It 'contains no secret-bearing configuration fields' {
        $content=(Get-Content -Raw $schemaPath)+(Get-Content -Raw $templatePath)+
            (Get-Content -Raw $samplePath)
        $content|Should Not Match '(?i)"(Password|ConnectionString|Certificate|PrivateKey|Secret)"\s*:'
    }
}
