using System.Text.Json;

namespace WolfConquer.Tests;

public sealed class DataToolLicenseRouteRelayCompareTests
{
    private const string CommandName = "license-route-relay-compare";
    private const string ExpectedUsage =
        "WolfConquer.DataTool license-route-relay-compare <relayCaptureJsonPath> [relayCaptureJsonPath ...]";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public async Task HelpMentionsLicenseRouteRelayCompareCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(ExpectedUsage, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task LicenseRouteRelayCompareRequiresAtLeastOneRelayCapture()
    {
        var result = await RunDataToolAsync(CommandName);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains(ExpectedUsage, result.StandardError);
    }

    [Fact]
    public async Task LicenseRouteRelayCompareReportsClientAndServerStableRanges()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "relay-capture-1.json");
            var secondPath = Path.Combine(tempRoot, "relay-capture-2.json");

            var firstClientBytes = Enumerable.Range(0, 208)
                .Select(index => (byte)index)
                .ToArray();
            var secondClientBytes = firstClientBytes.ToArray();
            MutateRange(secondClientBytes, 0, 12, 0x80);
            MutateRange(secondClientBytes, 192, 16, 0x40);

            var firstServerBytes = Enumerable.Range(0, 445)
                .Select(index => (byte)((index * 7 + 3) & 0xff))
                .ToArray();
            var secondServerBytes = firstServerBytes.ToArray();
            MutateRange(secondServerBytes, 0, 32, 0x20);
            MutateRange(secondServerBytes, 400, 45, 0x10);

            await WriteRelayCaptureAsync(firstPath, firstClientBytes, firstServerBytes, "10.0.0.116:55540");
            await WriteRelayCaptureAsync(secondPath, secondClientBytes, secondServerBytes, "10.0.0.116:55542");

            var result = await RunDataToolAsync(CommandName, firstPath, secondPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("License route relay compare:", result.StandardOutput);
            Assert.Contains("Relay captures: 2", result.StandardOutput);
            Assert.Contains(firstPath, result.StandardOutput);
            Assert.Contains(secondPath, result.StandardOutput);
            Assert.Contains("Client-to-server lengths: 208,208", result.StandardOutput);
            Assert.Contains("Server-to-client lengths: 445,445", result.StandardOutput);
            Assert.Contains("Client stable byte ranges: 12-191", result.StandardOutput);
            Assert.Contains("Client different byte ranges: 0-11,192-207", result.StandardOutput);
            Assert.Contains("Server stable byte ranges: 32-399", result.StandardOutput);
            Assert.Contains("Server different byte ranges: 0-31,400-444", result.StandardOutput);
            Assert.Contains("Official 9865 response stable bytes: 368/445", result.StandardOutput);
            Assert.Contains("Official 9865 full response samples: yes", result.StandardOutput);
            Assert.Contains("Static response replay candidate: no", result.StandardOutput);
            Assert.Contains("Static response replay reason: server response bytes differ across captures", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task LicenseRouteRelayCompareReportsStaticReplayCandidateWhenOfficialResponsesMatch()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "relay-capture-static-1.json");
            var secondPath = Path.Combine(tempRoot, "relay-capture-static-2.json");

            var firstClientBytes = Enumerable.Range(0, 208)
                .Select(index => (byte)index)
                .ToArray();
            var secondClientBytes = firstClientBytes.ToArray();
            MutateRange(secondClientBytes, 32, 24, 0x55);

            var serverBytes = Enumerable.Range(0, 445)
                .Select(index => (byte)((index * 11 + 7) & 0xff))
                .ToArray();

            await WriteRelayCaptureAsync(firstPath, firstClientBytes, serverBytes, "10.0.0.116:55540");
            await WriteRelayCaptureAsync(secondPath, secondClientBytes, serverBytes, "10.0.0.116:55542");

            var result = await RunDataToolAsync(CommandName, firstPath, secondPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Official 9865 response stable bytes: 445/445", result.StandardOutput);
            Assert.Contains("Official 9865 full response samples: yes", result.StandardOutput);
            Assert.Contains("Static response replay candidate: yes", result.StandardOutput);
            Assert.Contains(
                "Static response replay reason: all fully sampled server response bytes match across captures",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
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

    private static async Task WriteRelayCaptureAsync(
        string path,
        IReadOnlyList<byte> clientToServerBytes,
        IReadOnlyList<byte> serverToClientBytes,
        string acceptedClientEndpoint)
    {
        var capture = new RelayCaptureJson(
            StartedAtUtc: "2026-07-06T15:00:00Z",
            CompletedAtUtc: "2026-07-06T15:00:03Z",
            ListenAddress: "10.0.0.116",
            ListenPort: 9865,
            TargetAddress: "192.0.2.10",
            TargetPort: 9865,
            AcceptedClientEndpoint: acceptedClientEndpoint,
            TargetEndpoint: "192.0.2.10:9865",
            MaxBytes: 4096,
            SampleBytes: 445,
            TotalClientToServerBytes: clientToServerBytes.Count,
            TotalServerToClientBytes: serverToClientBytes.Count,
            StopReason: "serverToClient-closed",
            Chunks:
            [
                CreateRelayCaptureChunk("clientToServer", clientToServerBytes, "2026-07-06T15:00:01Z"),
                CreateRelayCaptureChunk("serverToClient", serverToClientBytes, "2026-07-06T15:00:02Z"),
            ]);

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(capture, JsonOptions));
    }

    private static RelayCaptureChunkJson CreateRelayCaptureChunk(
        string direction,
        IReadOnlyList<byte> bytes,
        string timestampUtc)
    {
        var sample = bytes.ToArray();
        return new RelayCaptureChunkJson(
            TimestampUtc: timestampUtc,
            Direction: direction,
            Length: sample.Length,
            SampleLength: sample.Length,
            FirstHex: Convert.ToHexString(sample).ToLowerInvariant(),
            FirstBase64: Convert.ToBase64String(sample));
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-license-route-relay-compare-" + Guid.NewGuid().ToString("N"));
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

    private sealed record RelayCaptureJson(
        string StartedAtUtc,
        string CompletedAtUtc,
        string ListenAddress,
        int ListenPort,
        string TargetAddress,
        int TargetPort,
        string AcceptedClientEndpoint,
        string TargetEndpoint,
        int MaxBytes,
        int SampleBytes,
        int TotalClientToServerBytes,
        int TotalServerToClientBytes,
        string StopReason,
        IReadOnlyList<RelayCaptureChunkJson> Chunks);

    private sealed record RelayCaptureChunkJson(
        string TimestampUtc,
        string Direction,
        int Length,
        int SampleLength,
        string FirstHex,
        string FirstBase64);

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
