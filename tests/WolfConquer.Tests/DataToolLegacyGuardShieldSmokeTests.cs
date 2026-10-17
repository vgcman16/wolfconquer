using System.Diagnostics;
using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class DataToolLegacyGuardShieldSmokeTests
{
    [Fact]
    public async Task LegacyGuardShieldSmokePrintsWelcomeHashResponse()
    {
        var hashes = new LegacyGuardShieldHashSet(
            "effect-md5",
            "type-md5",
            "strres-md5",
            "exe-md5");
        await using var server = CreateLegacyLoopbackServer(hashes);

        server.Start();
        var result = await RunDataToolAsync(
            "legacy-guard-shield-smoke",
            "127.0.0.1",
            server.BoundEndPoint.Port.ToString(),
            "--expect-response",
            hashes.ToLegacyResponse());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Legacy GuardShield smoke:", result.StandardOutput);
        Assert.Contains($"endpoint=127.0.0.1:{server.BoundEndPoint.Port}", result.StandardOutput);
        Assert.Contains("command=Welcome:Hash:HD123:HWID456", result.StandardOutput);
        Assert.Contains($"response={hashes.ToLegacyResponse()}", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task LegacyGuardShieldSmokeReturnsNonzeroForResponseMismatch()
    {
        var hashes = new LegacyGuardShieldHashSet(
            "effect-md5",
            "type-md5",
            "strres-md5",
            "exe-md5");
        var wrongResponse = new string('x', hashes.ToLegacyResponse().Length);
        await using var server = CreateLegacyLoopbackServer(hashes);

        server.Start();
        var result = await RunDataToolAsync(
            "legacy-guard-shield-smoke",
            "127.0.0.1",
            server.BoundEndPoint.Port.ToString(),
            "--expect-response",
            wrongResponse);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains($"response={hashes.ToLegacyResponse()}", result.StandardOutput);
        Assert.Contains(
            $"Expected GuardShield response {wrongResponse} but received {hashes.ToLegacyResponse()}.",
            result.StandardError);
    }

    [Fact]
    public async Task HelpMentionsLegacyGuardShieldSmokeCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool legacy-guard-shield-smoke <address> <port> [--command text] [--expect-response text] [--timeout-ms ms]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static ConquerTcpServer CreateLegacyLoopbackServer(LegacyGuardShieldHashSet hashes)
    {
        return new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.Gateway,
                IPAddress.Loopback,
                ListenPort: 0,
                WireMode: ConquerTcpWireMode.LegacyGuardShield,
                GuardShieldTextProtocol: new LegacyGuardShieldTextProtocolOptions(hashes)),
            (_, _, _) => throw new InvalidOperationException("GuardShield text mode must not dispatch packet frames."));
    }

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
