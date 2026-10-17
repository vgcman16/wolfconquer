using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolRelayCaptureSummaryTests
{
    private const string CommandName = "tcp-relay-capture-summary";

    [Fact]
    public async Task LegacyWorldActiveBridgeSummaryReportsHandshakeTotalsAndOpcodes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-active-bridge-summary-" + Guid.NewGuid().ToString("N"));
        var capturePath = Path.Combine(tempRoot, "active-bridge.json");

        try
        {
            Directory.CreateDirectory(tempRoot);
            var captureJson = """
                {
                  "startedAtUtc": "2026-06-06T16:00:00Z",
                  "completedAtUtc": "2026-06-06T16:00:05Z",
                  "listenAddress": "127.0.0.1",
                  "listenPort": 15819,
                  "targetAddress": "127.0.0.1",
                  "targetPort": 5818,
                  "acceptedClientEndpoint": "192.168.84.1:50000",
                  "targetEndpoint": "127.0.0.1:5818",
                  "maxBytes": 131072,
                  "sampleBytes": 128,
                  "maxFrames": 256,
                  "totalClientToServerBytes": 60,
                  "totalServerToClientBytes": 115,
                  "stopReason": "clientToServer-closed",
                  "targetServerHandshakeLength": 333,
                  "targetClientHandshakeReplyLength": 184,
                  "clientServerHandshakeLength": 333,
                  "clientHandshakeReplyLength": 184,
                  "frames": [
                    {
                      "timestampUtc": "2026-06-06T16:00:02Z",
                      "index": 0,
                      "direction": "clientToServer",
                      "opcode": 2065,
                      "length": 28,
                      "payloadLength": 4,
                      "sampleLength": 28,
                      "firstHex": "1c001108",
                      "firstBase64": "HAARCQ=="
                    },
                    {
                      "timestampUtc": "2026-06-06T16:00:03Z",
                      "index": 1,
                      "direction": "serverToClient",
                      "opcode": 2179,
                      "length": 73,
                      "payloadLength": 49,
                      "sampleLength": 73,
                      "firstHex": "49008308",
                      "firstBase64": "SQCDCA=="
                    }
                  ],
                  "chunks": [
                    {
                      "timestampUtc": "2026-06-06T16:00:02Z",
                      "direction": "clientToServer",
                      "length": 28,
                      "sampleLength": 28,
                      "firstHex": "1c001108",
                      "firstBase64": "HAARCQ=="
                    }
                  ],
                  "captureWarnings": [
                    "test warning"
                  ]
                }
                """;
            await File.WriteAllTextAsync(capturePath, captureJson, Encoding.UTF8);
            var captureBytes = await File.ReadAllBytesAsync(capturePath);
            var captureSha256 = Convert.ToHexString(SHA256.HashData(captureBytes)).ToLowerInvariant();

            var result = await RunDataToolAsync("legacy-world-active-bridge-summary", capturePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Legacy world active bridge summary:", result.StandardOutput);
            Assert.Contains($"Path: {capturePath}", result.StandardOutput);
            Assert.Contains($"sha256={captureSha256}", result.StandardOutput);
            Assert.Contains("listen=127.0.0.1:15819 target=127.0.0.1:5818", result.StandardOutput);
            Assert.Contains("acceptedClient=192.168.84.1:50000 targetEndpoint=127.0.0.1:5818", result.StandardOutput);
            Assert.Contains("handshakeStatus=completed targetServer=333 targetClient=184 clientServer=333 client=184", result.StandardOutput);
            Assert.Contains("totalClientToServerBytes=60", result.StandardOutput);
            Assert.Contains("totalServerToClientBytes=115", result.StandardOutput);
            Assert.Contains("frames=2 chunks=1 stopReason=clientToServer-closed", result.StandardOutput);
            Assert.Contains("frameOpcodes=0:clientToServer:2065, 1:serverToClient:2179", result.StandardOutput);
            Assert.Contains("warnings=test warning", result.StandardOutput);
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
    public async Task TcpRelayCaptureSummaryReportsFixtureTotalsAndFirstChunkDirections()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-relay-capture-summary-" + Guid.NewGuid().ToString("N"));
        var capturePath = Path.Combine(tempRoot, "relay-capture.json");

        try
        {
            Directory.CreateDirectory(tempRoot);
            var captureJson = """
                {
                  "startedAtUtc": "2026-06-06T15:00:00Z",
                  "completedAtUtc": "2026-06-06T15:00:03Z",
                  "listenAddress": "127.0.0.1",
                  "listenPort": 15819,
                  "targetAddress": "127.0.0.1",
                  "targetPort": 5818,
                  "maxBytes": 16,
                  "acceptSeconds": 180,
                  "idleSeconds": 45,
                  "totalClientToServerBytes": 3,
                  "totalServerToClientBytes": 5,
                  "stopReason": "idle-timeout",
                  "chunks": [
                    {
                      "timestampUtc": "2026-06-06T15:00:01Z",
                      "direction": "clientToServer",
                      "length": 3,
                      "sampleLength": 3,
                      "firstHex": "010203",
                      "firstBase64": "AQID"
                    },
                    {
                      "timestampUtc": "2026-06-06T15:00:02Z",
                      "direction": "serverToClient",
                      "length": 5,
                      "sampleLength": 5,
                      "firstHex": "a0a1a2a3a4",
                      "firstBase64": "oKGio6Q="
                    }
                  ]
                }
                """;
            await File.WriteAllTextAsync(capturePath, captureJson, Encoding.UTF8);
            var captureBytes = await File.ReadAllBytesAsync(capturePath);
            var captureSha256 = Convert.ToHexString(SHA256.HashData(captureBytes)).ToLowerInvariant();

            var result = await RunDataToolAsync(CommandName, capturePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("TCP relay capture summary:", result.StandardOutput);
            Assert.Contains($"Path: {capturePath}", result.StandardOutput);
            Assert.Contains($"sha256={captureSha256}", result.StandardOutput);
            Assert.Contains("totalClientToServerBytes=3", result.StandardOutput);
            Assert.Contains("totalServerToClientBytes=5", result.StandardOutput);
            Assert.Contains("chunks=2", result.StandardOutput);
            Assert.Contains("stopReason=idle-timeout", result.StandardOutput);
            Assert.Contains("clientToServer:3", result.StandardOutput);
            Assert.Contains("serverToClient:5", result.StandardOutput);
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
    public async Task LegacyWorldActiveBridgeSummaryKeepsMaxFrameWarningVisibleWithLaterStopReason()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-active-bridge-summary-cap-" + Guid.NewGuid().ToString("N"));
        var capturePath = Path.Combine(tempRoot, "active-bridge-max-frame-cap.json");

        try
        {
            Directory.CreateDirectory(tempRoot);
            var captureJson = """
                {
                  "startedAtUtc": "2026-06-06T16:00:00Z",
                  "completedAtUtc": "2026-06-06T16:01:05Z",
                  "listenAddress": "127.0.0.1",
                  "listenPort": 15819,
                  "targetAddress": "127.0.0.1",
                  "targetPort": 5818,
                  "acceptedClientEndpoint": "192.168.84.1:50000",
                  "targetEndpoint": "127.0.0.1:5818",
                  "maxBytes": 131072,
                  "sampleBytes": 128,
                  "maxFrames": 2,
                  "totalClientToServerBytes": 60,
                  "totalServerToClientBytes": 4096,
                  "stopReason": "idle-timeout",
                  "targetServerHandshakeLength": 333,
                  "targetClientHandshakeReplyLength": 184,
                  "clientServerHandshakeLength": 333,
                  "clientHandshakeReplyLength": 184,
                  "frames": [
                    {
                      "timestampUtc": "2026-06-06T16:00:02Z",
                      "index": 0,
                      "direction": "serverToClient",
                      "opcode": 2179,
                      "length": 73,
                      "payloadLength": 49,
                      "sampleLength": 73,
                      "firstHex": "49008308",
                      "firstBase64": "SQCDCA=="
                    },
                    {
                      "timestampUtc": "2026-06-06T16:00:03Z",
                      "index": 1,
                      "direction": "serverToClient",
                      "opcode": 10010,
                      "length": 52,
                      "payloadLength": 28,
                      "sampleLength": 52,
                      "firstHex": "34001a27",
                      "firstBase64": "NAAaJw=="
                    }
                  ],
                  "chunks": [],
                  "captureWarnings": [
                    "max-frames reached at 2 stored frame(s); continuing active bridge relay"
                  ]
                }
                """;
            await File.WriteAllTextAsync(capturePath, captureJson, Encoding.UTF8);

            var result = await RunDataToolAsync("legacy-world-active-bridge-summary", capturePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("frames=2 chunks=0 stopReason=idle-timeout", result.StandardOutput);
            Assert.Contains("frameOpcodes=0:serverToClient:2179, 1:serverToClient:10010", result.StandardOutput);
            Assert.Contains(
                "warnings=max-frames reached at 2 stored frame(s); continuing active bridge relay",
                result.StandardOutput);
            Assert.DoesNotContain("stopReason=max-frames", result.StandardOutput);
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
    public async Task HelpMentionsTcpRelayCaptureSummaryCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool tcp-relay-capture-summary <relayCaptureJsonPath>",
            result.StandardOutput);
        Assert.Contains(
            "WolfConquer.DataTool legacy-world-relay-decode <relayCaptureJsonPath> <outputJsonPath>",
            result.StandardOutput);
        Assert.Contains(
            "WolfConquer.DataTool legacy-world-active-bridge-capture <listenAddress> <listenPort> <targetAddress> <targetPort> <outputPath>",
            result.StandardOutput);
        Assert.Contains("--continue-after-max-frames", result.StandardOutput);
        Assert.Contains(
            "WolfConquer.DataTool legacy-world-active-bridge-summary <activeBridgeCaptureJsonPath>",
            result.StandardOutput);
        Assert.Contains(
            "WolfConquer.DataTool active-bridge-capture-runbook [rootPath]",
            result.StandardOutput);
        Assert.Contains(
            "WolfConquer.DataTool active-bridge-capture-status [activeBridgeCaptureJsonPath] [clientExePath]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task ActiveBridgeCaptureRunbookPrintsDatabaseSetupAndRestoreCommands()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-active-bridge-runbook-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);

            var result = await RunDataToolAsync("active-bridge-capture-runbook", tempRoot);
            var dataToolProjectPath = Path.Combine(tempRoot, "src", "WolfConquer.DataTool", "WolfConquer.DataTool.csproj");
            var accountServerProjectPath = Path.Combine(tempRoot, "src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj");
            const string outputPath = @".artifacts/active-bridge.json";

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Active world bridge capture runbook:", result.StandardOutput);
            Assert.Contains($"Root: {tempRoot}", result.StandardOutput);
            Assert.Contains("Bridge listen: 127.0.0.1:15819", result.StandardOutput);
            Assert.Contains("Bridge target: 127.0.0.1:5818", result.StandardOutput);
            Assert.Contains("Optional account DB configuration (not bundled): config/GuardShield.ini.", result.StandardOutput);
            Assert.Contains("$env:WOLFCONQUER_ACCOUNT_MYSQL =", result.StandardOutput);
            Assert.Contains("$env:WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE = '1'", result.StandardOutput);
            Assert.Contains("$env:WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER = '1293600'", result.StandardOutput);
            Assert.Contains(
                "Remove-Item Env:\\WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE -ErrorAction SilentlyContinue",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{dataToolProjectPath}' -- legacy-world-active-bridge-capture 127.0.0.1 15819 127.0.0.1 5818 '{outputPath}' --max-bytes 131072 --max-frames 256 --sample-bytes 128 --accept-seconds 600 --idle-seconds 60 --continue-after-max-frames --progress",
                result.StandardOutput);
            Assert.Contains("Progress output should print 'accepted client'", result.StandardOutput);
            var statusCommand =
                $"dotnet run --project '{dataToolProjectPath}' -- active-bridge-capture-status '{outputPath}' 'client\\Env_DX8\\Conquer.exe'";
            Assert.Contains(statusCommand, result.StandardOutput);
            Assert.Contains("Continue only when status reports ReadyForManualLogin: True", result.StandardOutput);
            Assert.Contains("Known client launcher: client/launch.cmd", result.StandardOutput);
            Assert.Contains("changes into Env_DX8 and starts Conquer.exe blacknull", result.StandardOutput);
            Assert.True(
                result.StandardOutput.IndexOf(statusCommand, StringComparison.Ordinal) <
                result.StandardOutput.IndexOf("Start-Process 'client/launch.cmd'", StringComparison.Ordinal),
                "The runbook should print the active bridge status gate before launching the client.");
            Assert.Contains(
                $"dotnet run --project '{accountServerProjectPath}' -- --listen --address 0.0.0.0 --port 9960 --legacy-account-wire --world-endpoint 127.0.0.1:15819",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{dataToolProjectPath}' -- legacy-world-active-bridge-summary '{outputPath}'",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{accountServerProjectPath}' -- --listen --address 0.0.0.0 --port 9960 --legacy-account-wire --world-endpoint 127.0.0.1:5818",
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
    public async Task ActiveBridgeCaptureAcceptsContinueAfterMaxFramesOptionAndKeepsLaterStopReason()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-active-bridge-continue-after-cap-" + Guid.NewGuid().ToString("N"));
        var capturePath = Path.Combine(tempRoot, "active-bridge-continue-after-cap.json");

        try
        {
            Directory.CreateDirectory(tempRoot);

            var result = await RunDataToolAsync(
                "legacy-world-active-bridge-capture",
                "127.0.0.1",
                "0",
                "127.0.0.1",
                "1",
                capturePath,
                "--max-frames",
                "1",
                "--continue-after-max-frames",
                "--accept-seconds",
                "1",
                "--idle-seconds",
                "1",
                "--progress");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Progress: listening 127.0.0.1:", result.StandardOutput);
            Assert.Contains("stopReason=accept-timeout", result.StandardOutput);
            Assert.DoesNotContain("Unknown legacy-world-active-bridge-capture option", result.StandardOutput);
            Assert.True(File.Exists(capturePath), "Capture should write the timeout JSON artifact when the continue-after-max-frames flag is present.");
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
    public async Task ActiveBridgeCaptureProgressPrintsListeningMilestoneAndWritesTimeoutArtifact()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-active-bridge-progress-" + Guid.NewGuid().ToString("N"));
        var capturePath = Path.Combine(tempRoot, "active-bridge-progress.json");

        try
        {
            Directory.CreateDirectory(tempRoot);

            var result = await RunDataToolAsync(
                "legacy-world-active-bridge-capture",
                "127.0.0.1",
                "0",
                "127.0.0.1",
                "1",
                capturePath,
                "--accept-seconds",
                "1",
                "--idle-seconds",
                "1",
                "--progress");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Progress: listening 127.0.0.1:", result.StandardOutput);
            Assert.Contains("-> 127.0.0.1:1", result.StandardOutput);
            Assert.Contains("Output JSON: " + capturePath, result.StandardOutput);
            Assert.Contains("stopReason=accept-timeout", result.StandardOutput);
            Assert.True(File.Exists(capturePath), "Progress capture should write a timeout JSON artifact.");
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
