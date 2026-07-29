# Hello Collector

The sample project also contains NoData, Failure, LongRunning and Cancellation
examples. All are Experimental, fake, read-only, infrastructure-neutral and
registered only by tests/sample composition.

`samples/PSMOperationsPlatform.HelloCollector` demonstrates SDK version 1.0. It declares a unique example PluginId and StrategyCode, is read-only, supports cancellation and timeout, explicitly declines retry/parallel/batching, validates fixed contracts, and returns one deterministic object artifact.

The sample uses no network, filesystem, registry, process, WinRM, PowerShell or SQL. It is referenced by tests only and is not registered in normal production startup.
