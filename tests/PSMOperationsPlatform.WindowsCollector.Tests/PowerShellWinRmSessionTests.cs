using System.Management.Automation.Runspaces;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

[Collection("PowerShell session tests")]
public sealed class PowerShellWinRmSessionTests
{
    [Fact]
    public async Task OpenSessionSupportsSequentialReusableInvocation()
    {
        Runspace runspace = RunspaceFactory.CreateRunspace();
        var session = new PowerShellWinRmSession(runspace);

        await session.OpenAsync(CancellationToken.None);
        IReadOnlyList<WinRmCommandRecord> first =
            await session.InvokeAsync(
                new WinRmCommandDefinition("Get-Date"),
                CancellationToken.None);
        IReadOnlyList<WinRmCommandRecord> second =
            await session.InvokeAsync(
                new WinRmCommandDefinition("Get-Date"),
                CancellationToken.None);

        Assert.True(session.IsUsable);
        Assert.Single(first);
        Assert.Single(second);

        await session.DisposeAsync();
        Assert.False(session.IsUsable);
    }

    [Fact]
    public void CommandDefinitionCopiesAndValidatesParameters()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = "PID",
        };
        var command = new WinRmCommandDefinition(
            " Get-Variable ",
            parameters);
        parameters["Name"] = "SENSITIVE-SENTINEL";

        Assert.Equal("Get-Variable", command.CommandName);
        Assert.Equal("PID", command.Parameters["Name"]);
        Assert.Throws<ArgumentException>(
            () => new WinRmCommandDefinition(" "));
    }
}

[CollectionDefinition(
    "PowerShell session tests",
    DisableParallelization = true)]
public sealed class PowerShellSessionTestCollection;
