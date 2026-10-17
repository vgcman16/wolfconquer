using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static partial class Program
{
    static int LegacyWorldActiveBridgeCapture(string[] args)
    {
        LegacyWorldActiveBridgeCaptureCommandOptions commandOptions;
        try
        {
            commandOptions = ResolveLegacyWorldActiveBridgeCaptureOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            void ReportProgress(string message) => Console.WriteLine($"Progress: {message}");

            var result = LegacyWorldActiveBridgeCaptureRunner.RunAsync(
                    commandOptions.Bridge,
                    CancellationToken.None,
                    commandOptions.Progress ? ReportProgress : null)
                .GetAwaiter()
                .GetResult();
            var outputDirectory = Path.GetDirectoryName(commandOptions.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            WriteJson(commandOptions.OutputPath, result);

            Console.WriteLine("Legacy world active bridge capture:");
            Console.WriteLine($"Listen: {commandOptions.Bridge.ListenAddress}:{commandOptions.Bridge.ListenPort}");
            Console.WriteLine($"Target: {commandOptions.Bridge.TargetAddress}:{commandOptions.Bridge.TargetPort}");
            Console.WriteLine($"Output JSON: {commandOptions.OutputPath}");
            Console.WriteLine(
                $"Handshake: server={result.TargetServerHandshakeLength} " +
                $"client={result.ClientHandshakeLength} status={result.HandshakeStatus}");
            Console.WriteLine(
                $"Totals: clientToServer={result.TotalClientToServerBytes:N0} " +
                $"serverToClient={result.TotalServerToClientBytes:N0} frames={result.Frames.Count:N0} " +
                $"stopReason={result.StopReason}");
            return result.Frames.Count > 0 ? 0 : 2;
        }
        catch (Exception ex) when (
            ex is ArgumentException or FormatException or IOException or InvalidDataException or SocketException or
                OperationCanceledException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Legacy world active bridge capture failed: {ex.Message}");
            return 2;
        }
    }

    static int LegacyWorldActiveBridgeSummary(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: WolfConquer.DataTool legacy-world-active-bridge-summary <activeBridgeCaptureJsonPath>");
            return 1;
        }

        try
        {
            var path = Path.GetFullPath(args[1]);
            var bytes = File.ReadAllBytes(path);
            var capture = ReadLegacyWorldActiveBridgeCaptureResult(path);
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            Console.WriteLine("Legacy world active bridge summary:");
            Console.WriteLine($"Path: {path}");
            Console.WriteLine($"fileLength={bytes.Length} sha256={sha256}");
            Console.WriteLine($"listen={capture.ListenAddress}:{capture.ListenPort} target={capture.TargetAddress}:{capture.TargetPort}");
            Console.WriteLine(
                $"acceptedClient={FormatNullableCaptureValue(capture.AcceptedClientEndpoint)} " +
                $"targetEndpoint={FormatNullableCaptureValue(capture.TargetEndpoint)}");
            Console.WriteLine(
                $"handshakeStatus={capture.HandshakeStatus} " +
                $"targetServer={capture.TargetServerHandshakeLength} " +
                $"targetClient={capture.TargetClientHandshakeReplyLength} " +
                $"clientServer={capture.ClientServerHandshakeLength} " +
                $"client={capture.ClientHandshakeReplyLength}");
            Console.WriteLine(
                $"maxBytes={capture.MaxBytes} sampleBytes={capture.SampleBytes} maxFrames={capture.MaxFrames}");
            Console.WriteLine(
                $"totalClientToServerBytes={capture.TotalClientToServerBytes} " +
                $"totalServerToClientBytes={capture.TotalServerToClientBytes} " +
                $"frames={capture.Frames.Count} chunks={capture.Chunks.Count} stopReason={capture.StopReason}");
            Console.WriteLine($"frameOpcodes={FormatLegacyWorldActiveBridgeFrameOpcodes(capture.Frames)}");
            Console.WriteLine($"warnings={FormatLegacyWorldActiveBridgeWarnings(capture.CaptureWarnings)}");
            return capture.HandshakeCompleted && capture.Frames.Count > 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Legacy world active bridge summary failed: {ex.Message}");
            return 1;
        }
    }

    static LegacyWorldActiveBridgeCaptureResult ReadLegacyWorldActiveBridgeCaptureResult(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var capture = JsonSerializer.Deserialize<LegacyWorldActiveBridgeCaptureResult>(
            File.ReadAllText(path),
            options);
        if (capture is null)
        {
            throw new InvalidDataException($"Could not read legacy world active bridge capture JSON: {path}");
        }

        if (capture.Frames is null)
        {
            throw new InvalidDataException($"Legacy world active bridge capture JSON has no frames array: {path}");
        }

        if (capture.Chunks is null)
        {
            throw new InvalidDataException($"Legacy world active bridge capture JSON has no chunks array: {path}");
        }

        if (capture.CaptureWarnings is null)
        {
            throw new InvalidDataException($"Legacy world active bridge capture JSON has no captureWarnings array: {path}");
        }

        return capture;
    }

    static string FormatNullableCaptureValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
    }

    static string FormatLegacyWorldActiveBridgeFrameOpcodes(
        IReadOnlyList<LegacyWorldActiveBridgeCaptureFrame> frames)
    {
        if (frames.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            ", ",
            frames
                .OrderBy(frame => frame.Index)
                .Take(16)
                .Select(frame => $"{frame.Index}:{frame.Direction}:{frame.Opcode}"));
    }

    static string FormatLegacyWorldActiveBridgeWarnings(IReadOnlyList<string> warnings)
    {
        return warnings.Count == 0 ? "<none>" : string.Join(" | ", warnings);
    }

    static LegacyWorldActiveBridgeCaptureCommandOptions ResolveLegacyWorldActiveBridgeCaptureOptions(string[] args)
    {
        if (args.Length < 6)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool legacy-world-active-bridge-capture <listenAddress> <listenPort> <targetAddress> <targetPort> <outputPath> [--max-bytes N] [--max-frames N] [--sample-bytes N] [--idle-seconds N] [--accept-seconds N] [--continue-after-max-frames] [--progress]");
        }

        if (!IPAddress.TryParse(args[1], out var listenAddress))
        {
            throw new FormatException($"Invalid listen address: {args[1]}");
        }

        if (!int.TryParse(args[2], out var listenPort) || listenPort is < 0 or > 65535)
        {
            throw new FormatException($"Invalid listen port: {args[2]}");
        }

        if (!int.TryParse(args[4], out var targetPort) || targetPort is < 1 or > 65535)
        {
            throw new FormatException($"Invalid target port: {args[4]}");
        }

        var maxBytes = 65536;
        var maxFrames = 128;
        var sampleBytes = 64;
        var idleSeconds = 45;
        var acceptSeconds = 300;
        var progress = false;
        var continueAfterMaxFrames = false;
        for (var index = 6; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--progress", StringComparison.OrdinalIgnoreCase))
            {
                progress = true;
            }
            else if (string.Equals(option, "--continue-after-max-frames", StringComparison.OrdinalIgnoreCase))
            {
                continueAfterMaxFrames = true;
            }
            else if (string.Equals(option, "--max-bytes", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out maxBytes) || maxBytes < 0)
                {
                    throw new FormatException("--max-bytes requires a non-negative integer.");
                }
            }
            else if (string.Equals(option, "--max-frames", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out maxFrames) || maxFrames <= 0)
                {
                    throw new FormatException("--max-frames requires a positive integer.");
                }
            }
            else if (string.Equals(option, "--sample-bytes", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out sampleBytes) || sampleBytes < 0)
                {
                    throw new FormatException("--sample-bytes requires a non-negative integer.");
                }
            }
            else if (string.Equals(option, "--idle-seconds", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out idleSeconds) || idleSeconds <= 0)
                {
                    throw new FormatException("--idle-seconds requires a positive integer.");
                }
            }
            else if (string.Equals(option, "--accept-seconds", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out acceptSeconds) || acceptSeconds <= 0)
                {
                    throw new FormatException("--accept-seconds requires a positive integer.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown legacy-world-active-bridge-capture option: {option}");
            }
        }

        return new LegacyWorldActiveBridgeCaptureCommandOptions(
            new LegacyWorldActiveBridgeCaptureOptions(
                listenAddress,
                listenPort,
                args[3],
                targetPort,
                maxBytes,
                maxFrames,
                sampleBytes,
                TimeSpan.FromSeconds(acceptSeconds),
                TimeSpan.FromSeconds(idleSeconds),
                continueAfterMaxFrames),
            Path.GetFullPath(args[5]),
            progress);
    }

    static int ActiveBridgeCaptureRunbook(string[] args)
    {
        string rootPath;
        try
        {
            rootPath = ResolveActiveBridgeCaptureRunbookRootPath(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var fullRootPath = Path.GetFullPath(rootPath);
        var dataToolProjectPath = Path.Combine(
            fullRootPath,
            "src",
            "WolfConquer.DataTool",
            "WolfConquer.DataTool.csproj");
        var accountServerProjectPath = Path.Combine(
            fullRootPath,
            "src",
            "WolfConquer.AccountServer",
            "WolfConquer.AccountServer.csproj");
        const string outputPath = @".artifacts/active-bridge.json";
        const string clientExePath = @"client\Env_DX8\Conquer.exe";
        const string clientLauncherPath = @"client/launch.cmd";
        const string liveAddress = "127.0.0.1";
        const int accountPort = 9960;
        const int originalWorldPort = 5818;
        const int bridgeWorldPort = 15819;
        const uint captureIdentifier = 1293600;

        Console.WriteLine("Active world bridge capture runbook:");
        Console.WriteLine($"Root: {fullRootPath}");
        Console.WriteLine($"Bridge listen: {liveAddress}:{bridgeWorldPort}");
        Console.WriteLine($"Bridge target: {liveAddress}:{originalWorldPort}");
        Console.WriteLine($"Output JSON: {outputPath}");
        Console.WriteLine("Guardrails: runbook only; these commands print the staged flow and do not stop or start processes by themselves.");
        Console.WriteLine("Known client launcher: client/launch.cmd changes into Env_DX8 and starts Conquer.exe blacknull. Computer Use can click the DirectX UI, but the login text fields may require manual keyboard input.");
        Console.WriteLine("Capture override: WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER forces any parsed legacy auth to return Ready with the configured identifier only when WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE is explicitly truthy; it cannot bypass the client's blank-field validation.");
        Console.WriteLine("Set live AccountServer and smoke-test inputs:");
        Console.WriteLine("Optional account DB configuration (not bundled): config/GuardShield.ini.");
        Console.WriteLine(FormatAccountMySqlEnvironmentCommand());
        Console.WriteLine("$env:WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE = '1'");
        Console.WriteLine($"$env:WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER = '{captureIdentifier}'");
        Console.WriteLine("$smokeUsername = '2'");
        Console.WriteLine("$smokePassword = '2'");
        Console.WriteLine("$serverName = 'TestSrv'");
        Console.WriteLine("$stopWolfAccountServer = \"Get-CimInstance Win32_Process | Where-Object { `$_.Name -eq 'dotnet.exe' -and `$_.CommandLine -like '*WolfConquer.AccountServer.dll*--listen*--port 9960*' } | ForEach-Object { Stop-Process -Id `$_.ProcessId -Force }\"");
        Console.WriteLine("1. Verify no stale client world sockets before opening the bridge:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"world-relay-capture-preflight {QuotePowerShellSingle(clientExePath)}");
        Console.WriteLine("2. Start the active bridge before restarting AccountServer:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"legacy-world-active-bridge-capture {liveAddress} {bridgeWorldPort} {liveAddress} {originalWorldPort} " +
            $"{QuotePowerShellSingle(outputPath)} --max-bytes 131072 --max-frames 256 --sample-bytes 128 --accept-seconds 600 --idle-seconds 60 --continue-after-max-frames --progress");
        Console.WriteLine("3. Stop the current WolfConquer AccountServer, then restart it so successful auth forwards to the bridge:");
        Console.WriteLine("powershell -NoProfile -ExecutionPolicy Bypass -Command $stopWolfAccountServer");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(accountServerProjectPath)} -- " +
            $"--listen --address 0.0.0.0 --port {accountPort} --legacy-account-wire " +
            $"--world-endpoint {liveAddress}:{bridgeWorldPort}");
        Console.WriteLine("4. Verify AccountServer returns the bridge endpoint before live-client login:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"legacy-account-smoke {liveAddress} {accountPort} $smokeUsername $smokePassword $serverName " +
            $"--expect-ip {liveAddress} --expect-port {bridgeWorldPort}");
        Console.WriteLine("5. Confirm the bridge, AccountServer, and old world listener are all ready before submitting credentials:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"active-bridge-capture-status {QuotePowerShellSingle(outputPath)} {QuotePowerShellSingle(clientExePath)}");
        Console.WriteLine("   Continue only when status reports ReadyForManualLogin: True; if it reports a stale client world socket, close or restart the client first.");
        Console.WriteLine("6. Launch the D client, type credentials manually if automation cannot focus the fields, then enter once while the bridge and forwarded AccountServer are active:");
        Console.WriteLine("   Progress output should print 'accepted client' after successful auth hands the client to the bridge; if it stays on 'listening', the client has not reached world-entry yet.");
        Console.WriteLine($"Start-Process {QuotePowerShellSingle(clientLauncherPath)}");
        Console.WriteLine("7. Summarize the active bridge artifact after the bridge exits:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"legacy-world-active-bridge-summary {QuotePowerShellSingle(outputPath)}");
        Console.WriteLine("8. Stop the bridge-mode AccountServer, clear the capture override, and restore forwarding to the original world endpoint:");
        Console.WriteLine("powershell -NoProfile -ExecutionPolicy Bypass -Command $stopWolfAccountServer");
        Console.WriteLine("Remove-Item Env:\\WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER -ErrorAction SilentlyContinue");
        Console.WriteLine("Remove-Item Env:\\WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE -ErrorAction SilentlyContinue");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(accountServerProjectPath)} -- " +
            $"--listen --address 0.0.0.0 --port {accountPort} --legacy-account-wire " +
            $"--world-endpoint {liveAddress}:{originalWorldPort}");
        Console.WriteLine("9. Verify AccountServer returns the original endpoint after restore:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"legacy-account-smoke {liveAddress} {accountPort} $smokeUsername $smokePassword $serverName " +
            $"--expect-ip {liveAddress} --expect-port {originalWorldPort}");
        return 0;
    }

    static int ActiveBridgeCaptureStatus(string[] args)
    {
        ActiveBridgeCaptureStatusOptions options;
        try
        {
            options = ResolveActiveBridgeCaptureStatusOptions(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var processes = EnumerateWorldRelayCaptureClientProcesses(options.ClientExePath);
            var connections = EnumerateWindowsIPv4TcpConnections();
            var artifact = CreateActiveBridgeCaptureArtifactStatus(options.OutputPath);
            var report = ActiveBridgeCaptureStatusLogic.CreateReport(
                options.OutputPath,
                options.ClientExePath,
                processes,
                connections,
                artifact);

            Console.Write(ActiveBridgeCaptureStatusLogic.FormatReport(report));
            return report.ReadyForManualLogin ? 0 : 2;
        }
        catch (Exception ex) when (
            ex is Win32Exception or
                PlatformNotSupportedException or
                InvalidOperationException or
                NotSupportedException or
                TimeoutException or
                IOException or
                UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Active bridge capture status failed: {ex.Message}");
            return 1;
        }
    }

    static string ResolveActiveBridgeCaptureRunbookRootPath(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException("Active bridge capture runbook accepts at most one root path.");
        }

        return Path.GetFullPath(args.Length > 1 ? args[1] : ".");
    }

    static ActiveBridgeCaptureStatusOptions ResolveActiveBridgeCaptureStatusOptions(string[] args)
    {
        if (args.Length > 3)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool active-bridge-capture-status [activeBridgeCaptureJsonPath] [clientExePath]");
        }

        var outputPath = args.Length > 1
            ? args[1]
            : ActiveBridgeCaptureStatusLogic.DefaultOutputPath;
        var clientExePath = args.Length > 2
            ? args[2]
            : WorldRelayCapturePreflightLogic.DefaultClientExePath;

        return new ActiveBridgeCaptureStatusOptions(
            Path.GetFullPath(outputPath),
            WorldRelayCapturePreflightLogic.NormalizeClientExePath(clientExePath));
    }

    static ActiveBridgeCaptureArtifactStatus CreateActiveBridgeCaptureArtifactStatus(string outputPath)
    {
        var path = Path.GetFullPath(outputPath);
        if (!File.Exists(path))
        {
            return ActiveBridgeCaptureArtifactStatus.Missing(path);
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            var capture = ReadLegacyWorldActiveBridgeCaptureResult(path);
            return ActiveBridgeCaptureArtifactStatus.Found(
                path,
                bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                capture.HandshakeStatus,
                capture.Frames.Count,
                capture.StopReason);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return ActiveBridgeCaptureArtifactStatus.Unreadable(path, ex.Message);
        }
    }
}

