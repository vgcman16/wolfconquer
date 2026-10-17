using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientItemTypeWarningOwnerAuditTests
{
    [Fact]
    public async Task MapsWarningIdsToClientTextOwners()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-itemtype-owner-audit-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var logPath = Path.Combine(tempRoot, "debug", "tqan.log");

        try
        {
            await WriteFixtureFileAsync(
                logPath,
                """
                2026-07-04 01:00:00 boot
                WARNING: Read Error in ItemType.dat, itemtypeID=2154
                ignored old tail WARNING: Read Error in ItemType.dat, itemtypeID=1105
                WARNING: Read Error in ItemType.dat, itemtypeID=2154
                WARNING: Read Error in ItemType.dat, itemtypeID=3316793
                WARNING: Read Error in ItemType.dat, itemtypeID=729464
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "ItemName.ini"),
                """
                2154=Side table owner
                12154=Not a whole-token match
                3316793 = Rare entry
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "Script", "Quest", "owner.lua"),
                """
                local reward = 729464
                local another = {2154, 729464}
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "ItemType.dat"),
                """
                1105 would be ignored because this is the source table
                """);
            await File.WriteAllBytesAsync(Path.Combine(clientRoot, "Script", "binary.dat"), [0, 49, 49, 48, 53]);

            var result = await RunDataToolAsync(
                "client-itemtype-warning-owner-audit",
                logPath,
                clientRoot,
                "--since-line",
                "4");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client ItemType warning owner audit:", result.StandardOutput);
            Assert.Contains("Warnings: total=3 uniqueIds=3 reportedIds=3", result.StandardOutput);
            Assert.Contains("Text files: scanned=2 skipped=1", result.StandardOutput);
            Assert.Contains("id 2154 logCount=1", result.StandardOutput);
            Assert.Contains("ini/ItemName.ini hits=1 firstLines=1", result.StandardOutput);
            Assert.Contains("Script/Quest/owner.lua hits=1 firstLines=2", result.StandardOutput);
            Assert.Contains("id 3316793 logCount=1", result.StandardOutput);
            Assert.Contains("ini/ItemName.ini hits=1 firstLines=3", result.StandardOutput);
            Assert.Contains("id 729464 logCount=1", result.StandardOutput);
            Assert.Contains("Script/Quest/owner.lua hits=2 firstLines=1, 2", result.StandardOutput);
            Assert.DoesNotContain("id 1105", result.StandardOutput);
            Assert.DoesNotContain("12154", result.StandardOutput);
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
    public async Task ReportsNoOwnerAndHonorsTopLimit()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-itemtype-owner-audit-top-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var logPath = Path.Combine(tempRoot, "debug", "tqan.log");

        try
        {
            await WriteFixtureFileAsync(
                logPath,
                """
                WARNING: Read Error in ItemType.dat, itemtypeID=200806
                WARNING: Read Error in ItemType.dat, itemtypeID=200806
                WARNING: Read Error in ItemType.dat, itemtypeID=1106
                """);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "owners.ini"), "1106=Known owner\r\n");

            var result = await RunDataToolAsync(
                "client-itemtype-warning-owner-audit",
                logPath,
                clientRoot,
                "--top",
                "1");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Warnings: total=3 uniqueIds=2 reportedIds=1", result.StandardOutput);
            Assert.Contains("id 200806 logCount=2", result.StandardOutput);
            Assert.Contains("<no text owner found>", result.StandardOutput);
            Assert.DoesNotContain("id 1106", result.StandardOutput);
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
    public async Task HelpIncludesClientItemTypeWarningOwnerAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-itemtype-warning-owner-audit <clientDebugLogPath> <clientRoot> [--since-line N] [--since timestamp] [--top N]",
            result.StandardOutput);
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

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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

    private static async Task WriteFixtureFileAsync(string path, string contents, Encoding? encoding = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = Path.GetDirectoryName(sourcePath);
        while (directory is not null && !Directory.Exists(Path.Combine(directory, "src", "WolfConquer.DataTool")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
