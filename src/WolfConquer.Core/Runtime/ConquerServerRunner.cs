using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Runtime;

public static class ConquerServerRunner
{
    public static async Task<int> RunAsync(ConquerEndpoint endpoint, string[] args, CancellationToken cancellationToken = default)
    {
        return await RunAsync(endpoint, args, frameHandler: null, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<int> RunAsync(
        ConquerEndpoint endpoint,
        string[] args,
        Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask>? frameHandler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(args);

        var runOptions = ServerRunOptions.Parse(endpoint, args);

        Console.WriteLine($"{endpoint.Role} for {ConquerRuntimeDefaults.ServerName}");
        Console.WriteLine($"Planned listen endpoint: {runOptions.Address}:{runOptions.Port}");
        Console.WriteLine($"Frame size limit: {ConquerRuntimeDefaults.ReceiveSize} bytes");
        Console.WriteLine($"Wire mode: {runOptions.WireMode}");

        LegacyGuardShieldTextProtocolOptions? guardShieldTextProtocol = null;
        if (runOptions.WireMode == ConquerTcpWireMode.LegacyGuardShield)
        {
            guardShieldTextProtocol = LegacyGuardShieldTextProtocolOptions.FromEnvironment(
                ReportGuardShieldUsage,
                ReportGuardShieldCommand);
            Console.WriteLine($"GuardShield INI: {guardShieldTextProtocol.SourcePath ?? "<not loaded>"}");
            Console.WriteLine($"GuardShield ban policy: {guardShieldTextProtocol.EffectiveBanPolicy.ToStatusText()}");
            Console.WriteLine(
                $"GuardShield effective ban hours: {guardShieldTextProtocol.EffectiveBanPolicy.ToEffectiveStatusText()}");
        }

        if (!runOptions.Listen)
        {
            Console.WriteLine("Pass --listen to start accepting TCP clients.");
            return 0;
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Cancel();
        };

        await using var server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                endpoint,
                IPAddress.Parse(runOptions.Address),
                runOptions.Port,
                ServerFirstPayloadFactory: runOptions.WireMode is not ConquerTcpWireMode.LegacyGameServer and
                    not ConquerTcpWireMode.LegacyGuardShield
                    ? () => ConquerServerFirstPayloads.Create(endpoint)
                    : null,
                WireMode: runOptions.WireMode,
                GuardShieldTextProtocol: runOptions.WireMode == ConquerTcpWireMode.LegacyGuardShield
                    ? guardShieldTextProtocol
                    : null),
            frameHandler ?? CreateDefaultFrameHandler(endpoint));

        server.Start(stop.Token);
        Console.WriteLine($"Listening on {server.BoundEndPoint}");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }

        return 0;
    }

    private static Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask> CreateDefaultFrameHandler(
        ConquerEndpoint endpoint)
    {
        return (connection, frame, _) =>
        {
            Console.WriteLine($"{endpoint.Role} received opcode {frame.Header.Opcode} from {connection.RemoteEndPoint}");
            return ValueTask.CompletedTask;
        };
    }

    private static void ReportGuardShieldUsage(LegacyGuardShieldUsageReport report)
    {
        var banText = report.BanDecision is null
            ? string.Empty
            : $" reason={report.BanDecision.Reason} configuredHours={report.BanDecision.ConfiguredHours} effectiveHours={report.BanDecision.EffectiveHours}";
        Console.WriteLine(
            $"Gateway GuardShield report {report.Kind} user={report.Username} server={report.Server} detail={report.Detail}{banText}");
    }

    private static void ReportGuardShieldCommand(LegacyGuardShieldCommandReport report)
    {
        Console.WriteLine(
            $"Gateway GuardShield command command={report.Command} status={report.Status} fields={report.FieldCount} " +
            $"responseBytes={report.ResponseLength} usage={report.UsageKind ?? "<none>"}");
        if (report.Status == LegacyGuardShieldTextCommandStatus.Ignored)
        {
            var command = NormalizeLogToken(report.Command);
            Console.WriteLine(
                "PARITY-GAP category=unsupported-packet severity=warning gap=guard-shield-command " +
                $"dedupe=unsupported-packet:guard-shield-command:command={command}:status=Ignored " +
                $"command={command} fieldCount={report.FieldCount} responseBytes={report.ResponseLength}; " +
                "ignored GuardShield text command is not modeled");
        }
    }

    private static string NormalizeLogToken(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var previousWasDash = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '.' or ':')
            {
                builder.Append(character);
                previousWasDash = false;
            }
            else if (!previousWasDash)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        var token = builder.ToString().Trim('-');
        return token.Length == 0 ? "unknown" : token;
    }

    private sealed record ServerRunOptions(bool Listen, string Address, int Port, ConquerTcpWireMode WireMode)
    {
        public static ServerRunOptions Parse(ConquerEndpoint endpoint, string[] args)
        {
            var listen = args.Any(arg => string.Equals(arg, "--listen", StringComparison.OrdinalIgnoreCase));
            var address = GetOption(args, "--address") ?? endpoint.Address;
            var portText = GetOption(args, "--port");
            var port = portText is null ? endpoint.Port : ParsePort(portText);
            var wireMode = ParseWireMode(endpoint, args);
            return new ServerRunOptions(listen, address, port, wireMode);
        }

        private static ConquerTcpWireMode ParseWireMode(ConquerEndpoint endpoint, string[] args)
        {
            var legacyGameWire = args.Any(arg =>
                string.Equals(arg, "--legacy-game-wire", StringComparison.OrdinalIgnoreCase));
            var legacyAccountWire = args.Any(arg =>
                string.Equals(arg, "--legacy-account-wire", StringComparison.OrdinalIgnoreCase));
            var guardShieldWire = args.Any(arg =>
                string.Equals(arg, "--guard-shield-wire", StringComparison.OrdinalIgnoreCase));

            var selectedWireModes = new[] { legacyGameWire, legacyAccountWire, guardShieldWire }
                .Count(selected => selected);
            if (selectedWireModes > 1)
            {
                throw new ArgumentException("Only one wire mode can be selected.");
            }

            if (selectedWireModes == 0)
            {
                return ConquerTcpWireMode.Modern;
            }

            if (legacyGameWire)
            {
                if (!string.Equals(endpoint.Role, ConquerRuntimeDefaults.WorldServer.Role, StringComparison.Ordinal))
                {
                    throw new ArgumentException("--legacy-game-wire is only valid for WorldServer.");
                }

                return ConquerTcpWireMode.LegacyGameServer;
            }

            if (legacyAccountWire)
            {
                if (!string.Equals(endpoint.Role, ConquerRuntimeDefaults.AccountServer.Role, StringComparison.Ordinal))
                {
                    throw new ArgumentException("--legacy-account-wire is only valid for AccountServer.");
                }

                return ConquerTcpWireMode.LegacyAccountServer;
            }

            if (!string.Equals(endpoint.Role, ConquerRuntimeDefaults.Gateway.Role, StringComparison.Ordinal))
            {
                throw new ArgumentException("--guard-shield-wire is only valid for Gateway.");
            }

            return ConquerTcpWireMode.LegacyGuardShield;
        }

        private static string? GetOption(string[] args, string name)
        {
            for (var index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index == args.Length - 1)
                {
                    throw new ArgumentException($"Missing value for {name}.");
                }

                return args[index + 1];
            }

            return null;
        }

        private static int ParsePort(string value)
        {
            if (!int.TryParse(value, out var port) || port is < 0 or > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Port must be between 0 and 65535.");
            }

            return port;
        }
    }
}
