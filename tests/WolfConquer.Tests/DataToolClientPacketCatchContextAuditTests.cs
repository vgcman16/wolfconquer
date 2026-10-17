using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientPacketCatchContextAuditTests
{
    [Fact]
    public async Task FiltersBySinceLineAndOpcodeAndPrintsContextWindows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-packet-catch-context-audit-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "tqan.log");

        try
        {
            await WriteFixtureFileAsync(
                logPath,
                """
                launch noise
                catch error in process msg:2227 old owner
                old stack tail
                owner before 2227 A
                handler context 2227 A
                catch error in process msg:2227
                stack after 2227 A
                owner after 2227 A
                catch error in process msg:2200 critical noise
                owner before 2227 B
                handler context 2227 B
                catch error in process msg:2227
                stack after 2227 B
                owner after 2227 B
                """);

            var result = await RunDataToolAsync(
                "client-packet-catch-context-audit",
                logPath,
                "--since-line",
                "4",
                "--opcode",
                "2227");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client packet catch context audit:", result.StandardOutput);
            Assert.Contains($"Client log: {Path.GetFullPath(logPath)}", result.StandardOutput);
            Assert.Contains("Since line: 4", result.StandardOutput);
            Assert.Contains("Opcode filter: 2227", result.StandardOutput);
            Assert.Contains("Context radius: 2", result.StandardOutput);
            Assert.Contains("Windows: reported=2 totalMatches=2 limit=50", result.StandardOutput);
            Assert.Contains("Window 1: line=6 opcode=2227", result.StandardOutput);
            Assert.Contains("      4 | owner before 2227 A", result.StandardOutput);
            Assert.Contains(">      6 | catch error in process msg:2227", result.StandardOutput);
            Assert.Contains("      8 | owner after 2227 A", result.StandardOutput);
            Assert.Contains("Window 2: line=12 opcode=2227", result.StandardOutput);
            Assert.Contains("     10 | owner before 2227 B", result.StandardOutput);
            Assert.Contains(">     12 | catch error in process msg:2227", result.StandardOutput);
            Assert.Contains("     14 | owner after 2227 B", result.StandardOutput);
            Assert.DoesNotContain("old owner", result.StandardOutput);
            Assert.DoesNotContain("line=9 opcode=2200", result.StandardOutput);
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
    public async Task AcceptsRepeatedOpcodeFiltersInDeterministicOrder()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-packet-catch-context-audit-opcodes-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "tqan.log");

        try
        {
            await WriteFixtureFileAsync(
                logPath,
                """
                before
                catch error in process msg:2227
                middle
                catch error in process msg:2176
                after
                """);

            var result = await RunDataToolAsync(
                "client-packet-catch-context-audit",
                logPath,
                "--opcode",
                "2227",
                "--opcode",
                "2176");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Opcode filter: 2176, 2227", result.StandardOutput);
            Assert.Contains("Windows: reported=2 totalMatches=2 limit=50", result.StandardOutput);
            Assert.Contains("Window 1: line=2 opcode=2227", result.StandardOutput);
            Assert.Contains("Window 2: line=4 opcode=2176", result.StandardOutput);
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
    public async Task HelpIncludesClientPacketCatchContextAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-packet-catch-context-audit <clientDebugLogPath> [--since-line N] [--opcode opcode] [...]",
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
