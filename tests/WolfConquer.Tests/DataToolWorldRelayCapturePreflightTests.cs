namespace WolfConquer.Tests;

public sealed class DataToolWorldRelayCapturePreflightTests
{
    [Fact]
    public void PreflightReportBlocksAlreadyConnectedWorldSession()
    {
        var report = WorldRelayCapturePreflightLogic.CreateReport(
            @"client\Env_DX8\Conquer.exe",
            [new WorldRelayCapturePreflightProcess(1234, @"client\Env_DX8\Conquer.exe")],
            [
                new WorldRelayCaptureTcpConnection(1234, "192.168.84.1", 57429, "127.0.0.1", 2021, "Established"),
                new WorldRelayCaptureTcpConnection(1234, "192.168.84.1", 57430, "127.0.0.1", 5818, "Established"),
                new WorldRelayCaptureTcpConnection(1234, "10.0.0.116", 57432, "198.51.100.10", 80, "CloseWait")
            ]);

        Assert.True(report.ClientRunning);
        Assert.False(report.Ready);
        Assert.Single(report.BlockingConnections);

        var formatted = WorldRelayCapturePreflightLogic.FormatReport(report);
        Assert.Contains("Client running: True", formatted);
        Assert.Contains("Client PID(s): 1234", formatted);
        Assert.Contains("remote=127.0.0.1:5818", formatted);
        Assert.Contains("WARNING: active world/relay TCP connection(s) already open: 1", formatted);
        Assert.Contains("Status: FAIL - relay capture needs a fresh world-entry connection.", formatted);
        Assert.Contains("Ready: False", formatted);
    }

    [Fact]
    public void PreflightReportPassesWhenClientIsNotRunning()
    {
        var report = WorldRelayCapturePreflightLogic.CreateReport(
            @"client\Env_DX8\Conquer.exe",
            [],
            [
                new WorldRelayCaptureTcpConnection(9999, "192.168.84.1", 57430, "127.0.0.1", 5818, "Established")
            ]);

        Assert.False(report.ClientRunning);
        Assert.True(report.Ready);
        Assert.Empty(report.KeyConnections);

        var formatted = WorldRelayCapturePreflightLogic.FormatReport(report);
        Assert.Contains("Client running: False", formatted);
        Assert.Contains("Client PID(s): <none>", formatted);
        Assert.Contains("Key TCP connections:", formatted);
        Assert.Contains("  none", formatted);
        Assert.Contains("Status: PASS - client is not running; start it only after the relay and forwarded AccountServer are ready.", formatted);
        Assert.Contains("Ready: True", formatted);
    }

    [Fact]
    public void PreflightReportIgnoresClosedWorldConnections()
    {
        var report = WorldRelayCapturePreflightLogic.CreateReport(
            @"client\Env_DX8\Conquer.exe",
            [new WorldRelayCapturePreflightProcess(1234, @"client\Env_DX8\Conquer.exe")],
            [
                new WorldRelayCaptureTcpConnection(1234, "192.168.84.1", 57430, "127.0.0.1", 5818, "Closed")
            ]);

        Assert.True(report.ClientRunning);
        Assert.True(report.Ready);
        Assert.Empty(report.BlockingConnections);

        var formatted = WorldRelayCapturePreflightLogic.FormatReport(report);
        Assert.Contains("remote=127.0.0.1:5818", formatted);
        Assert.Contains("Status: PASS - no active client TCP connection to world/relay ports 5818/15819.", formatted);
        Assert.Contains("Ready: True", formatted);
    }

    [Fact]
    public async Task HelpMentionsWorldRelayCapturePreflightCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool world-relay-capture-preflight [clientExePath]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
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
