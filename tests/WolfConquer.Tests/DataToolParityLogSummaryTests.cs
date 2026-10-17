using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolParityLogSummaryTests
{
    [Fact]
    public async Task FileInputSummarizesCategoriesAndStablePrefixes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-parity-log-summary-file-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "client.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                logPath,
                """
                [2026-06-11T01:00:00Z INF] PARITY-GAP world click opcode=10010 not modeled player=123 session=alpha
                [2026-06-11T01:00:01Z INF] PARITY-GAP world click opcode=10010 not modeled player=456 session=beta
                PARITY-GAP category=npc severity=warning gap=clicked-npc dedupe=npc:clicked-npc:npcId=999001:opcode=2084 npcId=999001 opcode=2084; clicked NPC is not modeled
                PARITY-BUG world runtime exception opcode=2135 exception=InvalidOperationException
                [WRN] deferred asset load path=ini/GameMap.ini
                [DBG] skipped optional animation path=ani/Missing.ani
                [DBG] normal debug line without a parity marker
                """,
                Encoding.UTF8);

            var result = await RunDataToolAsync("parity-log-summary", logPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Parity log summary:", result.StandardOutput);
            Assert.Contains($"Path: {Path.GetFullPath(logPath)}", result.StandardOutput);
            Assert.Contains("Scanned files: 1", result.StandardOutput);
            Assert.Contains("Matching lines: 6", result.StandardOutput);
            Assert.Contains("  PARITY-BUG: 1", result.StandardOutput);
            Assert.Contains("  PARITY-GAP: 3", result.StandardOutput);
            Assert.Contains("  category:npc: 1", result.StandardOutput);
            Assert.Contains("  deferred: 1", result.StandardOutput);
            Assert.Contains("  exception: 1", result.StandardOutput);
            Assert.Contains("  gap:clicked-npc: 1", result.StandardOutput);
            Assert.Contains("  skipped: 1", result.StandardOutput);
            Assert.Contains("  severity:warning: 1", result.StandardOutput);
            Assert.Contains("  not modeled: 3", result.StandardOutput);
            Assert.Contains(
                "  2: PARITY-GAP world click opcode=10010 not modeled player=# session=#",
                result.StandardOutput);
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
    public async Task FileInputReadsLogWhileWriterKeepsFileOpen()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-parity-log-summary-live-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "world.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await using var stream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteLineAsync("PARITY-GAP active runtime log still open");
            await writer.FlushAsync();

            var result = await RunDataToolAsync("parity-log-summary", logPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Scanned files: 1", result.StandardOutput);
            Assert.Contains("Matching lines: 1", result.StandardOutput);
            Assert.Contains("  PARITY-GAP: 1", result.StandardOutput);
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
    public async Task DirectoryInputScansLogFilesRecursivelyAndOrdersTiedPrefixes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-parity-log-summary-dir-" + Guid.NewGuid().ToString("N"));
        var nestedRoot = Path.Combine(tempRoot, "nested");

        try
        {
            Directory.CreateDirectory(nestedRoot);
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "z.log"), "[ERR] exception beta handler failed", Encoding.UTF8);
            await File.WriteAllTextAsync(Path.Combine(nestedRoot, "a.log"), "[ERR] exception alpha handler failed", Encoding.UTF8);
            await File.WriteAllTextAsync(Path.Combine(nestedRoot, "ignored.txt"), "rejected text fixture", Encoding.UTF8);

            var result = await RunDataToolAsync("parity-log-summary", tempRoot, "--top", "2");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Path: {Path.GetFullPath(tempRoot)}", result.StandardOutput);
            Assert.Contains("Scanned files: 2", result.StandardOutput);
            Assert.Contains("Matching lines: 2", result.StandardOutput);
            Assert.Contains("  exception: 2", result.StandardOutput);
            Assert.DoesNotContain("rejected:", result.StandardOutput);

            var alphaIndex = result.StandardOutput.IndexOf("  1: exception alpha handler failed", StringComparison.Ordinal);
            var betaIndex = result.StandardOutput.IndexOf("  1: exception beta handler failed", StringComparison.Ordinal);
            Assert.True(alphaIndex >= 0, "The alpha prefix should be present.");
            Assert.True(betaIndex >= 0, "The beta prefix should be present.");
            Assert.True(alphaIndex < betaIndex, "Tied prefixes should be ordered deterministically.");
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
    public async Task TopOptionLimitsRepeatedMessagePrefixes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-parity-log-summary-top-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "runtime.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                logPath,
                """
                malformed inventory packet action=Alpha player=1
                malformed inventory packet action=Alpha player=2
                malformed inventory packet action=Beta player=3
                rejected teleport opcode=9999
                """,
                Encoding.UTF8);

            var result = await RunDataToolAsync("parity-log-summary", logPath, "--top", "1");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Matching lines: 4", result.StandardOutput);
            Assert.Contains("  malformed: 3", result.StandardOutput);
            Assert.Contains("  rejected: 1", result.StandardOutput);
            Assert.Contains("  2: malformed inventory packet action=Alpha player=#", result.StandardOutput);
            Assert.DoesNotContain("malformed inventory packet action=Beta", result.StandardOutput);
            Assert.DoesNotContain("rejected teleport opcode=9999", result.StandardOutput);
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
    public async Task MissingPathReturnsNonZeroWithClearError()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-parity-log-summary-missing-" + Guid.NewGuid().ToString("N"),
            "missing.log");

        var result = await RunDataToolAsync("parity-log-summary", missingPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains($"Parity log path not found: {Path.GetFullPath(missingPath)}", result.StandardError);
    }

    [Fact]
    public async Task HelpIncludesParityLogSummaryUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool parity-log-summary <logPathOrDirectory> [--top N]",
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

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
