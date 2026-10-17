using System.Diagnostics;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;

namespace WolfConquer.Tests;

// Payloads and timestamps below are authored fixtures. Account samples retain only
// the fixed protocol signature; their four-byte seeds are deliberately sequential.
public sealed class DataToolEndpointProbeTests
{
    [Fact]
    public async Task ProbeEndpointsCommandReportsLoopbackEndpointBytes()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var acceptTask = AcceptAndSendAsync(listener, [0xAB, 0xCD]);

        try
        {
            var result = await RunDataToolAsync(
                "probe-endpoints",
                "--endpoint",
                $"Fixture=127.0.0.1:{endpoint.Port}",
                "--timeout-ms",
                "1000");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Passive endpoint probe: no payload bytes are sent.", result.StandardOutput);
            Assert.Contains(
                $"Fixture 127.0.0.1:{endpoint.Port} connected=True timedOut=False remoteClosed=False bytes=2 payloadBase64=q80=",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            listener.Stop();
            await IgnoreSocketShutdownAsync(acceptTask);
        }
    }

    [Fact]
    public async Task ProbeEndpointsCommandRepeatsSamplesAndWritesJson()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var acceptTask = AcceptAndSendSequenceAsync(listener, [[0x01], [0x02]]);
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-probe-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempRoot, "endpoint-probes.json");

        try
        {
            var result = await RunDataToolAsync(
                "probe-endpoints",
                "--endpoint",
                $"Fixture=127.0.0.1:{endpoint.Port}",
                "--timeout-ms",
                "1000",
                "--samples",
                "2",
                "--output-json",
                outputPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Fixture[1] 127.0.0.1:{endpoint.Port} connected=True", result.StandardOutput);
            Assert.Contains("payloadBase64=AQ==", result.StandardOutput);
            Assert.Contains($"Fixture[2] 127.0.0.1:{endpoint.Port} connected=True", result.StandardOutput);
            Assert.Contains("payloadBase64=Ag==", result.StandardOutput);
            Assert.Contains($"Output JSON: {outputPath}", result.StandardOutput);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;
            Assert.Equal(2, root.GetProperty("samples").GetInt32());
            Assert.Equal(1000, root.GetProperty("timeoutMs").GetInt32());
            var results = root.GetProperty("results");
            Assert.Equal(2, results.GetArrayLength());
            Assert.Equal(1, results[0].GetProperty("sample").GetInt32());
            Assert.Equal("AQ==", results[0].GetProperty("payloadBase64").GetString());
            Assert.Equal(2, results[1].GetProperty("sample").GetInt32());
            Assert.Equal("Ag==", results[1].GetProperty("payloadBase64").GetString());
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            listener.Stop();
            await IgnoreSocketShutdownAsync(acceptTask);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AnalyzeEndpointProbesCommandReportsStablePrefixes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-probe-analysis-" + Guid.NewGuid().ToString("N"));
        var capturePath = Path.Combine(tempRoot, "endpoint-probes.json");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                capturePath,
                """
                {
                  "capturedAtUtc": "2000-01-01T00:00:00Z",
                  "timeoutMs": 1000,
                  "maxBytes": 256,
                  "samples": 3,
                  "sampleDelayMs": 100,
                  "results": [
                    { "sample": 1, "role": "Gateway", "address": "127.0.0.1", "port": 2021, "connected": true, "timedOut": true, "remoteClosed": false, "bytesRead": 0, "payloadBase64": "", "payloadSha256": "", "elapsedMs": 1000 },
                    { "sample": 2, "role": "Gateway", "address": "127.0.0.1", "port": 2021, "connected": true, "timedOut": true, "remoteClosed": false, "bytesRead": 0, "payloadBase64": "", "payloadSha256": "", "elapsedMs": 1000 },
                    { "sample": 1, "role": "AccountServer", "address": "127.0.0.1", "port": 9960, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 8, "payloadBase64": "xUhpEhAREhM=", "payloadSha256": "31521cf4b2f801c83f0d5731ce9f90c72a5ffece0030ad35ca17f6209e3a14d8", "elapsedMs": 10 },
                    { "sample": 2, "role": "AccountServer", "address": "127.0.0.1", "port": 9960, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 8, "payloadBase64": "xUhpEiAhIiM=", "payloadSha256": "9715ef1cf838cda0a65f5ff63a5f70d014d8e3692199d7ebe461dcee44850e6c", "elapsedMs": 10 },
                    { "sample": 3, "role": "AccountServer", "address": "127.0.0.1", "port": 9960, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 8, "payloadBase64": "xUhpEjAxMjM=", "payloadSha256": "0f65b8e0e2cf1dbf35a8ec930e47b845f6f0540cf74fc13347e31cdd317d29b4", "elapsedMs": 10 }
                  ]
                }
                """);

            var result = await RunDataToolAsync("analyze-endpoint-probes", capturePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Endpoint probe analysis: {capturePath}", result.StandardOutput);
            Assert.Contains("Gateway 127.0.0.1:2021 samples=2 allConnected=True byteCounts=0 uniquePayloads=0 stablePrefixBytes=0 stablePrefixHex=", result.StandardOutput);
            Assert.Contains("AccountServer 127.0.0.1:9960 samples=3 allConnected=True byteCounts=8 uniquePayloads=3 stablePrefixBytes=4 stablePrefixHex=c5486912", result.StandardOutput);
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
    public async Task AnalyzeEndpointProbesCommandReportsRouteScopeForLauncherTraceEndpoints()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-probe-route-scope-" + Guid.NewGuid().ToString("N"));
        var capturePath = Path.Combine(tempRoot, "endpoint-probes.json");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                capturePath,
                """
                {
                  "capturedAtUtc": "2000-01-02T00:00:00Z",
                  "timeoutMs": 1000,
                  "maxBytes": 256,
                  "samples": 1,
                  "sampleDelayMs": 0,
                  "results": [
                    { "sample": 1, "role": "LocalLoginWeb", "address": "127.0.0.1", "port": 80, "connected": true, "timedOut": true, "remoteClosed": false, "bytesRead": 0, "payloadBase64": "", "payloadSha256": "", "elapsedMs": 1000 },
                    { "sample": 1, "role": "CachedDx9LoginWeb", "address": "198.51.100.10", "port": 80, "connected": true, "timedOut": true, "remoteClosed": false, "bytesRead": 0, "payloadBase64": "", "payloadSha256": "", "elapsedMs": 1000 },
                    { "sample": 1, "role": "CachedDx9Route", "address": "192.0.2.10", "port": 9865, "connected": true, "timedOut": true, "remoteClosed": false, "bytesRead": 0, "payloadBase64": "", "payloadSha256": "", "elapsedMs": 1000 }
                  ]
                }
                """);

            var result = await RunDataToolAsync("analyze-endpoint-probes", capturePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "LocalLoginWeb 127.0.0.1:80 samples=1 allConnected=True byteCounts=0 uniquePayloads=0 stablePrefixBytes=0 stablePrefixHex= anyTimedOut=True anyRemoteClosed=False routeScope=Local unexpectedExternalClientRoute=False",
                result.StandardOutput);
            Assert.Contains(
                "CachedDx9LoginWeb 198.51.100.10:80 samples=1 allConnected=True byteCounts=0 uniquePayloads=0 stablePrefixBytes=0 stablePrefixHex= anyTimedOut=True anyRemoteClosed=False routeScope=External unexpectedExternalClientRoute=True",
                result.StandardOutput);
            Assert.Contains(
                "CachedDx9Route 192.0.2.10:9865 samples=1 allConnected=True byteCounts=0 uniquePayloads=0 stablePrefixBytes=0 stablePrefixHex= anyTimedOut=True anyRemoteClosed=False routeScope=External unexpectedExternalClientRoute=True",
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
    public async Task CompareEndpointProbesCommandReportsCrossCapturePrefixes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-probe-comparison-" + Guid.NewGuid().ToString("N"));
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
                  "samples": 2,
                  "sampleDelayMs": 100,
                  "results": [
                    { "sample": 1, "role": "AccountServer", "address": "127.0.0.1", "port": 9960, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 8, "payloadBase64": "xUhpEhAREhM=", "payloadSha256": "31521cf4b2f801c83f0d5731ce9f90c72a5ffece0030ad35ca17f6209e3a14d8", "elapsedMs": 10 },
                    { "sample": 1, "role": "WorldServer", "address": "127.0.0.1", "port": 5818, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 4, "payloadBase64": "gIGCgw==", "payloadSha256": "8d71ad9ffc3b5bfeffcd080b8547b21ef1f05bdaac5371beef8c6fd45718c495", "elapsedMs": 10 }
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
                  "samples": 2,
                  "sampleDelayMs": 100,
                  "results": [
                    { "sample": 1, "role": "AccountServer", "address": "127.0.0.1", "port": 9960, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 8, "payloadBase64": "xUhpEkBBQkM=", "payloadSha256": "0245b1cfdc58506a125518e1ae1dbd7e43ca28110176be9a26e89b2eeaead54b", "elapsedMs": 10 },
                    { "sample": 1, "role": "WorldServer", "address": "127.0.0.1", "port": 5818, "connected": true, "timedOut": false, "remoteClosed": false, "bytesRead": 4, "payloadBase64": "kJGSkw==", "payloadSha256": "005805ae04099fea97050424920de1724a6a20b1e2ed0b76dd511b356c958b24", "elapsedMs": 10 }
                  ]
                }
                """);

            var result = await RunDataToolAsync("compare-endpoint-probes", firstCapturePath, secondCapturePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Endpoint probe comparison: 2 capture(s)", result.StandardOutput);
            Assert.Contains("AccountServer 127.0.0.1:9960 captures=2 samples=2 allConnected=True byteCounts=8 uniquePayloads=2 stablePrefixBytes=4 stablePrefixHex=c5486912", result.StandardOutput);
            Assert.Contains("WorldServer 127.0.0.1:5818 captures=2 samples=2 allConnected=True byteCounts=4 uniquePayloads=2 stablePrefixBytes=0 stablePrefixHex=", result.StandardOutput);
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
    public async Task WritePktmonCaptureScriptCommandWritesLoginScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-script-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempRoot, "login-capture.ps1");

        try
        {
            var result = await RunDataToolAsync("write-pktmon-capture-script", "login", outputPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Pktmon capture script:", result.StandardOutput);
            Assert.Contains("Kind: login", result.StandardOutput);
            Assert.Contains("Port: 9960", result.StandardOutput);
            Assert.Contains("Mode: manual stop", result.StandardOutput);
            Assert.Contains($"Output: {outputPath}", result.StandardOutput);
            Assert.Contains("Run from normal PowerShell; the script relaunches itself elevated if needed.", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);

            var script = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("function Test-IsAdministrator", script);
            Assert.Contains("Start-Process -FilePath powershell.exe -Verb RunAs -WorkingDirectory $Root -ArgumentList $ArgumentList", script);
            Assert.Contains("Set-Location -LiteralPath $Root", script);
            Assert.Contains("pktmon filter add CO_LOGIN_9960 -t TCP -p 9960", script);
            Assert.Contains("pktmon start --capture --comp all --pkt-size 128", script);
            Assert.Contains("--file-size 32", script);
            Assert.Contains("co_login_9960_pktmon.pcapng", script);
            Assert.Contains("Run one normal client login", script);
            Assert.Contains("$OutDir = Join-Path $Root '.setup_work\\captures\\login-pktmon'", script);
            Assert.Contains("Workspace root: $Root", script);
            Assert.Contains("Capture output will be converted to $Pcap", script);
            Assert.Contains("Return to this elevated window and press Enter to stop pktmon.", script);
            Assert.DoesNotContain("Get-Location", script);
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
    public async Task WritePktmonCaptureScriptCommandWritesWorldScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-script-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempRoot, "world-capture.ps1");

        try
        {
            var result = await RunDataToolAsync("write-pktmon-capture-script", "world", outputPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Kind: world", result.StandardOutput);
            Assert.Contains("Port: 5818", result.StandardOutput);
            Assert.Contains("Mode: manual stop", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);

            var script = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("pktmon filter add CO_WORLD_5818 -t TCP -p 5818", script);
            Assert.Contains("pktmon start --capture --comp all --pkt-size 256", script);
            Assert.Contains("--file-size 64", script);
            Assert.Contains("co_world_5818_pktmon.pcapng", script);
            Assert.Contains("Select the server and enter the world", script);
            Assert.Contains("Capture output will be converted to $Pcap", script);
            Assert.Contains("Return to this elevated window and press Enter to stop pktmon.", script);
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
    public async Task WritePktmonCaptureScriptCommandWritesTimedWorldScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-script-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempRoot, "world-capture-timed.ps1");

        try
        {
            var result = await RunDataToolAsync(
                "write-pktmon-capture-script",
                "world",
                outputPath,
                "--duration-seconds",
                "180");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Kind: world", result.StandardOutput);
            Assert.Contains("Port: 5818", result.StandardOutput);
            Assert.Contains("Mode: timed 180 seconds", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);

            var script = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("$Seconds = 180", script);
            Assert.Contains("WolfConquer timed world capture for TCP port 5818", script);
            Assert.Contains("Capture window: $Seconds seconds", script);
            Assert.Contains("pktmon filter add CO_WORLD_5818 -t TCP -p 5818", script);
            Assert.Contains("pktmon start --capture --comp all --pkt-size 256", script);
            Assert.Contains("Start-Sleep -Seconds $Seconds", script);
            Assert.Contains("Stop-Transcript | Out-Null", script);
            Assert.Contains("co_world_5818_pktmon.log", script);
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
    public async Task WritePktmonCaptureScriptCommandWritesTimedOriginalFlowScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-script-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempRoot, ".setup_work", "capture-scripts", "original-flow-capture-timed.ps1");

        try
        {
            var result = await RunDataToolAsync(
                "write-pktmon-capture-script",
                "original-flow",
                outputPath,
                "--duration-seconds",
                "240");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Kind: original-flow", result.StandardOutput);
            Assert.Contains("Ports: 80,2021,9960,5818", result.StandardOutput);
            Assert.Contains("Mode: timed 240 seconds", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);

            var script = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("$Seconds = 240", script);
            Assert.Contains($"$Root = '{tempRoot.Replace("'", "''", StringComparison.Ordinal)}'", script);
            Assert.Contains("function Test-IsAdministrator", script);
            Assert.Contains("Not elevated; opening an administrator PowerShell for this capture.", script);
            Assert.Contains("Start-Process -FilePath powershell.exe -Verb RunAs -WorkingDirectory $Root -ArgumentList $ArgumentList", script);
            Assert.Contains("Set-Location -LiteralPath $Root", script);
            Assert.Contains("WolfConquer timed original-flow capture for TCP port 80", script);
            Assert.Contains("This script opens an administrator PowerShell if launched normally.", script);
            Assert.Contains("Workspace root: $Root", script);
            Assert.Contains("Run one normal client loader, login, server-select, and world-entry flow", script);
            Assert.Contains("pktmon filter add CO_LOGINWEB_80 -t TCP -p 80", script);
            Assert.Contains("pktmon filter add CO_GUARDSHIELD_2021 -t TCP -p 2021", script);
            Assert.Contains("pktmon filter add CO_ACCOUNT_9960 -t TCP -p 9960", script);
            Assert.Contains("pktmon filter add CO_WORLD_5818 -t TCP -p 5818", script);
            Assert.Contains("pktmon start --capture --comp all --pkt-size 256", script);
            Assert.Contains("--file-size 96", script);
            Assert.Contains("co_original_flow_pktmon.pcapng", script);
            Assert.DoesNotContain("Get-Location", script);
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
    public async Task WritePktmonCaptureScriptCommandRejectsInvalidDuration()
    {
        var result = await RunDataToolAsync(
            "write-pktmon-capture-script",
            "login",
            "--duration-seconds",
            "0");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--duration-seconds must be a positive whole number.", result.StandardError);
    }

    [Fact]
    public async Task WritePktmonCaptureScriptCommandRejectsUnknownKind()
    {
        var result = await RunDataToolAsync("write-pktmon-capture-script", "chat");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown capture kind. Use 'login', 'world', or 'original-flow'.", result.StandardError);
    }

    private static async Task AcceptAndSendAsync(TcpListener listener, byte[] payload)
    {
        using var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    private static Task AcceptAndSendSequenceAsync(TcpListener listener, IReadOnlyList<byte[]> payloads)
    {
        // Keep sample replies off test-host continuations: the child process has
        // an independent read deadline even when the test scheduler is busy.
        return Task.Factory.StartNew(
            () =>
            {
                foreach (var payload in payloads)
                {
                    using var client = listener.AcceptTcpClient();
                    using var stream = client.GetStream();
                    stream.Write(payload);
                    stream.Flush();
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
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

    private static async Task IgnoreSocketShutdownAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
        catch (TimeoutException)
        {
        }
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
