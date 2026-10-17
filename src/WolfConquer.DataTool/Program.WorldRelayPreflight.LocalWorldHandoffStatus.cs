using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Runtime;

internal static partial class Program
{
    static LocalWorldHandoffStatusOptions ResolveLocalWorldHandoffStatusOptions(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool local-world-handoff-status [candidateWorldEndpointOrPort]");
        }

        var candidateWorldEndpoint = args.Length > 1
            ? LocalWorldHandoffStatusLogic.ParseCandidateWorldEndpoint(args[1])
            : LocalWorldHandoffStatusLogic.DefaultCandidateWorldEndpoint;
        return new LocalWorldHandoffStatusOptions(candidateWorldEndpoint);
    }

    static int LocalWorldHandoffStatus(string[] args)
    {
        LocalWorldHandoffStatusOptions options;
        try
        {
            options = ResolveLocalWorldHandoffStatusOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var connections = EnumerateWindowsIPv4TcpConnections();
            var processes = EnumerateWolfConquerCommandLineProcesses();
            var report = LocalWorldHandoffStatusLogic.CreateReport(
                options.CandidateWorldEndpoint,
                connections,
                processes);

            Console.Write(LocalWorldHandoffStatusLogic.FormatReport(report));
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
            Console.Error.WriteLine($"Local world handoff status failed: {ex.Message}");
            return 1;
        }
    }
}

internal static class LocalWorldHandoffStatusLogic
{
    public static ConquerEndpoint DefaultCandidateWorldEndpoint { get; } =
        new(ConquerRuntimeDefaults.WorldServer.Role, ConquerRuntimeDefaults.WorldServer.Address, 15818);

    private static ConquerEndpoint LoginWebEndpoint { get; } =
        new("LoginWeb", "127.0.0.1", 80);

    private static ConquerEndpoint AccountEndpoint { get; } =
        new(ConquerRuntimeDefaults.AccountServer.Role, "127.0.0.1", ConquerRuntimeDefaults.AccountServer.Port);

    private static ConquerEndpoint OldWorldEndpoint => ConquerRuntimeDefaults.WorldServer;