internal static class ActiveBridgeCaptureStatusLogic
{
    public const string DefaultOutputPath = @".artifacts/active-bridge.json";

    private static readonly int[] ServicePorts = [9960, 15819, 5818];
    private static readonly int[] WatchedClientPorts = [2021, 80, 9960, 5818, 15819];
    private static readonly int[] BlockingClientPorts = [5818, 15819];

    public static ActiveBridgeCaptureStatusReport CreateReport(
        string outputPath,
        string clientExePath,
        IEnumerable<WorldRelayCapturePreflightProcess> processes,
        IEnumerable<WorldRelayCaptureTcpConnection> connections,
        ActiveBridgeCaptureArtifactStatus artifact)
    {
        var fullClientExePath = WorldRelayCapturePreflightLogic.NormalizeClientExePath(clientExePath);
        var clientProcesses = processes
            .Where(process => WorldRelayCapturePreflightLogic.PathsEqual(process.ExecutablePath, fullClientExePath))
            .GroupBy(process => process.Id)
            .Select(group => group.First())
            .OrderBy(process => process.Id)
            .ToArray();
        var clientProcessIds = clientProcesses
            .Select(process => process.Id)
            .ToHashSet();
        var serviceListeners = connections
            .Where(connection =>
                string.Equals(connection.State, "Listen", StringComparison.OrdinalIgnoreCase) &&
                ServicePorts.Contains(connection.LocalPort))
            .OrderBy(connection => connection.LocalPort)
            .ThenBy(connection => connection.LocalAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.OwningProcessId)
            .ToArray();
        var clientConnections = connections
            .Where(connection =>
                clientProcessIds.Contains(connection.OwningProcessId) &&
                (WatchedClientPorts.Contains(connection.LocalPort) ||
                    WatchedClientPorts.Contains(connection.RemotePort)))
            .OrderBy(connection => connection.OwningProcessId)
            .ThenBy(connection => connection.RemoteAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.RemotePort)
            .ThenBy(connection => connection.LocalPort)
            .ToArray();
        var blockingConnections = clientConnections
            .Where(connection =>
                (BlockingClientPorts.Contains(connection.LocalPort) ||
                    BlockingClientPorts.Contains(connection.RemotePort)) &&
                WorldRelayCapturePreflightLogic.IsActiveTcpState(connection.State))
            .ToArray();

        return new ActiveBridgeCaptureStatusReport(
            Path.GetFullPath(outputPath),
            fullClientExePath,
            artifact,
            serviceListeners,
            clientProcesses,
            clientConnections,
            blockingConnections);
    }

