#requires -Version 5.1
Set-StrictMode -Version Latest

function New-PortalMonitoringCheck {
    param([string]$Prefix,[string]$Id,[string]$Category,[string]$Name,[string]$Target,
        [object]$Actual,[string]$Status='PASS',[string]$Severity='INFO',[object]$Expected='observe',
        [string]$Message='Value observed.',[string]$Recommendation=$null,[bool]$Mandatory=$false)
    New-OperationalObservation "$Prefix.$Id" $Category $Name $Name $Target $Status $Severity `
        $Expected $Actual $Message $Recommendation $Actual $Mandatory
}

function Test-PortalMonitoringConfiguration {
    param([object]$Configuration,[string]$PortalName)
    if($null -eq $Configuration.Portal -or $null -eq $Configuration.MonitoringValidation){throw 'Portal and MonitoringValidation configuration are required.'}
    $portal=$Configuration.Portal
    if($PortalName -and -not [string]::Equals($PortalName,$portal.Name,[StringComparison]::OrdinalIgnoreCase)){throw 'PortalName must identify the configured Portal.'}
    if($portal.Scheme -ne 'https' -or $portal.Port -lt 1 -or $portal.Port -gt 65535 -or $portal.AuthenticationMode -ne 'Windows'){throw 'Portal endpoint or authentication configuration is invalid.'}
    if($portal.BasePath -notmatch '^/' -or $portal.HealthPath -ne '/health'){throw 'Portal paths are invalid.'}
    if($Configuration.MonitoringValidation.InstrumentationName -ne 'PSMOperationsPlatform.Execution' -or $Configuration.MonitoringValidation.InstrumentationVersion -ne '1.0'){throw 'Monitoring instrumentation identity does not match the repository contract.'}
    if($Configuration.MonitoringValidation.BackendExpected -and -not $Configuration.MonitoringValidation.ExporterExpected){throw 'A monitoring backend cannot be expected without an exporter.'}
    $serialized=$Configuration|ConvertTo-Json -Depth 12
    if($serialized -match '(?i)"(password|token|clientsecret|connectionstring|privatekey|certificate)"\s*:'){throw 'Secret-bearing configuration fields are prohibited.'}
    $true
}

function New-PortalMonitoringOperations {
    param([string]$RepositoryRoot)
    $root=$RepositoryRoot
    $getAuthenticationFacts=${function:Get-PortalAuthenticationCompositionFacts}
    @{
        ResolveDns={param($name)@(Resolve-OperationalDns $name)}
        TestTcp={param($name,$port)Test-OperationalTcpPort $name $port}
        GetHttp={param($uri)
            $request=[Net.HttpWebRequest]::Create($uri);$request.Method='GET';$request.Timeout=10000;$request.ReadWriteTimeout=10000;$request.AllowAutoRedirect=$false;$request.MaximumAutomaticRedirections=5;$request.UseDefaultCredentials=$true
            try{$response=$request.GetResponse();try{[pscustomobject]@{StatusCode=[int]$response.StatusCode;ContentType=[string]$response.ContentType;Location=[string]$response.Headers['Location'];ElapsedMilliseconds=0}}finally{$response.Dispose()}}
            catch{if($_.Exception.Response){$response=$_.Exception.Response;try{[pscustomobject]@{StatusCode=[int]$response.StatusCode;ContentType=[string]$response.ContentType;Location=[string]$response.Headers['Location'];ElapsedMilliseconds=0}}finally{$response.Dispose()}}else{throw}}
        }
        GetIis={param($configuration,$portal)Invoke-IisTargetChecks $configuration $portal.Server 1 (New-IisValidationOperations)}
        GetDatabase={param($configuration,$source)
            $target=@($configuration.SqlTargets|Where-Object{$_.ExpectedRole -eq 'OperationsDatabase' -and $_.ValidationEnabled})
            if($target.Count -ne 1){throw 'Exactly one enabled Operations database target is required.'}
            Invoke-SqlTargetChecks $configuration $target[0] 1 $source $false $false (New-SqlTargetValidationOperations)
        }
        GetRepositoryFacts=({
            $program=Get-Content -Raw (Join-Path $root 'src\PSMOperationsPlatform.Web\Program.cs')
            $settings=Get-Content -Raw (Join-Path $root 'src\PSMOperationsPlatform.Web\appsettings.json')
            $monitoring=Get-Content -Raw (Join-Path $root 'src\PSMOperationsPlatform.Application\Runtime\ExecutionMonitoring.cs')
            $composition=Get-Content -Raw (Join-Path $root 'src\PSMOperationsPlatform.Infrastructure\Persistence\OperationsDatabasePersistenceServiceCollectionExtensions.cs')
            $health=Get-Content -Raw (Join-Path $root 'src\PSMOperationsPlatform.WindowsCollector\WindowsCollectorHost.cs')
            $authentication=& $getAuthenticationFacts $root
            [pscustomobject]@{
                GenericHealth=$program -match 'MapHealthChecks\("/health"\)';DedicatedMonitoringEndpoint=$program -match 'monitoring';WindowsAuthentication=$authentication.Composed;IisWindowsScheme=$authentication.IisWindowsScheme;AuthenticationMiddleware=$authentication.AuthenticationMiddleware;AuthorizationMiddleware=$authentication.AuthorizationMiddleware;AuthenticatedFallbackPolicy=$authentication.AuthenticatedFallbackPolicy;AnonymousHealth=$authentication.AnonymousHealth;AllowedHostsRestricted=$settings -notmatch '"AllowedHosts"\s*:\s*"\*"'
                InstrumentationName=$monitoring -match 'InstrumentationName\s*=\s*"PSMOperationsPlatform\.Execution"';InstrumentationVersion=$monitoring -match 'InstrumentationVersion\s*=\s*"1\.0"';SchemaVersion=$monitoring -match 'ExecutionMonitoringSchemaVersion.*Value\s*=\s*1';SnapshotSchemaVersion=$monitoring -match 'ExecutionMonitoringSnapshotSchemaVersion.*Value\s*=\s*1'
                MetricCatalog=$monitoring -match 'ExecutionMetricCatalog';Meter=$monitoring -match 'new\(ExecutionMetricCatalog\.InstrumentationName';Activities=$monitoring -match 'ActivitySource';Snapshot=$monitoring -match 'GetCurrentSnapshot';HealthAssessment=$monitoring -match 'MonitoringHealthAssessment';Subscriber=$composition -match 'ExecutionMonitoringEventSubscriber';LoggingSubscriber=$composition -match 'LoggingExecutionEventSubscriber';Composite=$composition -match 'CompositeExecutionEventSink';HealthRegistration=$health -match 'AddCheck<ExecutionMonitoringHealthCheck>\("execution-monitoring"\)';ExporterConfigured=$false;BackendConfigured=$false
            }
        }).GetNewClosure()
        CurrentIdentity={[Security.Principal.WindowsIdentity]::GetCurrent().Name}
    }
}

function Add-PortalStageChecks {
    param([Collections.Generic.List[object]]$Results,[string]$Prefix,[object]$Portal,[string]$Reason)
    foreach($item in @(@('HOST.STAGE','PortalHostPrerequisites'),@('PROCESS.STAGE','PortalProcessService'),@('HTTP.STAGE','PortalHttpEndpoint'),@('AUTH.STAGE','PortalAuthentication'),@('CONFIG.STAGE','PortalConfiguration'),@('DATABASE.STAGE','PortalDatabaseConnectivity'))){$Results.Add((New-PortalMonitoringCheck $Prefix $item[0] $item[1] 'Deployment-stage validation' $Portal.Server $Portal.DeploymentExpected $(if($Portal.DeploymentExpected){'SKIPPED'}else{'NOT_APPLICABLE'}) MEDIUM 'deployed Portal' $Reason 'Deploy through an approved Portal work package before live validation.' $Portal.DeploymentExpected))}
}

function Invoke-PortalMonitoringChecks {
    param([object]$Configuration,[string]$RepositoryRoot,[bool]$SkipPortalChecks,[bool]$SkipMonitoringChecks,[bool]$SkipDatabaseChecks,[hashtable]$Operations)
    [void](Test-PortalMonitoringConfiguration $Configuration $Configuration.Portal.Name)
    $portal=$Configuration.Portal;$prefix='P001';$results=New-Object Collections.Generic.List[object]
    $results.Add((New-PortalMonitoringCheck $prefix 'TARGET.CONFIGURATION' PortalTargetConfiguration 'Portal target configuration' $portal.Name $true PASS INFO $true 'The enabled singleton Portal definition is valid and secret-free.' $null $true))
    if(-not $portal.ValidationEnabled){$results.Add((New-PortalMonitoringCheck $prefix 'TARGET.DISABLED' PortalTargetConfiguration 'Portal validation enabled' $portal.Name $false NOT_APPLICABLE INFO $false 'Portal validation is disabled by configuration.'));return $results.ToArray()}
    $facts=& $Operations.GetRepositoryFacts
    foreach($item in @(
        @('AUTH.SCHEME','IIS Windows Authentication scheme',$facts.IisWindowsScheme),
        @('AUTH.MIDDLEWARE','Authentication middleware',$facts.AuthenticationMiddleware),
        @('AUTHORIZATION.MIDDLEWARE','Authorization middleware order',$facts.AuthorizationMiddleware),
        @('AUTHORIZATION.FALLBACK','Authenticated fallback policy',$facts.AuthenticatedFallbackPolicy),
        @('HEALTH.ANONYMOUS','Explicit anonymous generic health policy',$facts.AnonymousHealth)
    )){$valid=[bool]$item[2];$results.Add((New-PortalMonitoringCheck $prefix $item[0] PortalAuthentication $item[1] 'repository' $valid $(if($valid){'PASS'}else{'FAIL'}) $(if($valid){'INFO'}else{'CRITICAL'}) $true 'Bounded source composition evidence only; live IIS and HTTP transport remain unvalidated.' 'Restore the approved IIS Integration composition.' $true))}
    if($SkipPortalChecks){Add-PortalStageChecks $results $prefix $portal 'SkipPortalChecks was supplied.'}
    elseif(-not $portal.DeploymentExpected){Add-PortalStageChecks $results $prefix $portal 'Portal deployment is not expected at this stage.'}
    else{
        try{$dns=@(& $Operations.ResolveDns $portal.Server);$results.Add((New-PortalMonitoringCheck $prefix 'NETWORK.DNS' DNSNetworkConnectivity 'Portal DNS' $portal.Server $dns.Count $(if($dns.Count){'PASS'}else{'FAIL'}) HIGH 'one or more addresses' 'Configured endpoint only.' 'Correct approved DNS configuration.' $true))}catch{$results.Add((New-OperationalExceptionResult "$prefix.NETWORK.DNS" DNSNetworkConnectivity 'Portal DNS' 'Configured endpoint only.' $portal.Server $_.Exception HIGH $true))}
        try{$tcp=& $Operations.TestTcp $portal.Server $portal.Port;$results.Add((New-PortalMonitoringCheck $prefix 'NETWORK.TCP' DNSNetworkConnectivity 'Portal TCP' $portal.Server $tcp $(if($tcp){'PASS'}else{'FAIL'}) HIGH $true 'Configured port only.' 'Correct approved listener/network configuration.' $true))}catch{$results.Add((New-OperationalExceptionResult "$prefix.NETWORK.TCP" DNSNetworkConnectivity 'Portal TCP' 'Configured endpoint only.' $portal.Server $_.Exception HIGH $true))}
        try{$iis=@(& $Operations.GetIis $Configuration $portal);$iisPass=-not ($iis|Where-Object Status -eq FAIL);$results.Add((New-PortalMonitoringCheck $prefix 'HOST.IIS' PortalHostPrerequisites 'ASP.NET Core IIS host prerequisites' $portal.Server $iisPass $(if($iisPass){'PASS'}else{'FAIL'}) HIGH $true 'Reused WP-007.Z.4 evidence.' 'Resolve cited IIS prerequisite findings.' $true));$results.Add((New-PortalMonitoringCheck $prefix 'PROCESS.IIS' PortalProcessService 'Portal IIS process and identity' $portal.Server $iisPass $(if($iisPass){'PASS'}else{'FAIL'}) HIGH $true 'Read-only IIS evidence.' 'Correct deployment through approved operations.' $true))}catch{$results.Add((New-OperationalExceptionResult "$prefix.HOST.IIS" PortalHostPrerequisites 'Portal IIS evidence' 'Reuse WP-007.Z.4.' $portal.Server $_.Exception HIGH $true));$results.Add((New-PortalMonitoringCheck $prefix 'PROCESS.UNAVAILABLE' PortalProcessService 'Portal process evidence' $portal.Server $null SKIPPED HIGH 'IIS evidence' 'Dependent IIS evidence is unavailable.' $null $true))}
        foreach($endpoint in @(@('HTTP.BASE',$portal.BasePath),@('HTTP.HEALTH',$portal.HealthPath))){try{$uri='{0}://{1}:{2}{3}' -f $portal.Scheme,$portal.Server,$portal.Port,$endpoint[1];$response=& $Operations.GetHttp $uri;$ok=$response.StatusCode -ge 200 -and $response.StatusCode -lt 400;$results.Add((New-PortalMonitoringCheck $prefix $endpoint[0] PortalHttpEndpoint 'Bounded Portal HTTP response' $portal.Name $response.StatusCode $(if($ok){'PASS'}else{'FAIL'}) HIGH '2xx or bounded redirect' 'Response body was not read or persisted.' 'Review deployed endpoint without disabling TLS validation.' $true))}catch{$results.Add((New-OperationalExceptionResult "$prefix.$($endpoint[0])" PortalHttpEndpoint 'Portal HTTP endpoint' 'Bounded HTTPS response.' $portal.Name $_.Exception HIGH $true))}}
        $authOk=$facts.WindowsAuthentication;$results.Add((New-PortalMonitoringCheck $prefix 'AUTH.COMPOSITION' PortalAuthentication 'Windows Authentication composition' $portal.Name $authOk $(if($authOk){'PASS'}else{'FAIL'}) CRITICAL $true 'Repository Web host composition.' 'Implement only through separately approved Portal feature scope.' $true))
        $results.Add((New-PortalMonitoringCheck $prefix 'CONFIG.ALLOWEDHOSTS' PortalConfiguration 'AllowedHosts restriction' $portal.Name $facts.AllowedHostsRestricted $(if($facts.AllowedHostsRestricted){'PASS'}else{'WARNING'}) MEDIUM $true 'Sanitized repository configuration observation.' 'Use an approved deployment-specific host allowlist.' $false))
        if($SkipDatabaseChecks){$results.Add((New-PortalMonitoringCheck $prefix 'DATABASE.SKIPPED' PortalDatabaseConnectivity 'Operations database validation' $portal.Name $null SKIPPED HIGH 'WP-009 schema and permission evidence' 'SkipDatabaseChecks was supplied.' 'Run approved database checks.' $true))}else{try{$db=@(& $Operations.GetDatabase $Configuration $portal.Server);$ok=-not ($db|Where-Object{$_.Mandatory -and $_.Status -eq 'FAIL'});$results.Add((New-PortalMonitoringCheck $prefix 'DATABASE.READINESS' PortalDatabaseConnectivity 'Operations database readiness' $portal.Name $ok $(if($ok){'PASS'}else{'FAIL'}) CRITICAL $true 'Reused WP-007.Z.5 and WP-009 evidence.' 'Resolve cited database prerequisites.' $true))}catch{$results.Add((New-OperationalExceptionResult "$prefix.DATABASE.ERROR" PortalDatabaseConnectivity 'Operations database readiness' 'Reuse approved validators.' $portal.Name $_.Exception CRITICAL $true))}}
    }
    $monitoring=@(
        [pscustomobject]@{Id='IDENTITY.CONTRACT';Category='MonitoringInstrumentationIdentity';Name='Instrumentation identity';Valid=[bool]($facts.InstrumentationName -and $facts.InstrumentationVersion -and $facts.SchemaVersion -and $facts.SnapshotSchemaVersion)},
        [pscustomobject]@{Id='METRICS.CATALOG';Category='MonitoringMetrics';Name='Metric catalog and local listener contract';Valid=[bool]($facts.MetricCatalog -and $facts.Meter)},
        [pscustomobject]@{Id='ACTIVITIES.CONTRACT';Category='MonitoringActivitiesTraces';Name='ActivitySource and safe Activity contract';Valid=[bool]$facts.Activities},
        [pscustomobject]@{Id='SUBSCRIBER.COMPOSITION';Category='MonitoringSubscriber';Name='Logging and Monitoring subscriber composition';Valid=[bool]($facts.Subscriber -and $facts.LoggingSubscriber -and $facts.Composite)},
        [pscustomobject]@{Id='SNAPSHOT.CONTRACT';Category='MonitoringSnapshot';Name='Immutable bounded snapshot contract';Valid=[bool]($facts.Snapshot -and $facts.SnapshotSchemaVersion)},
        [pscustomobject]@{Id='HEALTH.CONTRACT';Category='MonitoringHealth';Name='Existing advisory health assessment';Valid=[bool]$facts.HealthAssessment},
        [pscustomobject]@{Id='HEALTHCHECK.REGISTRATION';Category='HealthCheckIntegration';Name='execution-monitoring health registration';Valid=[bool]$facts.HealthRegistration},
        [pscustomobject]@{Id='INTEGRATION.EVENTS';Category='CollectorMonitoringIntegration';Name='Typed event boundary composition';Valid=[bool]($facts.Subscriber -and $facts.Composite)})
    foreach($item in $monitoring){$enabled=-not $SkipMonitoringChecks;$status=if(-not $enabled){'SKIPPED'}elseif($item.Valid){'PASS'}else{'FAIL'};$results.Add((New-PortalMonitoringCheck $prefix $item.Id $item.Category $item.Name 'repository' $item.Valid $status $(if($status -eq 'FAIL'){'HIGH'}elseif($status -eq 'SKIPPED'){'MEDIUM'}else{'INFO'}) $true $(if($enabled){'Repository contract observation; in-process behavior is covered by solution tests.'}else{'SkipMonitoringChecks was supplied.'}) 'Restore the existing repository contract; do not create alternate logic.' $true))}
    $results.Add((New-PortalMonitoringCheck $prefix 'HEALTH.PORTAL.GENERIC' HealthCheckIntegration 'Portal generic /health endpoint' $portal.Name $facts.GenericHealth $(if($facts.GenericHealth){'PASS'}else{'FAIL'}) INFO $true 'Only the generic /health endpoint is implemented.' 'Do not invent a dedicated Portal Monitoring endpoint.' $true))
    $results.Add((New-PortalMonitoringCheck $prefix 'HEALTH.PORTAL.MONITORING' HealthCheckIntegration 'Dedicated Portal Monitoring endpoint' $portal.Name $facts.DedicatedMonitoringEndpoint NOT_APPLICABLE INFO $false 'No dedicated endpoint is approved or implemented.' $null $false))
    $exporterOk=if($Configuration.MonitoringValidation.ExporterExpected){$facts.ExporterConfigured}else{-not $facts.ExporterConfigured};$exporterStatus=if($exporterOk){'PASS'}elseif($Configuration.MonitoringValidation.ExporterExpected){'FAIL'}else{'WARNING'};$results.Add((New-PortalMonitoringCheck $prefix 'EXPORTER.EXPECTATION' MonitoringExporterBackend 'Exporter and backend expectation' 'repository' $exporterOk $exporterStatus HIGH (-not $Configuration.MonitoringValidation.ExporterExpected) 'No remote exporter or backend was contacted.' 'Configure only through separately approved deployment scope.' $Configuration.MonitoringValidation.ExporterExpected))
    $mandatory=@($results|Where-Object{$_.Mandatory -and $_.Category -ne 'PortalMonitoringReadiness'});$readiness=if($mandatory|Where-Object Status -eq FAIL){'FAIL'}elseif($mandatory|Where-Object Status -in @('WARNING','SKIPPED')){'WARNING'}elseif($results|Where-Object Status -eq WARNING){'WARNING'}else{'PASS'};$results.Add((New-PortalMonitoringCheck $prefix 'READINESS.RESULT' PortalMonitoringReadiness 'Portal and Monitoring readiness' $portal.Name $readiness $readiness $(if($readiness -eq 'FAIL'){'CRITICAL'}elseif($readiness -eq 'WARNING'){'MEDIUM'}else{'INFO'}) 'all mandatory evidence passes' 'Repository capability and live deployment capability remain separate.' 'Resolve cited findings and obtain approved live evidence.' $true))
    $results.ToArray()
}
