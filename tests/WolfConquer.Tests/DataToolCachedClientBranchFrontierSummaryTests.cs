using System.Runtime.CompilerServices;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed class DataToolCachedClientBranchFrontierSummaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    [Fact]
    public async Task HelpMentionsCachedClientBranchFrontierSummaryCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool cached-client-branch-frontier-summary <patchResultJsonPath> [patchResultJsonPath ...] [--output-json path]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task CachedClientBranchFrontierSummaryReportsCompactArtifactRowsAndAggregate()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "frontier-a.json");
            var secondPath = Path.Combine(tempRoot, "frontier-b.json");
            await WriteFrontierArtifactAsync(
                firstPath,
                moduleMemoryProbes:
                [
                    DataThreadProbe("frontier-a", "0x1000", "0x10001000", "E9 0B 00 00 00 EB 19"),
                    LicenseProbe(),
                ],
                accepted: true,
                relayStatus: "response-received");
            await WriteFrontierArtifactAsync(
                secondPath,
                moduleMemoryProbes:
                [
                    DataThreadProbe("frontier-b", "0x1000", "0x10001000", "E9 0B 00 00 00"),
                ],
                accepted: true,
                relayStatus: "response-received");

            var result = await RunDataToolAsync("cached-client-branch-frontier-summary", firstPath, secondPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Cached client branch frontier summary:", result.StandardOutput);
            Assert.Contains($"Path: {firstPath}", result.StandardOutput);
            Assert.Contains(
                "Ready=True Accepted=True RelayStatus=response-received RelayRequestSentBytes=208 " +
                "RelayResponseBytes=445 ResponseSentBytes=445 ProcessRespondingAfterObserve=True " +
                "WindowCount=1 ModuleProbeCount=2 HotInstructionRva=0x342B8D",
                result.StandardOutput);
            Assert.Contains(
                "BranchTargetRvas=DataThread.dll+0x1010, DataThread.dll+0x1020",
                result.StandardOutput);
            Assert.Contains($"Path: {secondPath}", result.StandardOutput);
            Assert.Contains("ModuleProbeCount=1 HotInstructionRva=0x342B8D", result.StandardOutput);
            Assert.Contains("BranchTargetRvas=DataThread.dll+0x1010", result.StandardOutput);
            Assert.Contains("Aggregate:", result.StandardOutput);
            Assert.Contains(
                "RecurrentBranchTargetRvas=DataThread.dll+0x1010 count=2",
                result.StandardOutput);
            Assert.Contains(
                "NextProbeCandidates=DataThread.dll+0x1010 count=2",
                result.StandardOutput);
            Assert.DoesNotContain("license.dll+0x1010", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientBranchFrontierSummaryWritesBoundedSchemaVersionOneJsonReportAtomically()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "frontier-a.json");
            var secondPath = Path.Combine(tempRoot, "frontier-b.json");
            var outputDirectory = Path.Combine(tempRoot, "reports");
            var outputPath = Path.Combine(outputDirectory, "frontier-summary.json");
            await WriteFrontierArtifactAsync(
                firstPath,
                moduleMemoryProbes:
                [
                    DataThreadProbe("frontier-a", "0x1000", "0x10001000", "E9 0B 00 00 00 EB 19"),
                    LicenseProbe(),
                ],
                accepted: true,
                relayStatus: "response-received");
            await WriteFrontierArtifactAsync(
                secondPath,
                moduleMemoryProbes:
                [
                    DataThreadProbe("frontier-b", "0x1000", "0x10001000", "E9 0B 00 00 00"),
                ],
                accepted: true,
                relayStatus: "response-received");
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(outputPath, "stale");

            var result = await RunDataToolAsync(
                "cached-client-branch-frontier-summary",
                firstPath,
                "--output-json",
                outputPath,
                secondPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Output JSON: {outputPath}", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
            Assert.Empty(Directory.EnumerateFiles(outputDirectory, "*.tmp"));

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;
            Assert.Equal(1, root.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal(2, root.GetProperty("ArtifactCount").GetInt32());

            var limits = root.GetProperty("Limits");
            Assert.Equal(64, limits.GetProperty("MaximumArtifacts").GetInt32());
            Assert.Equal(64, limits.GetProperty("MaximumEvidenceValuesPerArtifact").GetInt32());
            Assert.Equal(64, limits.GetProperty("MaximumAggregateTargets").GetInt32());
            Assert.Equal(8, limits.GetProperty("MaximumNextProbeCandidates").GetInt32());
            Assert.Equal(1024 * 1024, limits.GetProperty("MaximumSerializedBytes").GetInt32());

            var artifacts = root.GetProperty("Artifacts");
            Assert.Equal(2, artifacts.GetArrayLength());
            var firstArtifact = artifacts[0];
            Assert.Equal(firstPath, firstArtifact.GetProperty("Path").GetString());
            Assert.True(firstArtifact.GetProperty("Ready").GetBoolean());
            Assert.Equal(2, firstArtifact.GetProperty("BranchTargetRvaCount").GetInt32());
            Assert.False(firstArtifact.GetProperty("BranchTargetRvasTruncated").GetBoolean());
            Assert.Equal(
                ["DataThread.dll+0x1010", "DataThread.dll+0x1020"],
                firstArtifact.GetProperty("BranchTargetRvas")
                    .EnumerateArray()
                    .Select(value => value.GetString()!)
                    .ToArray());
            Assert.Equal(1, firstArtifact.GetProperty("ProbedRvaCount").GetInt32());
            Assert.Equal(
                ["DataThread.dll+0x1000"],
                firstArtifact.GetProperty("ProbedRvas")
                    .EnumerateArray()
                    .Select(value => value.GetString()!)
                    .ToArray());

            var aggregate = root.GetProperty("Aggregate");
            Assert.Equal(2, aggregate.GetProperty("BranchTargetRvaCount").GetInt32());
            Assert.Equal(1, aggregate.GetProperty("RecurrentBranchTargetRvaCount").GetInt32());
            Assert.Equal(
                "DataThread.dll+0x1010",
                aggregate.GetProperty("RecurrentBranchTargetRvas")[0].GetProperty("Target").GetString());
            Assert.Equal(
                2,
                aggregate.GetProperty("RecurrentBranchTargetRvas")[0].GetProperty("Count").GetInt32());
            Assert.Equal(1, aggregate.GetProperty("UnprobedRecurrentTargetCount").GetInt32());
            Assert.Equal(
                "DataThread.dll+0x1010",
                aggregate.GetProperty("UnprobedRecurrentCandidates")[0].GetProperty("Target").GetString());
            Assert.Equal(0, aggregate.GetProperty("ExhaustedRecurrentTargetCount").GetInt32());
            Assert.False(aggregate.GetProperty("RecurrentTargetsExhausted").GetBoolean());
            Assert.Equal(
                "unprobed-recurrent-candidates",
                aggregate.GetProperty("CandidateStatus").GetString());
            Assert.Equal(0, aggregate.GetProperty("FallbackDisplayTargets").GetArrayLength());
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientBranchFrontierSummaryReportsExhaustedRecurrentTargetsWithoutSuggestingAnotherProbe()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "frontier-a.json");
            var secondPath = Path.Combine(tempRoot, "frontier-b.json");
            var outputPath = Path.Combine(tempRoot, "frontier-summary.json");
            await WriteFrontierArtifactAsync(
                firstPath,
                moduleMemoryProbes:
                [
                    DataThreadProbe("frontier-a", "0x1000", "0x10001000", "E9 0B 00 00 00"),
                    DataThreadProbe("already-probed", "0x1010", "0x10001010", "90"),
                ],
                accepted: true,
                relayStatus: "response-received");
            await WriteFrontierArtifactAsync(
                secondPath,
                moduleMemoryProbes:
                [
                    DataThreadProbe("frontier-b", "0x1000", "0x10001000", "E9 0B 00 00 00"),
                ],
                accepted: true,
                relayStatus: "response-received");

            var result = await RunDataToolAsync(
                "cached-client-branch-frontier-summary",
                firstPath,
                secondPath,
                "--output-json",
                outputPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "RecurrentBranchTargetRvas=DataThread.dll+0x1010 count=2",
                result.StandardOutput);
            Assert.Contains("NextProbeCandidates=<none>", result.StandardOutput);
            Assert.DoesNotContain(
                "NextProbeCandidates=DataThread.dll+0x1010",
                result.StandardOutput);
            Assert.Contains("RecurrentTargetsExhausted=True", result.StandardOutput);
            Assert.Contains(
                "ExhaustedRecurrentTargets=DataThread.dll+0x1010 count=2",
                result.StandardOutput);
            Assert.Contains(
                "FallbackDisplayTargets=DataThread.dll+0x1010 count=2",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var aggregate = document.RootElement.GetProperty("Aggregate");
            Assert.Equal(0, aggregate.GetProperty("UnprobedRecurrentTargetCount").GetInt32());
            Assert.Equal(0, aggregate.GetProperty("UnprobedRecurrentCandidates").GetArrayLength());
            Assert.Equal(1, aggregate.GetProperty("ExhaustedRecurrentTargetCount").GetInt32());
            Assert.Equal(
                "DataThread.dll+0x1010",
                aggregate.GetProperty("ExhaustedRecurrentTargets")[0].GetProperty("Target").GetString());
            Assert.True(aggregate.GetProperty("RecurrentTargetsExhausted").GetBoolean());
            Assert.Equal(
                "recurrent-targets-exhausted",
                aggregate.GetProperty("CandidateStatus").GetString());
            Assert.Equal(
                "DataThread.dll+0x1010",
                aggregate.GetProperty("FallbackDisplayTargets")[0].GetProperty("Target").GetString());
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientBranchFrontierSummaryRejectsUnknownOrIncompleteOptions()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var path = Path.Combine(tempRoot, "frontier.json");
            await WriteFrontierArtifactAsync(
                path,
                moduleMemoryProbes:
                [
                    DataThreadProbe("frontier", "0x1000", "0x10001000", "E9 0B 00 00 00"),
                ],
                accepted: true,
                relayStatus: "response-received");

            var unknown = await RunDataToolAsync(
                "cached-client-branch-frontier-summary",
                path,
                "--bogus");
            var incomplete = await RunDataToolAsync(
                "cached-client-branch-frontier-summary",
                path,
                "--output-json");

            Assert.Equal(1, unknown.ExitCode);
            Assert.Equal(string.Empty, unknown.StandardOutput);
            Assert.Contains(
                "Unknown cached-client-branch-frontier-summary option: --bogus",
                unknown.StandardError);
            Assert.Equal(1, incomplete.ExitCode);
            Assert.Equal(string.Empty, incomplete.StandardOutput);
            Assert.Contains("Missing value for --output-json.", incomplete.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientBranchFrontierSummaryRejectsBadAndInputCollidingPaths()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var path = Path.Combine(tempRoot, "frontier.json");
            var missingPath = Path.Combine(tempRoot, "missing.json");
            await WriteFrontierArtifactAsync(
                path,
                moduleMemoryProbes:
                [
                    DataThreadProbe("frontier", "0x1000", "0x10001000", "E9 0B 00 00 00"),
                ],
                accepted: true,
                relayStatus: "response-received");
            var originalArtifact = await File.ReadAllTextAsync(path);

            var missing = await RunDataToolAsync(
                "cached-client-branch-frontier-summary",
                missingPath);
            var collision = await RunDataToolAsync(
                "cached-client-branch-frontier-summary",
                path,
                "--output-json",
                path);

            Assert.Equal(1, missing.ExitCode);
            Assert.Equal(string.Empty, missing.StandardOutput);
            Assert.Contains($"Patch result JSON not found: {missingPath}", missing.StandardError);
            Assert.Equal(1, collision.ExitCode);
            Assert.Equal(string.Empty, collision.StandardOutput);
            Assert.Contains(
                "Output JSON path must not overwrite an input patch result JSON.",
                collision.StandardError);
            Assert.Equal(originalArtifact, await File.ReadAllTextAsync(path));
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientBranchFrontierSummaryKeepsBranchTargetsWhenHotThreadEvidenceIsMissing()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var path = Path.Combine(tempRoot, "probe-only.json");
            await WriteFrontierArtifactAsync(
                path,
                moduleMemoryProbes:
                [
                    DataThreadProbe("probe-only", "0x2000", "0x10002000", "EB 0E"),
                ],
                accepted: null,
                relayStatus: null,
                includeThreadEvidence: false,
                processRespondingAfterObserve: false);

            var result = await RunDataToolAsync("cached-client-branch-frontier-summary", path);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Path: {path}", result.StandardOutput);
            Assert.Contains(
                "Ready=False Accepted=<missing> RelayStatus=<missing> RelayRequestSentBytes=<missing> " +
                "RelayResponseBytes=<missing> ResponseSentBytes=<missing> ProcessRespondingAfterObserve=False " +
                "WindowCount=1 ModuleProbeCount=1 HotInstructionRva=<missing>",
                result.StandardOutput);
            Assert.Contains("BranchTargetRvas=DataThread.dll+0x2010", result.StandardOutput);
            Assert.Contains("RecurrentBranchTargetRvas=<none>", result.StandardOutput);
            Assert.Contains("NextProbeCandidates=<none>", result.StandardOutput);
            Assert.Contains("RecurrentTargetsExhausted=False", result.StandardOutput);
            Assert.Contains(
                "FallbackDisplayTargets=DataThread.dll+0x2010 count=1",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static async Task WriteFrontierArtifactAsync(
        string path,
        object[] moduleMemoryProbes,
        bool? accepted,
        string? relayStatus,
        bool includeThreadEvidence = true,
        bool processRespondingAfterObserve = true)
    {
        var payload = new Dictionary<string, object?>
        {
            ["ProcessId"] = 1234,
            ["ModuleEvidenceAfterObserve"] = new object[]
            {
                new
                {
                    ModuleName = "DataThread.dll",
                    FileName = @"C:\fixtures\DataThread.dll",
                    BaseAddress = "0x10000000",
                    ModuleMemorySize = 0x500000,
                },
                new
                {
                    ModuleName = "license.dll",
                    FileName = @"C:\fixtures\license.dll",
                    BaseAddress = "0x20000000",
                    ModuleMemorySize = 0x100000,
                },
            },
            ["ModuleMemoryProbesAfterObserve"] = moduleMemoryProbes,
            ["ProcessRespondingAfterObserve"] = processRespondingAfterObserve,
            ["WindowEvidenceAfterObserve"] = new object[]
            {
                new
                {
                    Title = "Default IME",
                    ClassName = "IME",
                    Visible = false,
                },
            },
        };

        if (accepted.HasValue)
        {
            payload["Accepted"] = accepted.Value;
        }

        if (relayStatus is not null)
        {
            payload["RelayStatus"] = relayStatus;
            payload["RelayRequestSentBytes"] = 208;
            payload["RelayResponseBytes"] = 445;
            payload["ResponseSentBytes"] = 445;
        }

        if (includeThreadEvidence)
        {
            payload["ThreadEvidenceAfterObserve"] = new object[]
            {
                new
                {
                    ProcessId = 1234,
                    ThreadId = 44,
                    ThreadState = "Running",
                    InstructionPointer = "0x10342B8D",
                    InstructionModuleName = "DataThread.dll",
                    InstructionBaseAddress = "0x10000000",
                    InstructionRva = "0x342B8D",
                    InstructionBytes = "5C E9 0F 01 00 00 50 51 B9 00 10 00 20",
                    StackPointer = "0x1AF87E",
                    Eax = "0x1",
                    Ebx = "0x2",
                    Ecx = "0x3",
                    Edx = "0x4",
                    Ebp = "0x5",
                    TotalProcessorTimeMs = 100,
                },
            };
        }

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static object DataThreadProbe(string name, string rva, string address, string hex)
    {
        return new
        {
            Name = name,
            ModuleName = "DataThread.dll",
            ModuleFileName = @"C:\fixtures\DataThread.dll",
            Rva = rva,
            Length = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            Address = address,
            Read = true,
            Hex = hex,
            FailureReason = string.Empty,
        };
    }

    private static object LicenseProbe()
    {
        return new
        {
            Name = "license-state",
            ModuleName = "license.dll",
            ModuleFileName = @"C:\fixtures\license.dll",
            Rva = "0x1000",
            Length = 5,
            Address = "0x20001000",
            Read = true,
            Hex = "E9 0B 00 00 00",
            FailureReason = string.Empty,
        };
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-cached-client-branch-frontier-summary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
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