    public static string FormatReport(ActiveBridgeCaptureStatusReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Active bridge capture status:");
        builder.AppendLine($"Output JSON: {report.OutputPath}");
        builder.AppendLine($"Client: {report.ClientExePath}");
        builder.AppendLine($"Service listeners: {FormatServiceListener(report, 9960)}");
        builder.AppendLine($"Bridge listener: {FormatServiceListener(report, 15819)}");
        builder.AppendLine($"World listener: {FormatServiceListener(report, 5818)}");
        builder.AppendLine($"Client running: {report.ClientRunning}");
        builder.AppendLine($"Client PID(s): {WorldRelayCapturePreflightLogic.FormatClientProcessIds(report.ClientProcesses)}");
        builder.AppendLine("Client watched TCP connections:");

        if (report.ClientConnections.Count == 0)
        {
            builder.AppendLine("  none");
        }
        else
        {
            foreach (var connection in report.ClientConnections)
            {
                builder.AppendLine($"  {FormatConnection(connection)}");
            }
        }

        if (report.BlockingClientConnections.Count > 0)
        {
            builder.AppendLine(
                $"WARNING: active client world/bridge TCP connection(s) already open: {report.BlockingClientConnections.Count}");
        }

        builder.AppendLine(FormatArtifact(report.Artifact));
        builder.AppendLine($"ReadyForManualLogin: {report.ReadyForManualLogin}");
        builder.AppendLine($"NextAction: {GetNextAction(report)}");
        return builder.ToString();
    }