    public static ConquerEndpoint ParseCandidateWorldEndpoint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Candidate world endpoint or port is required.", nameof(value));
        }

        if (!value.Contains(':', StringComparison.Ordinal))
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
                port is <= 0 or > ushort.MaxValue)
            {
                throw new FormatException("Candidate world port must be between 1 and 65535.");
            }

            return new ConquerEndpoint(
                ConquerRuntimeDefaults.WorldServer.Role,
                ConquerRuntimeDefaults.WorldServer.Address,
                port);
        }

        return ParseWorldEndpoint(value, "candidate world endpoint");
    }

    public static LocalWorldHandoffStatusReport CreateReport(
        ConquerEndpoint candidateWorldEndpoint,
        IEnumerable<WorldRelayCaptureTcpConnection> connections,
        IEnumerable<LocalWorldHandoffProcess> processes)
    {
        ArgumentNullException.ThrowIfNull(candidateWorldEndpoint);

        var connectionArray = connections.ToArray();
        var processArray = processes
            .OrderBy(process => process.ProcessId)
            .ToArray();
        var loginWeb = CreateEndpointStatus("LoginWeb", LoginWebEndpoint, connectionArray);
        var account = CreateEndpointStatus("AccountServer", AccountEndpoint, connectionArray);
        var oldWorld = CreateEndpointStatus("Old world", OldWorldEndpoint, connectionArray);
        var candidateWorld = CreateEndpointStatus("Candidate world", candidateWorldEndpoint, connectionArray);
        var accountListenerProcessIds = account.Listeners
            .Select(listener => listener.OwningProcessId)
            .ToHashSet();
        var accountProcesses = processArray
            .Where(process => ContainsWolfConquerProcess(process, "WolfConquer.AccountServer"))
            .ToArray();
        var forwarding = CreateForwardStatus(
            account.Ready,
            accountListenerProcessIds,
            accountProcesses,
            candidateWorldEndpoint,
            OldWorldEndpoint);

        return new LocalWorldHandoffStatusReport(
            candidateWorldEndpoint,
            loginWeb,
            account,
            oldWorld,
            candidateWorld,
            accountProcesses,
            forwarding);
    }

    public static string FormatReport(LocalWorldHandoffStatusReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Local WolfConquer world handoff status:");
        builder.AppendLine($"Candidate world: {FormatEndpoint(report.CandidateWorldEndpoint)}");
        builder.AppendLine("Endpoint readiness:");
        builder.AppendLine($"  {FormatEndpointStatus(report.LoginWeb)}");
        builder.AppendLine($"  {FormatEndpointStatus(report.AccountServer)}");
        builder.AppendLine($"  {FormatEndpointStatus(report.OldWorld)}");
        builder.AppendLine($"  {FormatEndpointStatus(report.CandidateWorld)}");
        builder.AppendLine($"Account forward target: {FormatForwardStatus(report.ForwardStatus)}");
        builder.AppendLine("WolfConquer AccountServer process metadata:");

        if (report.AccountProcesses.Count == 0)
        {
            builder.AppendLine("  none");
        }
        else
        {
            foreach (var process in report.AccountProcesses)
            {
                builder.AppendLine($"  pid={process.ProcessId} name={process.Name}");
            }
        }

        builder.AppendLine($"All listeners ready: {report.AllListenersReady}");
        builder.AppendLine($"ReadyForManualLogin: {report.ReadyForManualLogin}");
        builder.AppendLine($"NextAction: {GetNextAction(report)}");
        return builder.ToString();
    }

    private static LocalWorldHandoffEndpointStatus CreateEndpointStatus(
        string name,
        ConquerEndpoint endpoint,
        IReadOnlyList<WorldRelayCaptureTcpConnection> connections)
    {
        var listeners = connections
            .Where(connection =>
                IsListeningTcpState(connection.State) &&
                IsListenerForEndpoint(connection, endpoint))
            .OrderBy(connection => connection.LocalAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.OwningProcessId)
            .ToArray();

        return new LocalWorldHandoffEndpointStatus(name, endpoint, listeners);
    }

    private static LocalWorldHandoffForwardStatus CreateForwardStatus(
        bool accountReady,
        IReadOnlySet<int> accountListenerProcessIds,
        IReadOnlyList<LocalWorldHandoffProcess> accountProcesses,
        ConquerEndpoint candidateWorldEndpoint,
        ConquerEndpoint oldWorldEndpoint)
    {
        if (!accountReady)
        {
            return new LocalWorldHandoffForwardStatus(
                LocalWorldHandoffForwardKind.NotRunning,
                null,
                "AccountServer listener is missing.",
                []);
        }

        var listenerProcesses = accountProcesses
            .Where(process => accountListenerProcessIds.Contains(process.ProcessId))
            .ToArray();
        if (listenerProcesses.Length == 0)
        {
            return new LocalWorldHandoffForwardStatus(
                LocalWorldHandoffForwardKind.Unknown,
                null,
                "No WolfConquer AccountServer command line matched the 9960 listener PID.",
                []);
        }

        var routes = listenerProcesses
            .Select(process => CreateForwardRoute(process, oldWorldEndpoint))
            .ToArray();
        if (routes.Any(route => route.Endpoint is null))
        {
            return new LocalWorldHandoffForwardStatus(
                LocalWorldHandoffForwardKind.Unknown,
                null,
                string.Join("; ", routes.Where(route => route.Endpoint is null).Select(route => route.Source)),
                routes);
        }

        var distinctEndpoints = routes
            .Select(route => route.Endpoint!)
            .DistinctBy(endpoint => $"{NormalizeEndpointAddress(endpoint.Address)}:{endpoint.Port}")
            .ToArray();
        if (distinctEndpoints.Length > 1)
        {
            return new LocalWorldHandoffForwardStatus(
                LocalWorldHandoffForwardKind.Mixed,
                null,
                "Multiple AccountServer listener PIDs expose different forward endpoints.",
                routes);
        }

        var endpoint = distinctEndpoints[0];
        var kind = EndpointMatches(endpoint, candidateWorldEndpoint)
            ? LocalWorldHandoffForwardKind.CandidateWorld
            : EndpointMatches(endpoint, oldWorldEndpoint)
                ? LocalWorldHandoffForwardKind.OldWorld
                : LocalWorldHandoffForwardKind.Other;

        return new LocalWorldHandoffForwardStatus(kind, endpoint, routes[0].Source, routes);
    }

    private static LocalWorldHandoffForwardRoute CreateForwardRoute(
        LocalWorldHandoffProcess process,
        ConquerEndpoint oldWorldEndpoint)
    {
        string[] tokens;
        try
        {
            tokens = SplitCommandLine(process.CommandLine);
        }
        catch (ArgumentException ex)
        {
            return new LocalWorldHandoffForwardRoute(process.ProcessId, null, ex.Message);
        }

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token.StartsWith(
                    AccountServerStartupOptions.WorldEndpointOption + "=",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ParseRouteEndpoint(
                    process.ProcessId,
                    token[(AccountServerStartupOptions.WorldEndpointOption.Length + 1)..],
                    AccountServerStartupOptions.WorldEndpointOption);
            }

            if (!string.Equals(token, AccountServerStartupOptions.WorldEndpointOption, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index == tokens.Length - 1)
            {
                return new LocalWorldHandoffForwardRoute(
                    process.ProcessId,
                    null,
                    $"pid {process.ProcessId} has {AccountServerStartupOptions.WorldEndpointOption} without a value");
            }

            return ParseRouteEndpoint(
                process.ProcessId,
                tokens[index + 1],
                AccountServerStartupOptions.WorldEndpointOption);
        }

        return new LocalWorldHandoffForwardRoute(
            process.ProcessId,
            oldWorldEndpoint,
            $"pid {process.ProcessId} default runtime endpoint");
    }

    private static LocalWorldHandoffForwardRoute ParseRouteEndpoint(
        int processId,
        string value,
        string source)
    {
        try
        {
            return new LocalWorldHandoffForwardRoute(processId, ParseWorldEndpoint(value, source), $"pid {processId} {source}");
        }
        catch (FormatException ex)
        {
            return new LocalWorldHandoffForwardRoute(processId, null, $"pid {processId} {ex.Message}");
        }
    }

    private static ConquerEndpoint ParseWorldEndpoint(string value, string source)
    {
        var colonIndex = value.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == value.Length - 1)
        {
            throw new FormatException($"{source} must use address:port format.");
        }

        var address = value[..colonIndex];
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new FormatException($"{source} address cannot be empty.");
        }

        if (!int.TryParse(value[(colonIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            port is <= 0 or > ushort.MaxValue)
        {
            throw new FormatException($"{source} port must be between 1 and 65535.");
        }

        return new ConquerEndpoint(ConquerRuntimeDefaults.WorldServer.Role, address, port);
    }

    private static string[] SplitCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
        {
            throw new ArgumentException("Command line has an unterminated quote.");
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens.ToArray();
    }

    private static bool ContainsWolfConquerProcess(LocalWorldHandoffProcess process, string processName)
    {
        return process.CommandLine.Contains(processName, StringComparison.OrdinalIgnoreCase) ||
            process.Name.Contains(processName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsListenerForEndpoint(WorldRelayCaptureTcpConnection connection, ConquerEndpoint endpoint)
    {
        return connection.LocalPort == endpoint.Port &&
            (IsWildcardAddress(connection.LocalAddress) || AddressesMatch(connection.LocalAddress, endpoint.Address));
    }

    private static bool EndpointMatches(ConquerEndpoint endpoint, ConquerEndpoint expected)
    {
        return endpoint.Port == expected.Port && AddressesMatch(endpoint.Address, expected.Address);
    }

    private static bool AddressesMatch(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsLoopbackAlias(left) && IsLoopbackAlias(right))
        {
            return true;
        }

        return false;
    }

    private static bool IsLoopbackAlias(string value)
    {
        return string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(value, out var address) && IPAddress.IsLoopback(address));
    }

    private static bool IsWildcardAddress(string value)
    {
        return string.Equals(value, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "::", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsListeningTcpState(string state)
    {
        var normalized = state.Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        return string.Equals(normalized, "Listen", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Listening", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatEndpointStatus(LocalWorldHandoffEndpointStatus status)
    {
        var listenerText = status.Listeners.Count == 0
            ? "<none>"
            : string.Join("; ", status.Listeners.Select(FormatConnection));
        return $"{status.Name}: {(status.Ready ? "ready" : "missing")} " +
            $"desired={FormatEndpoint(status.Endpoint)} listeners={listenerText}";
    }

    private static string FormatForwardStatus(LocalWorldHandoffForwardStatus status)
    {
        var endpointText = status.Endpoint is null
            ? "<none>"
            : FormatEndpoint(status.Endpoint);
        var routeText = status.Routes.Count == 0
            ? "<none>"
            : string.Join(", ", status.Routes.Select(FormatRoute));
        return $"{FormatKind(status.Kind)} endpoint={endpointText} source={status.Source} routes={routeText}";
    }

    private static string FormatRoute(LocalWorldHandoffForwardRoute route)
    {
        var endpointText = route.Endpoint is null
            ? "<none>"
            : FormatEndpoint(route.Endpoint);
        return $"pid={route.ProcessId}:{endpointText}";
    }

    private static string FormatKind(LocalWorldHandoffForwardKind kind)
    {
        return kind switch
        {
            LocalWorldHandoffForwardKind.CandidateWorld => "candidate-world",
            LocalWorldHandoffForwardKind.OldWorld => "old-world",
            LocalWorldHandoffForwardKind.Other => "other",
            LocalWorldHandoffForwardKind.Mixed => "mixed",
            LocalWorldHandoffForwardKind.NotRunning => "not-running",
            _ => "unknown"
        };
    }

    private static string FormatConnection(WorldRelayCaptureTcpConnection connection)
    {
        return $"pid={connection.OwningProcessId} state={connection.State} " +
            $"local={connection.LocalAddress}:{connection.LocalPort}";
    }

    private static string FormatEndpoint(ConquerEndpoint endpoint)
    {
        return $"{endpoint.Address}:{endpoint.Port}";
    }

    private static string GetNextAction(LocalWorldHandoffStatusReport report)
    {
        if (!report.LoginWeb.Ready)
        {
            return "Start WolfConquer LoginWeb on 127.0.0.1:80 before opening the client.";
        }

        if (!report.AccountServer.Ready)
        {
            return "Start WolfConquer AccountServer on 127.0.0.1:9960 with --legacy-account-wire.";
        }

        if (!report.OldWorld.Ready)
        {
            return "Restore or start the old world listener on 127.0.0.1:5818 before the handoff test.";
        }

        if (!report.CandidateWorld.Ready)
        {
            return $"Start WolfConquer WorldServer with --legacy-game-wire on {FormatEndpoint(report.CandidateWorldEndpoint)}.";
        }

        if (report.ForwardStatus.Kind == LocalWorldHandoffForwardKind.CandidateWorld)
        {
            return "Submit one manual client login; AccountServer is forwarding to the local WolfConquer world candidate.";
        }

        if (report.ForwardStatus.Kind == LocalWorldHandoffForwardKind.OldWorld)
        {
            return $"Restart WolfConquer AccountServer on 9960 with --world-endpoint {FormatEndpoint(report.CandidateWorldEndpoint)}.";
        }

        return "Inspect or restart WolfConquer AccountServer; the current 9960 forward target is not the candidate world endpoint.";
    }

    private static string NormalizeEndpointAddress(string value)
    {
        return IsLoopbackAlias(value) ? "127.0.0.1" : value.ToLowerInvariant();
    }
}

internal sealed record LocalWorldHandoffStatusOptions(ConquerEndpoint CandidateWorldEndpoint);

internal sealed record LocalWorldHandoffProcess(
    int ProcessId,
    string Name,
    string CommandLine);

internal sealed record LocalWorldHandoffEndpointStatus(
    string Name,
    ConquerEndpoint Endpoint,
    IReadOnlyList<WorldRelayCaptureTcpConnection> Listeners)
{
    public bool Ready => Listeners.Count > 0;
}

internal enum LocalWorldHandoffForwardKind
{
    Unknown,
    CandidateWorld,
    OldWorld,
    Other,
    Mixed,
    NotRunning
}

internal sealed record LocalWorldHandoffForwardRoute(
    int ProcessId,
    ConquerEndpoint? Endpoint,
    string Source);

internal sealed record LocalWorldHandoffForwardStatus(
    LocalWorldHandoffForwardKind Kind,
    ConquerEndpoint? Endpoint,
    string Source,
    IReadOnlyList<LocalWorldHandoffForwardRoute> Routes);

internal sealed record LocalWorldHandoffStatusReport(
    ConquerEndpoint CandidateWorldEndpoint,
    LocalWorldHandoffEndpointStatus LoginWeb,
    LocalWorldHandoffEndpointStatus AccountServer,
    LocalWorldHandoffEndpointStatus OldWorld,
    LocalWorldHandoffEndpointStatus CandidateWorld,
    IReadOnlyList<LocalWorldHandoffProcess> AccountProcesses,
    LocalWorldHandoffForwardStatus ForwardStatus)
{
    public bool AllListenersReady =>
        LoginWeb.Ready &&
        AccountServer.Ready &&
        OldWorld.Ready &&
        CandidateWorld.Ready;

    public bool ReadyForManualLogin =>
        AllListenersReady &&
        ForwardStatus.Kind == LocalWorldHandoffForwardKind.CandidateWorld;
}
