using System.Diagnostics;

namespace WolfConquer.Tests;

public sealed class DataToolClientRoutingTests
{
    [Fact]
    public async Task InspectClientRoutingCommandPrintsReadyAndDeferredInventory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-routing-" + Guid.NewGuid().ToString("N"));
        var clientPath = Path.Combine(tempRoot, "client");
        var dbServerPath = Path.Combine(tempRoot, "DBServer");

        try
        {
            Directory.CreateDirectory(clientPath);
            Directory.CreateDirectory(Path.Combine(clientPath, "AutoPatch"));
            Directory.CreateDirectory(dbServerPath);

            await File.WriteAllTextAsync(
                Path.Combine(clientPath, "server38.dat"),
                """
                [Header]
                GroupAmount=1
                Group1=GroupPic8

                [Group1]
                ServerAmount=1
                Server1=FixtureServer
                Ip1=198.51.100.14
                Port1=9958
                ServerName1=Dragon Crossing
                HintWord1=Welcome to Dragon Crossing
                Pic1=servericon05
                """);

            await File.WriteAllTextAsync(
                Path.Combine(clientPath, "AutoPatch", "SocketConfig.ini"),
                """
                [Header]
                GroupAmount=1

                [Group1]
                count=1
                Server4=U4 198.51.100.11 9537 198.51.100.17 9532 1 1
                """);

            await File.WriteAllTextAsync(
                Path.Combine(dbServerPath, "client_config.ini"),
                """
                2 TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1 9220
                101 Realm1 3935 0328 0198 0003 0003 1 0 198.51.100.16 9221
                """);

            var result = await RunDataToolAsync(
                "inspect-client-routing",
                clientPath,
                dbServerPath,
                "--local-address",
                "127.0.0.1");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client routing inventory:", result.StandardOutput);
            Assert.Contains($"Client: {Path.GetFullPath(clientPath)}", result.StandardOutput);
            Assert.Contains($"DBServer: {Path.GetFullPath(dbServerPath)}", result.StandardOutput);
            Assert.Contains("Local address: 127.0.0.1", result.StandardOutput);
            Assert.Contains("LoginWeb", result.StandardOutput);
            Assert.Contains("Gateway", result.StandardOutput);
            Assert.Contains("AccountServer", result.StandardOutput);
            Assert.Contains("WorldServer", result.StandardOutput);
            Assert.Contains("current -> local", result.StandardOutput);
            Assert.Contains("127.0.0.1:80", result.StandardOutput);
            Assert.Contains("127.0.0.1:9960", result.StandardOutput);
            Assert.Contains("ready", result.StandardOutput);
            Assert.Contains("server38.dat", result.StandardOutput);
            Assert.Contains("198.51.100.14:9958", result.StandardOutput);
            Assert.Contains("AutoPatch/SocketConfig.ini", result.StandardOutput);
            Assert.Contains("198.51.100.11:9537", result.StandardOutput);
            Assert.Contains("198.51.100.17:9532", result.StandardOutput);
            Assert.Contains("AutoPatch listener", result.StandardOutput);
            Assert.Contains("client_config.ini", result.StandardOutput);
            Assert.Contains("127.0.0.1:9220", result.StandardOutput);
            Assert.Contains("deferred", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HelpIncludesInspectClientRoutingUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("WolfConquer.DataTool inspect-client-routing [clientPath] [dbServerPath]", result.StandardOutput);
        Assert.Contains("[--local-address address]", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
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
