using System.Diagnostics;

namespace WolfConquer.Tests;

// Payloads and timestamps below are authored fixtures. Account samples retain only
// the fixed protocol signature; their four-byte seeds are deliberately sequential.
public sealed class DataToolProtocolEvidenceTests
{
    [Fact]
    public async Task ProtocolEvidenceCommandReportsEndpointEvidenceFromSavedCaptures()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-protocol-evidence-" + Guid.NewGuid().ToString("N"));
        var firstCapturePath = Path.Combine(tempRoot, "endpoint-probes-1.json");
        var secondCapturePath = Path.Combine(tempRoot, "endpoint-probes-2.json");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                firstCapturePath,
                """
                {
                  "capturedAtUtc": "2000-01-01T00:00:00Z",
                  "timeoutMs": 1000,
                  "maxBytes": 256,
                  "samples": 1,
                  "sampleDelayMs": 0,
                  "results": [
                    { "sample": 1, "role": "AccountServer", "address": "127.0.0.1", "port": 9960, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 8, "payloadBase64": "xUhpEhAREhM=", "payloadSha256": "31521cf4b2f801c83f0d5731ce9f90c72a5ffece0030ad35ca17f6209e3a14d8", "elapsedMs": 10 }
                  ]
                }
                """);
            await File.WriteAllTextAsync(
                secondCapturePath,
                """
                {
                  "capturedAtUtc": "2000-01-01T00:02:00Z",
                  "timeoutMs": 1000,
                  "maxBytes": 256,
                  "samples": 1,
                  "sampleDelayMs": 0,
                  "results": [
                    { "sample": 1, "role": "AccountServer", "address": "127.0.0.1", "port": 9960, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 8, "payloadBase64": "xUhpEkBBQkM=", "payloadSha256": "0245b1cfdc58506a125518e1ae1dbd7e43ca28110176be9a26e89b2eeaead54b", "elapsedMs": 10 },
                    { "sample": 1, "role": "WorldServer", "address": "127.0.0.1", "port": 5818, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 4, "payloadBase64": "kJGSkw==", "payloadSha256": "005805ae04099fea97050424920de1724a6a20b1e2ed0b76dd511b356c958b24", "elapsedMs": 10 }
                  ]
                }
                """);

            var result = await RunDataToolAsync("protocol-evidence", firstCapturePath, secondCapturePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Protocol evidence:", result.StandardOutput);

            var lines = result.StandardOutput
                .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
            var accountLine = Assert.Single(lines, line => line.StartsWith("AccountServer 127.0.0.1:9960 ", StringComparison.Ordinal));
            var worldLine = Assert.Single(lines, line => line.StartsWith("WorldServer 127.0.0.1:5818 ", StringComparison.Ordinal));
            Assert.Contains("classification=", accountLine);
            Assert.Contains("evidence=", accountLine);
            Assert.Contains("classification=", worldLine);
            Assert.Contains("evidence=", worldLine);
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
    public async Task HelpMentionsProtocolEvidenceCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool protocol-evidence <endpointProbeJsonPath> [endpointProbeJsonPath ...]",
            result.StandardOutput);
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
