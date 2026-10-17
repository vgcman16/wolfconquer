using System.Diagnostics;
using System.Security.Cryptography;

namespace WolfConquer.Tests;

public sealed class DataToolClientRoutingSnapshotTests
{
    [Fact]
    public async Task SnapshotClientRoutingCommandWritesManifestAndPrintsFileHashes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-routing-snapshot-" + Guid.NewGuid().ToString("N"));
        var clientPath = Path.Combine(tempRoot, "client");
        var outputPath = Path.Combine(tempRoot, "snapshot", "manifest.txt");

        var files = new[]
        {
            new SnapshotFixtureFile("server38.dat", "server-list fixture"u8.ToArray()),
            new SnapshotFixtureFile(Path.Combine("ini", "ndloginServer.dat"), "login-server fixture"u8.ToArray()),
            new SnapshotFixtureFile(
                Path.Combine("ini", "region.ini"),
                "1 1002 430 380 440 390 0 TwinCity 0 0 0 0 none none\r\n"u8.ToArray()),
            new SnapshotFixtureFile(
                Path.Combine("ini", "restrain.ini"),
                "1 2 2xDmg:TestMonster;NoStack\r\n"u8.ToArray()),
            new SnapshotFixtureFile(
                Path.Combine("AutoPatch", "SocketConfig.ini"),
                "Server4=U4 198.51.100.11 9537 198.51.100.17 9532 1 1\r\n"u8.ToArray()),
            new SnapshotFixtureFile("GuardShield.dat", [0x10, 0x20, 0x30, 0x40, 0x50]),
        };

        try
        {
            foreach (var file in files)
            {
                var path = Path.Combine(clientPath, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, file.Bytes);
            }

            var result = await RunDataToolAsync("snapshot-client-routing", clientPath, outputPath);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outputPath), $"Expected manifest at {outputPath}.");

            var fullClientPath = Path.GetFullPath(clientPath);
            var fullOutputPath = Path.GetFullPath(outputPath);
            var manifest = await File.ReadAllTextAsync(outputPath);

            Assert.Contains("Client routing snapshot:", result.StandardOutput);
            Assert.Contains($"Client: {fullClientPath}", result.StandardOutput);
            Assert.Contains($"Output: {fullOutputPath}", result.StandardOutput);
            Assert.Contains("Client routing snapshot:", manifest);
            Assert.Contains($"Client: {fullClientPath}", manifest);

            foreach (var file in files)
            {
                var relativePath = NormalizeRelativePath(file.RelativePath);
                var length = file.Bytes.Length.ToString();
                var sha256 = Convert.ToHexString(SHA256.HashData(file.Bytes)).ToLowerInvariant();
                var md5 = Convert.ToHexString(MD5.HashData(file.Bytes)).ToLowerInvariant();
                var expected = $"{relativePath} length={length} sha256={sha256} md5={md5}";

                Assert.Contains(expected, result.StandardOutput);
                Assert.Contains(expected, manifest);
            }

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
    public async Task HelpIncludesSnapshotClientRoutingUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("WolfConquer.DataTool snapshot-client-routing [clientPath] [outputPath]", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
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

    private sealed record SnapshotFixtureFile(string RelativePath, byte[] Bytes);

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
