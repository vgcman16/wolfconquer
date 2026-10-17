using System.Text.Json;

namespace WolfConquer.Tests;

public sealed class DataToolLegacyWorldCaptureCompareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public async Task HelpMentionsLegacyWorldCaptureCompareCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool legacy-world-capture-compare <expectedJson> <actualJson> [--ignore-indexes csv] [--start index] [--count n] [--actual-offset-start expectedIndex] [--actual-offset delta]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task LegacyWorldCaptureCompareReturnsZeroForIdenticalCaptures()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var expectedPath = Path.Combine(tempRoot, "expected.json");
            var actualPath = Path.Combine(tempRoot, "actual.json");
            await WriteCaptureAsync(expectedPath, CreateFrames());
            await WriteCaptureAsync(actualPath, CreateFrames());

            var result = await RunDataToolAsync(
                "legacy-world-capture-compare",
                expectedPath,
                actualPath,
                "--start",
                "0",
                "--count",
                "3");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Legacy world capture compare:", result.StandardOutput);
            Assert.Contains(expectedPath, result.StandardOutput);
            Assert.Contains(actualPath, result.StandardOutput);
            Assert.Contains("Compared range:", result.StandardOutput);
            Assert.Contains("Frame counts:", result.StandardOutput);
            Assert.Contains("Actual offset: <none>", result.StandardOutput);
            Assert.Contains("FirstDiff: -1", result.StandardOutput);
            Assert.Contains("Matched: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task LegacyWorldCaptureCompareReturnsTwoAndPrintsFirstDiffDetails()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var expectedPath = Path.Combine(tempRoot, "expected.json");
            var actualPath = Path.Combine(tempRoot, "actual.json");
            await WriteCaptureAsync(expectedPath, CreateFrames());
            await WriteCaptureAsync(
                actualPath,
                new CaptureFrame(0, 2176, 16, "10000000"),
                new CaptureFrame(1, 2001, 28, "aabbccdd"),
                new CaptureFrame(2, 2179, 20, "12000000"));

            var result = await RunDataToolAsync("legacy-world-capture-compare", expectedPath, actualPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Legacy world capture compare:", result.StandardOutput);
            Assert.Contains("FirstDiff: 1", result.StandardOutput);
            Assert.Contains("Matched: False", result.StandardOutput);
            Assert.Contains("opcode=2120", result.StandardOutput);
            Assert.Contains("opcode=2001", result.StandardOutput);
            Assert.Contains("length=24", result.StandardOutput);
            Assert.Contains("length=28", result.StandardOutput);
            Assert.Contains("firstHex=11000000", result.StandardOutput);
            Assert.Contains("firstHex=aabbccdd", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task LegacyWorldCaptureCompareAppliesActualOffsetAfterConfiguredExpectedIndex()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var expectedPath = Path.Combine(tempRoot, "expected.json");
            var actualPath = Path.Combine(tempRoot, "actual.json");
            await WriteCaptureAsync(expectedPath, CreateSequentialFrames(10));
            await WriteCaptureAsync(actualPath, CreateCaptureWithInsertedActualFrames());

            var result = await RunDataToolAsync(
                "legacy-world-capture-compare",
                expectedPath,
                actualPath,
                "--actual-offset-start",
                "5",
                "--actual-offset",
                "15",
                "--ignore-indexes",
                "7");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Actual offset: start=5 delta=15", result.StandardOutput);
            Assert.Contains("Ignored indexes: 7", result.StandardOutput);
            Assert.Contains("Compared frames: 9", result.StandardOutput);
            Assert.Contains("FirstDiff: -1", result.StandardOutput);
            Assert.Contains("Matched: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task LegacyWorldCaptureCompareShiftedCaptureFailsWithoutActualOffset()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var expectedPath = Path.Combine(tempRoot, "expected.json");
            var actualPath = Path.Combine(tempRoot, "actual.json");
            await WriteCaptureAsync(expectedPath, CreateSequentialFrames(10));
            await WriteCaptureAsync(actualPath, CreateCaptureWithInsertedActualFrames());

            var result = await RunDataToolAsync(
                "legacy-world-capture-compare",
                expectedPath,
                actualPath,
                "--ignore-indexes",
                "7");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Actual offset: <none>", result.StandardOutput);
            Assert.Contains("Ignored indexes: 7", result.StandardOutput);
            Assert.Contains("FirstDiff: 5", result.StandardOutput);
            Assert.Contains("ExpectedDiff: index=5 opcode=2105 length=21 firstHex=10000005", result.StandardOutput);
            Assert.Contains("ActualDiff: index=5 opcode=3005 length=37 firstHex=20000005", result.StandardOutput);
            Assert.Contains("Matched: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task LegacyWorldCaptureCompareIgnoresConfiguredFrameIndexes()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var expectedPath = Path.Combine(tempRoot, "expected.json");
            var actualPath = Path.Combine(tempRoot, "actual.json");
            await WriteCaptureAsync(expectedPath, CreateFrames());
            await WriteCaptureAsync(
                actualPath,
                new CaptureFrame(0, 2176, 16, "10000000"),
                new CaptureFrame(1, 2001, 28, "aabbccdd"),
                new CaptureFrame(2, 2179, 20, "12000000"));

            var result = await RunDataToolAsync(
                "legacy-world-capture-compare",
                expectedPath,
                actualPath,
                "--ignore-indexes",
                "1");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Legacy world capture compare:", result.StandardOutput);
            Assert.Contains("FirstDiff: -1", result.StandardOutput);
            Assert.Contains("Matched: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static CaptureFrame[] CreateFrames()
    {
        return
        [
            new CaptureFrame(0, 2176, 16, "10000000"),
            new CaptureFrame(1, 2120, 24, "11000000"),
            new CaptureFrame(2, 2179, 20, "12000000"),
        ];
    }

    private static CaptureFrame[] CreateSequentialFrames(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => new CaptureFrame(
                index,
                2100 + index,
                16 + index,
                "100000" + index.ToString("x2")))
            .ToArray();
    }

    private static CaptureFrame[] CreateCaptureWithInsertedActualFrames()
    {
        return
        [
            .. CreateSequentialFrames(5),
            .. Enumerable.Range(5, 15)
                .Select(index => new CaptureFrame(
                    index,
                    3000 + index,
                    32 + index,
                    "200000" + index.ToString("x2"))),
            .. Enumerable.Range(5, 5)
                .Select(index => new CaptureFrame(
                    index + 15,
                    2100 + index,
                    16 + index,
                    "100000" + index.ToString("x2"))),
        ];
    }

    private static async Task WriteCaptureAsync(string path, params CaptureFrame[] frames)
    {
        var capture = new
        {
            startedAtUtc = "2026-06-05T00:00:00Z",
            completedAtUtc = "2026-06-05T00:00:01Z",
            address = "127.0.0.1",
            port = 5818,
            accountKey = 1_293_600,
            maxFrames = frames.Length,
            sampleBytes = 4,
            answerOkSeen = true,
            status = "max-frames",
            frames = frames.Select(frame => new
            {
                timestampUtc = "2026-06-05T00:00:00Z",
                frame.Index,
                frame.Opcode,
                frame.Length,
                payloadLength = Math.Max(0, frame.Length - 8),
                sampleLength = frame.FirstHex.Length / 2,
                frame.FirstHex,
                firstBase64 = Convert.ToBase64String(Convert.FromHexString(frame.FirstHex)),
                isAnswerOk = false,
            }).ToArray(),
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(capture, JsonOptions));
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-legacy-world-capture-compare-" + Guid.NewGuid().ToString("N"));
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

    private sealed record CaptureFrame(int Index, int Opcode, int Length, string FirstHex);

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
