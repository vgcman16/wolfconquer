using System.Text.Json;

namespace WolfConquer.Tests;

public sealed class DataToolLicenseRouteCaptureCompareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    [Fact]
    public async Task HelpMentionsLicenseRouteCaptureCompareCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool license-route-capture-compare <captureJsonPath> <captureJsonPath> [captureJsonPath ...]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task LicenseRouteCaptureCompareReportsStableAndDifferentRanges()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "capture-1.json");
            var secondPath = Path.Combine(tempRoot, "capture-2.json");
            var firstBytes = Enumerable.Range(0, 208).Select(index => (byte)index).ToArray();
            var secondBytes = firstBytes.ToArray();
            MutateRange(secondBytes, 0, 12, 0x80);
            MutateRange(secondBytes, 192, 16, 0x40);

            await WriteCaptureAsync(firstPath, firstBytes, "10.0.0.116:55540");
            await WriteCaptureAsync(secondPath, secondBytes, "10.0.0.116:55542");

            var result = await RunDataToolAsync("license-route-capture-compare", firstPath, secondPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("License route capture compare:", result.StandardOutput);
            Assert.Contains("Captures: 2", result.StandardOutput);
            Assert.Contains(firstPath, result.StandardOutput);
            Assert.Contains(secondPath, result.StandardOutput);
            Assert.Contains("First-read lengths: 208,208", result.StandardOutput);
            Assert.Contains("Compared byte span: 0-207", result.StandardOutput);
            Assert.Contains("Stable byte positions: 180/208", result.StandardOutput);
            Assert.Contains("Different byte positions: 28/208", result.StandardOutput);
            Assert.Contains("Stable byte ranges: 12-191", result.StandardOutput);
            Assert.Contains("Different byte ranges: 0-11,192-207", result.StandardOutput);
            Assert.Contains("Length mismatch: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task LicenseRouteCaptureCompareRejectsDeclaredLengthMismatch()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "capture-1.json");
            var secondPath = Path.Combine(tempRoot, "capture-2.json");
            await WriteCaptureAsync(firstPath, [0x01, 0x02]);
            await File.WriteAllTextAsync(
                secondPath,
                JsonSerializer.Serialize(
                    new
                    {
                        FirstReadBytes = 208,
                        FirstReadHex = "01 02",
                    },
                    JsonOptions));

            var result = await RunDataToolAsync("license-route-capture-compare", firstPath, secondPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("License route capture compare failed:", result.StandardError);
            Assert.Contains("FirstReadBytes mismatch", result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static void MutateRange(byte[] bytes, int start, int count, byte xor)
    {
        for (var index = start; index < start + count; index++)
        {
            bytes[index] ^= xor;
        }
    }

    private static async Task WriteCaptureAsync(string path, IReadOnlyList<byte> bytes, string? remote = null)
    {
        var capture = new
        {
            ProcessId = 40248,
            SpawnFile = @"client\Env_DX9\._cache_Conquer.exe",
            SourceAddress = "192.0.2.10",
            TargetAddress = "10.000.000.116",
            Port = 9865,
            Patched = true,
            Accepted = true,
            Remote = remote,
            FirstReadBytes = bytes.Count,
            FirstReadHex = string.Join(" ", bytes.Select(value => value.ToString("X2"))),
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(capture, JsonOptions));
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-license-route-capture-compare-" + Guid.NewGuid().ToString("N"));
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

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
