using System.Diagnostics;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class DataToolLocalWorldHandoffStatusTests
{
    [Fact]
    public void ReportsReadyWhenAccountServerForwardsToCandidateWorld()
    {
        var candidate = LocalWorldHandoffStatusLogic.DefaultCandidateWorldEndpoint;
        var report = LocalWorldHandoffStatusLogic.CreateReport(
            candidate,
            [
                Listener(10, "127.0.0.1", 80),
                Listener(20, "0.0.0.0", 9960),
                Listener(30, "127.0.0.1", 5818),
                Listener(40, "127.0.0.1", 15818)
            ],
            [
                new LocalWorldHandoffProcess(
                    20,
                    "dotnet.exe",
                    "dotnet WolfConquer.AccountServer.dll --listen --address 0.0.0.0 --port 9960 --legacy-account-wire --world-endpoint 127.0.0.1:15818")
            ]);

        var formatted = LocalWorldHandoffStatusLogic.FormatReport(report);

        Assert.True(report.AllListenersReady);
        Assert.True(report.ReadyForManualLogin);
        Assert.Equal(LocalWorldHandoffForwardKind.CandidateWorld, report.ForwardStatus.Kind);
        Assert.Contains("Local WolfConquer world handoff status:", formatted);
        Assert.Contains("LoginWeb: ready desired=127.0.0.1:80", formatted);
        Assert.Contains("AccountServer: ready desired=127.0.0.1:9960", formatted);
        Assert.Contains("Old world: ready desired=127.0.0.1:5818", formatted);
        Assert.Contains("Candidate world: ready desired=127.0.0.1:15818", formatted);
        Assert.Contains("Account forward target: candidate-world endpoint=127.0.0.1:15818", formatted);
        Assert.Contains("All listeners ready: True", formatted);
        Assert.Contains("ReadyForManualLogin: True", formatted);
        Assert.Contains("Submit one manual client login", formatted);
    }

    [Fact]
    public void ReportsRestoredOldWorldForwardWhenWorldEndpointOverrideIsAbsent()
    {
        var candidate = LocalWorldHandoffStatusLogic.DefaultCandidateWorldEndpoint;
        var report = LocalWorldHandoffStatusLogic.CreateReport(
            candidate,
            [
                Listener(10, "127.0.0.1", 80),
                Listener(20, "0.0.0.0", 9960),
                Listener(30, "127.0.0.1", 5818),
                Listener(40, "127.0.0.1", 15818)
            ],
            [
                new LocalWorldHandoffProcess(
                    20,
                    "dotnet.exe",
                    "dotnet WolfConquer.AccountServer.dll --listen --address 0.0.0.0 --port 9960 --legacy-account-wire")
            ]);

        var formatted = LocalWorldHandoffStatusLogic.FormatReport(report);

        Assert.True(report.AllListenersReady);
        Assert.False(report.ReadyForManualLogin);
        Assert.Equal(LocalWorldHandoffForwardKind.OldWorld, report.ForwardStatus.Kind);
        Assert.Contains(
            "Account forward target: old-world endpoint=127.0.0.1:5818 source=pid 20 default runtime endpoint",
            formatted);
        Assert.Contains("ReadyForManualLogin: False", formatted);
        Assert.Contains(
            "Restart WolfConquer AccountServer on 9960 with --world-endpoint 127.0.0.1:15818.",
            formatted);
    }

    [Theory]
    [InlineData("15818", "127.0.0.1", 15818)]
    [InlineData("localhost:15819", "localhost", 15819)]
    public void CandidateEndpointCanBeParsedFromPortOrAddressPort(string value, string address, int port)
    {
        var endpoint = LocalWorldHandoffStatusLogic.ParseCandidateWorldEndpoint(value);

        Assert.Equal("WorldServer", endpoint.Role);
        Assert.Equal(address, endpoint.Address);
        Assert.Equal(port, endpoint.Port);
    }

    [Fact]
    public async Task HelpMentionsLocalWorldHandoffStatusCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool local-world-handoff-status [candidateWorldEndpointOrPort]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static WorldRelayCaptureTcpConnection Listener(int pid, string address, int port) =>
        new(pid, address, port, "0.0.0.0", 0, "Listen");

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