    private static string FormatServiceListener(ActiveBridgeCaptureStatusReport report, int port)
    {
        var listeners = report.ServiceListeners
            .Where(connection => connection.LocalPort == port)
            .ToArray();
        if (listeners.Length == 0)
        {
            return $"{WorldRelayCapturePreflightLogic.FormatKeyPort(port)} missing";
        }

        return $"{WorldRelayCapturePreflightLogic.FormatKeyPort(port)} present " +
            string.Join("; ", listeners.Select(FormatConnection));
    }

    private static string FormatConnection(WorldRelayCaptureTcpConnection connection)
    {
        return $"pid={connection.OwningProcessId} state={connection.State} " +
            $"local={connection.LocalAddress}:{connection.LocalPort} " +
            $"remote={connection.RemoteAddress}:{connection.RemotePort}";
    }

    private static string FormatArtifact(ActiveBridgeCaptureArtifactStatus artifact)
    {
        if (!artifact.Exists)
        {
            return artifact.Error is null
                ? $"Artifact: missing path={artifact.Path}"
                : $"Artifact: unreadable path={artifact.Path} error={artifact.Error}";
        }

        return $"Artifact: exists path={artifact.Path} length={artifact.Length} sha256={artifact.Sha256} " +
            $"handshakeStatus={artifact.HandshakeStatus} frames={artifact.Frames} stopReason={artifact.StopReason}";
    }

