namespace WolfConquer.Tests;

public sealed class DataToolOriginalFlowCapturePreflightTests
{
    [Fact]
    public void PreflightReportPassesWhenOldStackListensAndClientHasNoWatchedConnections()
    {
        var report = OriginalFlowCapturePreflightLogic.CreateReport(
            @"client\Env_DX8\Conquer.exe",
            [new WorldRelayCapturePreflightProcess(1234, @"client\Env_DX8\Conquer.exe")],
            [
                new WorldRelayCaptureTcpConnection(2001, "0.0.0.0", 2021, "0.0.0.0", 0, "Listen"),
                new WorldRelayCaptureTcpConnection(2002, "0.0.0.0", 9960, "0.0.0.0", 0, "Listen"),
                new WorldRelayCaptureTcpConnection(2003, "0.0.0.0", 5818, "0.0.0.0", 0, "Listen"),
                new WorldRelayCaptureTcpConnection(1234, "192.168.84.1", 57432, "198.51.100.10", 80, "Established")
            ]);

        Assert.True(report.ServicesReady);
        Assert.True(report.ClientRunning);
        Assert.True(report.Ready);
        Assert.Empty(report.MissingServicePorts);
        Assert.Empty(report.ClientWatchedConnections);
        Assert.Empty(report.BlockingClientConnections);
        Assert.Equal(0, OriginalFlowCapturePreflightLogic.GetExitCode(report));

        var formatted = OriginalFlowCapturePreflightLogic.FormatReport(report);
        Assert.Contains("Original-flow capture preflight:", formatted);
        Assert.Contains("Watched service ports: 2021(patch), 9960(account), 5818(world)", formatted);
        Assert.Contains("2021(patch): pid=2001 state=Listen local=0.0.0.0:2021", formatted);
        Assert.Contains("Services ready: True", formatted);
        Assert.Contains("Client running: True", formatted);
        Assert.Contains("Client PID(s): 1234", formatted);
        Assert.Contains("Client TCP connections involving watched service ports:", formatted);
        Assert.Contains("  none", formatted);
        Assert.Contains("Status: PASS - old stack is listening and the client has no active watched-port TCP socket.", formatted);
        Assert.Contains("Ready: True", formatted);
    }

    [Fact]
    public void PreflightReportBlocksHalfDisconnectedClientConnectionToWatchedPort()
    {
        var report = OriginalFlowCapturePreflightLogic.CreateReport(
            @"client\Env_DX8\Conquer.exe",
            [new WorldRelayCapturePreflightProcess(1234, @"client\Env_DX8\Conquer.exe")],
            [
                new WorldRelayCaptureTcpConnection(2001, "0.0.0.0", 2021, "0.0.0.0", 0, "Listen"),
                new WorldRelayCaptureTcpConnection(2002, "0.0.0.0", 9960, "0.0.0.0", 0, "Listen"),
                new WorldRelayCaptureTcpConnection(2003, "0.0.0.0", 5818, "0.0.0.0", 0, "Listen"),
                new WorldRelayCaptureTcpConnection(1234, "192.168.84.1", 57430, "127.0.0.1", 9960, "CloseWait")
            ]);

        Assert.True(report.ServicesReady);
        Assert.True(report.ClientRunning);
        Assert.False(report.Ready);
        Assert.Single(report.ClientWatchedConnections);
        Assert.Single(report.BlockingClientConnections);
        Assert.Equal(2, OriginalFlowCapturePreflightLogic.GetExitCode(report));

        var formatted = OriginalFlowCapturePreflightLogic.FormatReport(report);
        Assert.Contains("remote=127.0.0.1:9960", formatted);
        Assert.Contains("watchedPorts=9960(account)", formatted);
        Assert.Contains("WARNING: client TCP connection(s) already involve watched service ports: 1", formatted);
        Assert.Contains("Status: FAIL - restart or fully close the client before opening the timed capture window.", formatted);
        Assert.Contains("Ready: False", formatted);
    }

    [Fact]
    public void PreflightReportFailsWhenRequiredServiceListenerIsMissing()
    {
        var report = OriginalFlowCapturePreflightLogic.CreateReport(
            @"client\Env_DX8\Conquer.exe",
            [],
            [
                new WorldRelayCaptureTcpConnection(2001, "0.0.0.0", 2021, "0.0.0.0", 0, "Listen"),
                new WorldRelayCaptureTcpConnection(2002, "0.0.0.0", 9960, "0.0.0.0", 0, "Listen")
            ]);

        Assert.False(report.ServicesReady);
        Assert.False(report.ClientRunning);
        Assert.False(report.Ready);
        Assert.Equal([5818], report.MissingServicePorts);
        Assert.Empty(report.BlockingClientConnections);
        Assert.Equal(2, OriginalFlowCapturePreflightLogic.GetExitCode(report));

        var formatted = OriginalFlowCapturePreflightLogic.FormatReport(report);
        Assert.Contains("5818(world): missing", formatted);
        Assert.Contains("Services ready: False", formatted);
        Assert.Contains("Client PID(s): <none>", formatted);
        Assert.Contains("Status: FAIL - required old-stack listener(s) missing: 5818(world).", formatted);
        Assert.Contains("Ready: False", formatted);
    }

    [Fact]
    public async Task HelpMentionsOriginalFlowCapturePreflightCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool original-flow-capture-preflight [clientExePath]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task OriginalFlowCapturePreflightUsageErrorReturnsOne()
    {
        var result = await RunDataToolAsync("original-flow-capture-preflight", "client-a.exe", "client-b.exe");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains(
            "Original-flow capture preflight accepts at most one client executable path.",
            result.StandardError);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ConsoleRunResult(process.ExitCode, standardOutput, standardError);
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

        throw new InvalidOperationException("Could not find repository root.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
