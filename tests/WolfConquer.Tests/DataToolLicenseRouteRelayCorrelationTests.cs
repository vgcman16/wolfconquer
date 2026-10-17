using System.Text.Json;

namespace WolfConquer.Tests;

public sealed class DataToolLicenseRouteRelayCorrelationTests
{
    private const string CommandName = "license-route-relay-correlation";
    private const string ExpectedUsage =
        "WolfConquer.DataTool license-route-relay-correlation <relayCaptureJsonPath> [relayCaptureJsonPath ...]";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public async Task HelpMentionsLicenseRouteRelayCorrelationCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(ExpectedUsage, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task LicenseRouteRelayCorrelationRequiresAtLeastOneRelayCapture()
    {
        var result = await RunDataToolAsync(CommandName);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains(ExpectedUsage, result.StandardError);
    }

    [Fact]
    public async Task LicenseRouteRelayCorrelationReportsDeterministicByteRelationships()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "relay-correlation-1.json");
            var secondPath = Path.Combine(tempRoot, "relay-correlation-2.json");
            var thirdPath = Path.Combine(tempRoot, "relay-correlation-3.json");

            await WriteRelayCaptureAsync(firstPath, CreateClientBytes(10), CreateServerBytes(10), "10.0.0.116:55540");
            await WriteRelayCaptureAsync(secondPath, CreateClientBytes(11), CreateServerBytes(11), "10.0.0.116:55542");
            await WriteRelayCaptureAsync(thirdPath, CreateClientBytes(12), CreateServerBytes(12), "10.0.0.116:55544");

            var result = await RunDataToolAsync(CommandName, firstPath, secondPath, thirdPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("License route relay correlation:", result.StandardOutput);
            Assert.Contains("Relay captures: 3", result.StandardOutput);
            Assert.Contains("Client request lengths: 8,8,8", result.StandardOutput);
            Assert.Contains("Official response lengths: 12,12,12", result.StandardOutput);
            Assert.Contains("Official 208/445 shape: no", result.StandardOutput);
            Assert.Contains("Official 9865 response stable bytes: 6/12", result.StandardOutput);
            Assert.Contains("Official 9865 full response samples: yes", result.StandardOutput);
            Assert.Contains("Static response replay candidate: no", result.StandardOutput);
            Assert.Contains("Static response replay reason: server response bytes differ across captures", result.StandardOutput);
            Assert.Contains("Stable request byte ranges: 2,6", result.StandardOutput);
            Assert.Contains("Request bytes present somewhere in same response:", result.StandardOutput);
            Assert.Contains("relay-correlation-1.json=4/8", result.StandardOutput);
            Assert.Contains("Same-offset byte equality: 2 pair(s): r0->s0, r6->s6", result.StandardOutput);
            Assert.Contains("Request-byte occurrence stable offset pairs: 5 pair(s):", result.StandardOutput);
            Assert.Contains("r1->s2", result.StandardOutput);
            Assert.Contains("XOR stable single-byte candidates:", result.StandardOutput);
            Assert.Contains("r3->s3 xor=0x55", result.StandardOutput);
            Assert.Contains("ADD stable single-byte candidates:", result.StandardOutput);
            Assert.Contains("r4->s4 +0x05", result.StandardOutput);
            Assert.Contains("SUB stable single-byte candidates:", result.StandardOutput);
            Assert.Contains("r5->s5 -0x07", result.StandardOutput);
            Assert.Contains("does not prove crypto, direction, or causality", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task LicenseRouteRelayCorrelationReportsStaticReplayCandidateWhenOfficialResponsesMatch()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "relay-correlation-static-1.json");
            var secondPath = Path.Combine(tempRoot, "relay-correlation-static-2.json");
            var thirdPath = Path.Combine(tempRoot, "relay-correlation-static-3.json");
            var officialResponseBytes = CreateServerBytes(10);

            await WriteRelayCaptureAsync(firstPath, CreateClientBytes(10), officialResponseBytes, "10.0.0.116:55540");
            await WriteRelayCaptureAsync(secondPath, CreateClientBytes(11), officialResponseBytes, "10.0.0.116:55542");
            await WriteRelayCaptureAsync(thirdPath, CreateClientBytes(12), officialResponseBytes, "10.0.0.116:55544");

            var result = await RunDataToolAsync(CommandName, firstPath, secondPath, thirdPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Official 9865 response stable bytes: 12/12", result.StandardOutput);
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

    [Fact]
    public async Task LicenseRouteRelayCorrelationSkipsIncompleteRelayCaptures()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var validPath = Path.Combine(tempRoot, "relay-correlation-valid.json");
            var incompletePath = Path.Combine(tempRoot, "relay-correlation-timeout.json");

            await WriteRelayCaptureAsync(validPath, CreateClientBytes(10), CreateServerBytes(10), "10.0.0.116:55540");
            await WriteIncompleteRelayCaptureAsync(incompletePath);

            var result = await RunDataToolAsync(CommandName, incompletePath, validPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Relay captures: 1", result.StandardOutput);
            Assert.Contains("Skipped incomplete captures: 1", result.StandardOutput);
            Assert.Contains("relay-correlation-timeout.json", result.StandardOutput);
            Assert.Contains("has no clientToServer chunk", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static byte[] CreateClientBytes(byte seed)
    {
        return
        [
            seed,
            (byte)(seed + 10),
            0x30,
            (byte)(seed + 30),
            (byte)(seed + 40),
            (byte)(seed + 50),
            0xaa,
            (byte)(seed + 60),
        ];
    }

    private static byte[] CreateServerBytes(byte seed)
    {
        var request = CreateClientBytes(seed);
        return
        [
            request[0],
            (byte)(0xf0 - seed),
            request[1],
            (byte)(request[3] ^ 0x55),
            (byte)(request[4] + 0x05),
            (byte)(request[5] - 0x07),
            request[6],
            0x01,
            0x02,
            request[2],
            0x03,
            request[6],
        ];
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

    private static async Task WriteIncompleteRelayCaptureAsync(string path)
    {
        var capture = new RelayCaptureJson(
            StartedAtUtc: "2026-07-06T15:00:00Z",
            CompletedAtUtc: "2026-07-06T15:00:30Z",
            ListenAddress: "10.0.0.116",
            ListenPort: 9865,
            TargetAddress: "192.0.2.10",
            TargetPort: 9865,
            AcceptedClientEndpoint: "",
            TargetEndpoint: "192.0.2.10:9865",
            MaxBytes: 4096,
            SampleBytes: 445,
            TotalClientToServerBytes: 0,
            TotalServerToClientBytes: 0,
            StopReason: "accept-timeout",
            Chunks: []);

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
            "wolfconquer-license-route-relay-correlation-" + Guid.NewGuid().ToString("N"));
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