    private static string GetNextAction(ActiveBridgeCaptureStatusReport report)
    {
        if (!report.BridgeListenerPresent)
        {
            return "Start legacy-world-active-bridge-capture with --progress before submitting client login.";
        }

        if (!report.AccountListenerPresent)
        {
            return "Start WolfConquer AccountServer on 9960 forwarding to 127.0.0.1:15819.";
        }

        if (!report.WorldListenerPresent)
        {
            return "Restore or start the original world listener on 5818 before opening the bridge window.";
        }

        if (report.BlockingClientConnections.Count > 0)
        {
            return "Log out, close, or restart the client so the bridge can catch a fresh world-entry socket.";
        }

        return "Submit one client login while the bridge window is still running, then summarize the output JSON.";
    }
}

internal sealed record LegacyWorldActiveBridgeCaptureCommandOptions(
    LegacyWorldActiveBridgeCaptureOptions Bridge,
    string OutputPath,
    bool Progress);

internal sealed record ActiveBridgeCaptureStatusOptions(
    string OutputPath,
    string ClientExePath);

internal sealed record ActiveBridgeCaptureStatusReport(
    string OutputPath,
    string ClientExePath,
    ActiveBridgeCaptureArtifactStatus Artifact,
    IReadOnlyList<WorldRelayCaptureTcpConnection> ServiceListeners,
    IReadOnlyList<WorldRelayCapturePreflightProcess> ClientProcesses,
    IReadOnlyList<WorldRelayCaptureTcpConnection> ClientConnections,
    IReadOnlyList<WorldRelayCaptureTcpConnection> BlockingClientConnections)
{
    public bool AccountListenerPresent => ServiceListeners.Any(connection => connection.LocalPort == 9960);

    public bool BridgeListenerPresent => ServiceListeners.Any(connection => connection.LocalPort == 15819);

    public bool WorldListenerPresent => ServiceListeners.Any(connection => connection.LocalPort == 5818);

    public bool ClientRunning => ClientProcesses.Count > 0;

    public bool ReadyForManualLogin =>
        AccountListenerPresent &&
        BridgeListenerPresent &&
        WorldListenerPresent &&
        BlockingClientConnections.Count == 0;
}

internal sealed record ActiveBridgeCaptureArtifactStatus(
    string Path,
    bool Exists,
    long Length,
    string? Sha256,
    string? HandshakeStatus,
    int Frames,
    string? StopReason,
    string? Error)
{
    public static ActiveBridgeCaptureArtifactStatus Missing(string path) =>
        new(path, false, 0, null, null, 0, null, null);

    public static ActiveBridgeCaptureArtifactStatus Unreadable(string path, string error) =>
        new(path, false, 0, null, null, 0, null, error);

    public static ActiveBridgeCaptureArtifactStatus Found(
        string path,
        long length,
        string sha256,
        string handshakeStatus,
        int frames,
        string stopReason) =>
        new(path, true, length, sha256, handshakeStatus, frames, stopReason, null);
}
