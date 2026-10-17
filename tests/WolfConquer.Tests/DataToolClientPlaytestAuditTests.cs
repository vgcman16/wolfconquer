using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WolfConquer.Tests;

public sealed class DataToolClientPlaytestAuditTests
{
    [Fact]
    public async Task SinceLineReportsCriticalClientCatchesAndServerCounts()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-playtest-audit-" + Guid.NewGuid().ToString("N"));
        var clientLog = Path.Combine(tempRoot, "debug", "2026_7_4.log");
        var serverLog = Path.Combine(tempRoot, "world.log");

        try
        {
            await WriteFixtureFileAsync(
                clientLog,
                """
                catch error in process msg:2176 -- Sat Jul  4 13:59:58 2026
                =========Lua error: old launch noise -- Sat Jul  4 13:59:59 2026
                catch error in process msg:2200 -- Sat Jul  4 14:00:01 2026
                =========Lua error: current playtest issue -- Sat Jul  4 14:00:02 2026
                texture [61409] not found! -- Sat Jul 04 14:00:03 2026
                Error: CreateNewAni(ani/Control.ani, hero_pro_7) failed. -- Sat Jul  4 14:00:04 2026
                Ani Index [PirateJX_ui5] Not Found In [ani/Control.ani] -- Sat Jul  4 14:00:05 2026
                WARNING: Read Error in ItemType.dat, itemtypeID=4039109 -- Sat Jul  4 14:00:06 2026
                CHECKF(nAmount > 0 && nAmount <= E_ATTR_MAX) in e:\cqclient\cqclient-161.4\myshell\itemtipbase.cpp, 199 -- Sat Jul  4 14:00:06 2026
                catch error in process msg:9999 -- Sat Jul  4 14:00:07 2026
                """);
            await WriteFixtureFileAsync(
                serverLog,
                """
                Runtime error log file: C:\temp\WorldServer.err.log
                [14:00:00 INF] stack ready
                [14:00:01 WRN] Unsupported packet 1234
                [14:00:02 WRN] Unhandled frame 2200
                [14:00:03 ERR] Error: failed handler
                """);

            var result = await RunDataToolAsync(
                "client-playtest-audit",
                clientLog,
                "--since-line",
                "3",
                "--server-log",
                serverLog);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Client playtest audit:", result.StandardOutput);
            Assert.Contains($"Client log: {Path.GetFullPath(clientLog)}", result.StandardOutput);
            Assert.Contains("Since: <none>", result.StandardOutput);
            Assert.Contains("Since line: 3", result.StandardOutput);
            Assert.Contains("Fail on critical catch: True", result.StandardOutput);
            Assert.Contains("Client lines: scanned=10 included=8", result.StandardOutput);
            Assert.Contains("Client catch errors: 2", result.StandardOutput);
            Assert.Contains("opcode 2200 count=1 firstLine=3 lastLine=3 critical=True", result.StandardOutput);
            Assert.Contains("opcode 9999 count=1 firstLine=10 lastLine=10 critical=False", result.StandardOutput);
            Assert.DoesNotContain("opcode 2176 count=1", result.StandardOutput);
            Assert.Contains("Critical client catches: 1 opcodes=2200,2176", result.StandardOutput);
            Assert.Contains("Lua errors: 1", result.StandardOutput);
            Assert.Contains("Missing textures: 1", result.StandardOutput);
            Assert.Contains("ANI warnings: 2", result.StandardOutput);
            Assert.Contains("ItemType read errors: 1", result.StandardOutput);
            Assert.Contains("Client check/assert lines: 1", result.StandardOutput);
            Assert.Contains(
                "Client check/assert previews: unique=1 first20=CHECKF(nAmount > 0 && nAmount <= E_ATTR_MAX) in itemtipbase.cpp, 199",
                result.StandardOutput);
            Assert.Contains("Client symptom hint lines: 0", result.StandardOutput);
            Assert.Contains("Client symptom hint previews: unique=0 first20=<none>", result.StandardOutput);
            Assert.DoesNotContain("Missing texture ids all:", result.StandardOutput);
            Assert.DoesNotContain("ItemType read error ids all:", result.StandardOutput);
            Assert.DoesNotContain("Missing string keys all:", result.StandardOutput);
            Assert.DoesNotContain("Client check/assert previews all:", result.StandardOutput);
            Assert.DoesNotContain("Client symptom hint previews all:", result.StandardOutput);
            Assert.Contains("Server logs: 1", result.StandardOutput);
            Assert.Contains("Server unhandled/unsupported/error lines: unhandled=1 unsupported=1 errors=1", result.StandardOutput);
            Assert.Contains($"{Path.GetFullPath(serverLog)}: scanned=5 included=4 unhandled=1 unsupported=1 errors=1", result.StandardOutput);
            Assert.Contains("Result: critical client catches found.", result.StandardOutput);
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
    public async Task SinceTimestampAndNoFailOnCatchAllowsCriticalCatchProof()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-playtest-audit-since-" + Guid.NewGuid().ToString("N"));
        var clientLog = Path.Combine(tempRoot, "debug", "2026_7_4.log");
        var serverLog = Path.Combine(tempRoot, "world.log");

        try
        {
            await WriteFixtureFileAsync(
                clientLog,
                """
                catch error in process msg:2200 -- Sat Jul  4 13:59:59 2026
                catch error in process msg:2176 -- Sat Jul  4 14:00:00 2026
                stack detail without its own timestamp
                =========Lua error: current issue -- Sat Jul  4 14:00:01 2026
                """);
            await WriteFixtureFileAsync(
                serverLog,
                """
                2026-07-04 13:59:59 Error: before cutoff
                2026-07-04 14:00:01 Unsupported world action
                2026-07-04 14:00:02 Error: after cutoff
                """);

            var result = await RunDataToolAsync(
                "client-playtest-audit",
                clientLog,
                "--since",
                "Sat Jul  4 14:00:00 2026",
                "--server-log",
                serverLog,
                "--no-fail-on-catch");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Since: 2026-07-04 14:00:00", result.StandardOutput);
            Assert.Contains("Fail on critical catch: False", result.StandardOutput);
            Assert.Contains("Client lines: scanned=4 included=3", result.StandardOutput);
            Assert.Contains("Client catch errors: 1", result.StandardOutput);
            Assert.Contains("opcode 2176 count=1 firstLine=2 lastLine=2 critical=True", result.StandardOutput);
            Assert.DoesNotContain("opcode 2200 count=1", result.StandardOutput);
            Assert.Contains("Lua errors: 1", result.StandardOutput);
            Assert.Contains("Server unhandled/unsupported/error lines: unhandled=0 unsupported=1 errors=1", result.StandardOutput);
            Assert.Contains($"{Path.GetFullPath(serverLog)}: scanned=3 included=2 unhandled=0 unsupported=1 errors=1", result.StandardOutput);
            Assert.Contains("Result: critical client catches found, but --no-fail-on-catch is set.", result.StandardOutput);
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
    public async Task SinceLineDeduplicatesItemTypeIdsAndMissingStringKeys()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-playtest-audit-keys-" + Guid.NewGuid().ToString("N"));
        var clientLog = Path.Combine(tempRoot, "debug", "2026_7_4.log");

        try
        {
            await WriteFixtureFileAsync(
                clientLog,
                """
                WARNING: Read Error in ItemType.dat, itemtypeID=111111 -- Sat Jul  4 14:00:00 2026
                CStringManagerW::GetStr() Can't find string[STR_INSTANCE_DESC_OLD]! -- Sat Jul  4 14:00:01 2026
                WARNING: Read Error in ItemType.dat, itemtypeID=222222 -- Sat Jul  4 14:00:02 2026
                WARNING: Read Error in ItemType.dat, itemtypeID=222222 -- Sat Jul  4 14:00:03 2026
                CStringManagerW::GetStr() Can't find string[STR_INSTANCE_DESC_A]! -- Sat Jul  4 14:00:04 2026
                CStringManagerW::GetStr() Can't find string[STR_INSTANCE_DESC_A]! -- Sat Jul  4 14:00:05 2026
                WARNING: Read Error in ItemType.dat, itemtypeID=333333 -- Sat Jul  4 14:00:06 2026
                CStringManagerW::GetStr() Can't find string[STR_INSTANCE_DESC_B]! -- Sat Jul  4 14:00:07 2026
                """);

            var result = await RunDataToolAsync(
                "client-playtest-audit",
                clientLog,
                "--since-line",
                "3");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client lines: scanned=8 included=6", result.StandardOutput);
            Assert.Contains("ItemType read errors: 3", result.StandardOutput);
            Assert.Contains("ItemType read error ids: unique=2 first20=222222, 333333", result.StandardOutput);
            Assert.Contains("Missing string keys: unique=2 first20=STR_INSTANCE_DESC_A, STR_INSTANCE_DESC_B", result.StandardOutput);
            Assert.DoesNotContain("111111", result.StandardOutput);
            Assert.DoesNotContain("STR_INSTANCE_DESC_OLD", result.StandardOutput);
            Assert.Contains("Result: no critical client catches found.", result.StandardOutput);
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
    public async Task ListAllPrintsCompleteUniqueNoisyAssetLists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-playtest-audit-list-all-" + Guid.NewGuid().ToString("N"));
        var clientLog = Path.Combine(tempRoot, "debug", "2026_7_4.log");
        var itemTypeIds = Enumerable.Range(0, 23).Select(index => $"{700000 + index}").ToArray();
        var stringKeys = Enumerable.Range(0, 23).Select(index => $"STR_INSTANCE_DESC_{index:00}").ToArray();
        var textureIds = Enumerable.Range(0, 23).Select(index => $"{810000 + index}").ToArray();

        try
        {
            var lines = new List<string>();
            for (var index = 0; index < itemTypeIds.Length; index++)
            {
                lines.Add($"texture [{textureIds[index]}] not found!");
                lines.Add($"WARNING: Read Error in ItemType.dat, itemtypeID={itemTypeIds[index]}");
                lines.Add($"CStringManagerW::GetStr() Can't find string[{stringKeys[index]}]!");
            }

            lines.Add($"texture [{textureIds[0]}] not found!");
            lines.Add($"WARNING: Read Error in ItemType.dat, itemtypeID={itemTypeIds[0]}");
            lines.Add($"CStringManagerW::GetStr() Can't find string[{stringKeys[0]}]!");

            await WriteFixtureFileAsync(clientLog, string.Join(Environment.NewLine, lines));

            var compactResult = await RunDataToolAsync(
                "client-playtest-audit",
                clientLog);

            Assert.Equal(0, compactResult.ExitCode);
            Assert.Contains($"Missing textures: {textureIds.Length + 1}", compactResult.StandardOutput);
            Assert.Contains($"ItemType read errors: {itemTypeIds.Length + 1}", compactResult.StandardOutput);
            Assert.Contains(
                $"ItemType read error ids: unique={itemTypeIds.Length} first20={string.Join(", ", itemTypeIds.Take(20))}, ...",
                compactResult.StandardOutput);
            Assert.Contains(
                $"Missing string keys: unique={stringKeys.Length} first20={string.Join(", ", stringKeys.Take(20))}, ...",
                compactResult.StandardOutput);
            Assert.DoesNotContain("Missing texture ids all:", compactResult.StandardOutput);
            Assert.DoesNotContain("ItemType read error ids all:", compactResult.StandardOutput);
            Assert.DoesNotContain("Missing string keys all:", compactResult.StandardOutput);
            Assert.DoesNotContain(textureIds[^1], compactResult.StandardOutput);
            Assert.DoesNotContain(itemTypeIds[^1], compactResult.StandardOutput);
            Assert.DoesNotContain(stringKeys[^1], compactResult.StandardOutput);
            Assert.Equal(string.Empty, compactResult.StandardError);

            var fullResult = await RunDataToolAsync(
                "client-playtest-audit",
                clientLog,
                "--list-all");

            Assert.Equal(0, fullResult.ExitCode);
            Assert.Contains(
                $"Missing texture ids all: unique={textureIds.Length} values={string.Join(", ", textureIds)}",
                fullResult.StandardOutput);
            Assert.Contains(
                $"ItemType read error ids all: unique={itemTypeIds.Length} values={string.Join(", ", itemTypeIds)}",
                fullResult.StandardOutput);
            Assert.Contains(
                $"Missing string keys all: unique={stringKeys.Length} values={string.Join(", ", stringKeys)}",
                fullResult.StandardOutput);
            Assert.Equal(string.Empty, fullResult.StandardError);
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
    public async Task SinceLineReportsClientSymptomHintsForBlackScreenAndInvisiblePlaytests()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-playtest-audit-symptoms-" + Guid.NewGuid().ToString("N"));
        var clientLog = Path.Combine(tempRoot, "debug", "2026_7_4.log");

        try
        {
            await WriteFixtureFileAsync(
                clientLog,
                """
                old invisible issue before cutoff -- Sat Jul  4 13:59:59 2026
                black screen after monster attack -- Sat Jul  4 14:00:00 2026
                role render failed for player body -- Sat Jul  4 14:00:01 2026
                monster model not found on TwinCity spawn -- Sat Jul  4 14:00:02 2026
                load map failed for map/1002.dmap -- Sat Jul  4 14:00:03 2026
                unrelated normal line -- Sat Jul  4 14:00:04 2026
                """);

            var result = await RunDataToolAsync(
                "client-playtest-audit",
                clientLog,
                "--since-line",
                "2",
                "--list-all");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client lines: scanned=6 included=5", result.StandardOutput);
            Assert.Contains("Client symptom hint lines: 4", result.StandardOutput);
            Assert.Contains(
                "Client symptom hint previews: unique=4 first20=black screen after monster attack, role render failed for player body, monster model not found on TwinCity spawn, load map failed for map/1002.dmap",
                result.StandardOutput);
            Assert.Contains(
                "Client symptom hint previews all: unique=4 values=black screen after monster attack, role render failed for player body, monster model not found on TwinCity spawn, load map failed for map/1002.dmap",
                result.StandardOutput);
            Assert.DoesNotContain("old invisible issue before cutoff", result.StandardOutput);
            Assert.Contains("Result: no critical client catches found.", result.StandardOutput);
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
    public async Task MissingClientLogReturnsUsageAndExitOne()
    {
        var result = await RunDataToolAsync("client-playtest-audit");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("Missing client debug log path.", result.StandardError);
        Assert.Contains(
            "Usage: WolfConquer.DataTool client-playtest-audit <clientDebugLogPath> [--since timestamp] [--since-line N] [--server-log path] [...] [--list-all] [--no-fail-on-catch]",
            result.StandardError);
    }

    [Fact]
    public async Task HelpIncludesClientPlaytestAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-playtest-audit <clientDebugLogPath> [--since timestamp] [--since-line N] [--server-log path] [...] [--list-all] [--no-fail-on-catch]",
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
        await File.WriteAllTextAsync(path, contents);
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
