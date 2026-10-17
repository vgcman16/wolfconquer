using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientTextureWarningOwnerAuditTests
{
    [Fact]
    public async Task MapsMissingTextureWarningsToOwnersAndAssetPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-texture-owner-audit-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var logPath = Path.Combine(tempRoot, "debug", "tqan.log");

        try
        {
            await WriteFixtureFileAsync(
                logPath,
                """
                texture [3804] not found! old run
                unrelated startup line
                texture [3804] not found!
                texture [5800] not found!
                texture [3804] not found!
                texture [9999] not found!
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ani", "effect.ani"),
                """
                [Texture3804]
                Frame0=data/effect/pic3804.dds
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "ui.ini"),
                """
                Texture=5800
                Frame0=data/ui/missing5800.dds
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "Script", "owner.lua"),
                """
                local ref = "data/effect/pic3804.dds"
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ani", "item-icons.ani"),
                """
                [Item3313804]
                Frame0=data/ItemMinIcon/3313804.dds
                """);
            await File.WriteAllBytesAsync(Path.Combine(clientRoot, "Script", "binary.dat"), [0, 51, 56, 48, 52]);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "data", "effect", "pic3804.dds"), "fake texture bytes");

            var result = await RunDataToolAsync(
                "client-texture-warning-owner-audit",
                logPath,
                clientRoot,
                "--since-line",
                "3",
                "--texture",
                "3804",
                "--texture",
                "5800");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client texture warning owner audit:", result.StandardOutput);
            Assert.Contains($"Client log: {Path.GetFullPath(logPath)}", result.StandardOutput);
            Assert.Contains($"Client root: {Path.GetFullPath(clientRoot)}", result.StandardOutput);
            Assert.Contains("Since line: 3", result.StandardOutput);
            Assert.Contains("Texture filter: 3804, 5800", result.StandardOutput);
            Assert.Contains("Warnings: total=3 uniqueIds=2 reportedIds=2", result.StandardOutput);
            Assert.Contains("Text files: scanned=4 skipped=1", result.StandardOutput);
            Assert.Contains("texture 3804 logCount=2", result.StandardOutput);
            Assert.Contains("ani/effect.ani hits=2 firstLines=1, 2", result.StandardOutput);
            Assert.Contains("asset line=2 path=data/effect/pic3804.dds status=exists", result.StandardOutput);
            Assert.Contains("ani/item-icons.ani hits=1 firstLines=2", result.StandardOutput);
            Assert.Contains("asset line=2 path=data/ItemMinIcon/3313804.dds status=missing", result.StandardOutput);
            Assert.Contains("Script/owner.lua hits=1 firstLines=1", result.StandardOutput);
            Assert.Contains("texture 5800 logCount=1", result.StandardOutput);
            Assert.Contains("ini/ui.ini hits=2 firstLines=1, 2", result.StandardOutput);
            Assert.Contains("asset line=2 path=data/ui/missing5800.dds status=missing", result.StandardOutput);
            Assert.DoesNotContain("texture 9999", result.StandardOutput);
            Assert.DoesNotContain("old run", result.StandardOutput);
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
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-texture-owner-audit-top-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var logPath = Path.Combine(tempRoot, "debug", "tqan.log");

        try
        {
            await WriteFixtureFileAsync(
                logPath,
                """
                texture [200806] not found!
                texture [200806] not found!
                texture [1106] not found!
                """);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "owners.ini"), "Texture=1106\r\n");

            var result = await RunDataToolAsync(
                "client-texture-warning-owner-audit",
                logPath,
                clientRoot,
                "--top",
                "1");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Warnings: total=3 uniqueIds=2 reportedIds=1", result.StandardOutput);
            Assert.Contains("texture 200806 logCount=2", result.StandardOutput);
            Assert.Contains("<no text owner found>", result.StandardOutput);
            Assert.DoesNotContain("texture 1106", result.StandardOutput);
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
    public async Task HelpIncludesClientTextureWarningOwnerAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-texture-warning-owner-audit <clientDebugLogPath> <clientRoot> [--since-line N] [--texture id] [...] [--top N]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDataToolProcess(repositoryRoot);
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

    private static Process CreateDataToolProcess(string repositoryRoot)
    {
        var copiedDataTool = Path.Combine(AppContext.BaseDirectory, "WolfConquer.DataTool.dll");
        if (!File.Exists(copiedDataTool))
        {
            return TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(copiedDataTool);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    private static async Task WriteFixtureFileAsync(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
